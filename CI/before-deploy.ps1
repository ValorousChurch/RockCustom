# This script is run by AppVeyor's deploy agent before the deploy
$ErrorActionPreference = 'Stop'
$webroot = (Resolve-Path -LiteralPath $env:application_path).ProviderPath
$backup = Join-Path (Split-Path -Parent $webroot) 'bkup'

# Production XDT is intentionally stored on the server, never inside the web artifact.
$transformPath = 'C:\RockDeployment\Config\web.production.xdt'
if (-not (Test-Path -LiteralPath $transformPath -PathType Leaf)) {
    throw "Production XDT is missing: $transformPath. Refusing to deploy."
}
Write-Host "Pre-deploy: $webroot"
Write-Output "--------------------------------------------------"
Write-Output "Running script as: $env:userdomain\$env:username"
if (Test-Path -LiteralPath $backup) {
    if (@(Get-ChildItem -LiteralPath $backup -Force).Count -gt 0) {
        throw "Backup staging directory is not empty: $backup. Resolve previous incomplete deployment before proceeding."
    }
} else { New-Item -Path $backup -ItemType Directory -Force | Out-Null }
# Preserve old web.config for rollback only. The post-deploy script must NOT restore it.
foreach ($name in @('web.config','web.connectionstrings.config')) {
    $source = Join-Path $webroot $name
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Required configuration file not found: $source" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $backup $name) -Force -Verbose
}
$themeRoot = Join-Path $webroot 'Themes'
if (Test-Path -LiteralPath $themeRoot) {
    Get-ChildItem -LiteralPath $themeRoot -Filter '*-overrides.less' -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($webroot.Length).TrimStart('\','/')
        $dest = Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Force -Verbose
    }
}
$template = Join-Path $webroot 'app_offline-template.htm'
if (-not (Test-Path -LiteralPath $template -PathType Leaf)) { throw "Missing app_offline-template.htm: $template" }
Copy-Item -LiteralPath $template -Destination (Join-Path $webroot 'app_offline.htm') -Force -Verbose
Write-Host 'Pre-deploy completed: configuration staged; application offline.'
