# Run after all builds finish, before AppVeyor packages RockWeb.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$WebRoot,
    [string]$RockCommit = $env:APPVEYOR_REPO_COMMIT,
    [string]$RockCustomCommit = '',
    [string]$BuildNumber = $env:APPVEYOR_BUILD_VERSION
)
$ErrorActionPreference = 'Stop'
$skipExact = @('web.config','web.connectionstrings.config','app_offline.htm','app_offline-template.htm','deployment-manifest.json','before-deploy.ps1','deploy.ps1','Sync-DeploymentFiles.ps1','Apply-WebConfigTransform.ps1')

$root = (Resolve-Path -LiteralPath $WebRoot).ProviderPath.TrimEnd('\','/')
if (-not $RockCustomCommit) {
    $customPath = 'C:\projects\RockCustom'
    if (Test-Path -LiteralPath (Join-Path $customPath '.git')) {
        $RockCustomCommit = (& git -C $customPath rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Failed to read RockCustom commit.' }
    }
}
# Inventory only packaged files. Exclude machine-specific files and deployment metadata.
$files = @(Get-ChildItem -LiteralPath $root -File -Recurse -Force | ForEach-Object {
    $relative = $_.FullName.Substring($root.Length).TrimStart('\','/').Replace('\','/')
    if ($relative -in $skipExact) { return }
    if ($relative -match '(?i:-overrides\.less)$') { return }
    $relative
} | Sort-Object -Unique)
if ($files.Count -eq 0) { throw "No deployable files found under $root" }
$manifest = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    rockCommit = $RockCommit
    rockCustomCommit = $RockCustomCommit
    appveyorBuild = $BuildNumber
    files = $files
}
$out = Join-Path $root 'deployment-manifest.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $out -Encoding UTF8
Write-Host "Created deployment manifest containing $($files.Count) files: $out"
