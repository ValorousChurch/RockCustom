# AppVeyor Agent post-deployment hook. Default is report-only.
$ErrorActionPreference = 'Stop'
$postDeployDelete = @('deployment-manifest.json', 'before-deploy.ps1', 'deploy.ps1', 'Sync-DeploymentFiles.ps1', 'Apply-WebConfigTransform.ps1')

$webroot = (Resolve-Path -LiteralPath $env:application_path).ProviderPath
$backup = Join-Path (Split-Path -Parent $webroot) 'bkup'

# Change to 'Cleanup' only after reports have been compared with official Rock lists.
$cleanupMode = 'Report'
Write-Host "Post-deploy: $webroot (cleanup mode: $cleanupMode)"
Write-Output "--------------------------------------------------"
Write-Output "Running script as: $env:userdomain\$env:username"
if (-not (Test-Path -LiteralPath $backup)) { throw "Missing backup staging folder: $backup" }
# Restore connection strings and theme overrides. Keep backed-up web.config for rollback,
# but never restore it over the newly deployed upstream Rock web.config.
Get-ChildItem -LiteralPath $backup -File -Recurse | ForEach-Object {
    $rel = $_.FullName.Substring($backup.Length).TrimStart('\', '/')
    if ($rel -ieq 'web.config') { return }
    $dest = Join-Path $webroot $rel
    New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $dest -Force -Verbose
}
# Apply production-specific configuration while the application is still offline.
$transformScript = Join-Path $webroot 'Apply-WebConfigTransform.ps1'
$transformPath = 'C:\RockDeployment\Config\web.production.xdt'
if (-not (Test-Path -LiteralPath $transformScript -PathType Leaf)) { throw "Missing XDT script: $transformScript" }
& $transformScript -WebRoot $webroot -TransformPath $transformPath
if (-not $?) { throw 'Production web.config transform failed.' }

$sync = Join-Path $webroot 'Sync-DeploymentFiles.ps1'
if (-not (Test-Path -LiteralPath $sync -PathType Leaf)) { throw "Missing cleanup script in artifact: $sync" }
& $sync -WebRoot $webroot -Mode $cleanupMode
if (-not $?) { throw 'Deployment file comparison failed.' }
$compressed = 'C:\inetpub\temp\IIS Temporary Compressed Files'
if (Test-Path -LiteralPath $compressed) {
    Get-ChildItem -LiteralPath $compressed -Force | Remove-Item -Recurse -Force -ErrorAction Continue
}
$offline = Join-Path $webroot 'app_offline.htm'
if (Test-Path -LiteralPath $offline) { Remove-Item -LiteralPath $offline -Force }
# Only remove staging files after the application is back online.
Get-ChildItem -LiteralPath $backup -Force | Remove-Item -Recurse -Force
foreach ($name in $postDeployDelete) {
    $p = Join-Path $webroot $name
    if (Test-Path -LiteralPath $p -PathType Leaf) { Remove-Item -LiteralPath $p -Force }
}
Write-Host 'Post-deploy completed.'
