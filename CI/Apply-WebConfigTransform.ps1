# Apply a server-local production XDT to the newly deployed Rock web.config.
# Requires Windows PowerShell 5.1, the Rock-provided XDT DLL, and app_offline.htm.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$WebRoot,
    [string]$TransformPath = 'C:\inetpub\Config\web.production.xdt'
)

$ErrorActionPreference = 'Stop'
$webRootFull = (Resolve-Path -LiteralPath $WebRoot -ErrorAction Stop).ProviderPath
$configPath = Join-Path $webRootFull 'web.config'
$assemblyPath = Join-Path $webRootFull 'bin\Microsoft.Web.XmlTransform.dll'
$offlinePath = Join-Path $webRootFull 'app_offline.htm'

foreach ($path in @($configPath, $assemblyPath, $TransformPath, $offlinePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required deployment file missing: $path"
    }
}

Add-Type -LiteralPath $assemblyPath
$document = New-Object Microsoft.Web.XmlTransform.XmlTransformableDocument
$document.PreserveWhitespace = $true
$document.XmlResolver = $null
$document.Load($configPath)
$transformation = New-Object Microsoft.Web.XmlTransform.XmlTransformation($TransformPath)
$tempFile = Join-Path $webRootFull ('web.config.' + [guid]::NewGuid().ToString('N') + '.tmp')

try {
    if (-not $transformation.Apply($document)) {
        throw 'Production web.config XDT application failed.'
    }

    # Save to a temporary file, then independently parse the result.
    $document.Save($tempFile)
    $verified = New-Object System.Xml.XmlDocument
    $verified.XmlResolver = $null
    $verified.Load($tempFile)

    # Fail closed if Rock's stock keys remain, if keys are absent, or if the
    # server-local XDT still contains unfilled REPLACE_* placeholders.
    # Do not log actual secret values.
    $stockKeys = @{
        PasswordKey = 'D42E08ECDE448643C528C899F90BADC9411AE07F74F9BA00A81BA06FD17E3D6BA22C4AE6947DD9686A35E8538D72B471F14CDB31BD50B9F5B2A1C26E290E5FC2'
        DataEncryptionKey = 'v8Hw27G0dXAhjo9HCzi5MFMwJZznhunhivaillaPjpzc3czzTBkkCz+PzaRyCq61Rsvn7oq2G5zMHoixGT0lvc2uuuoHRIduOZJ0uxTGLa48ZWfJ2iBY8lSgwFmRgYEEOSZZQyA0nGSwJlgNicJRCfArC8IikRMKRWQCzenaLjA='
    }

    foreach ($key in @('PasswordKey', 'DataEncryptionKey')) {
        $node = $verified.SelectSingleNode("/configuration/appSettings/add[@key='$key']")
        if ($null -eq $node) {
            throw "Production web.config validation failed: $key is missing."
        }
        $value = $node.GetAttribute('value')
        if ([string]::IsNullOrWhiteSpace($value) -or
            $value -ceq $stockKeys[$key] -or
            $value -like 'REPLACE_*' -or
            $value -ceq 'REDACTED') {
            throw "Production web.config validation failed: $key is unset or contains a placeholder."
        }
    }

    # Atomically replace only after successful XDT application and validation.
    [System.IO.File]::Replace($tempFile, $configPath, $null)
    Write-Host 'Production web.config XDT applied and validated.'
}
finally {
    $transformation.Dispose()
    if (Test-Path -LiteralPath $tempFile) {
        Remove-Item -LiteralPath $tempFile -Force
    }
}
