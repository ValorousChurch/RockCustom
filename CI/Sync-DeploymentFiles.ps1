# Invoked locally by deploy.ps1 AFTER the AppVeyor Agent has copied all website files.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$WebRoot,
    [ValidateSet('Report','Cleanup')][string]$Mode = 'Report',
    [string]$StateRoot = '',
    [switch]$InitializeBaseline
)
$ErrorActionPreference = 'Stop'
$protectedExact = @('web.config','web.connectionstrings.config','app_offline.htm','app_offline-template.htm','deployment-manifest.json','before-deploy.ps1','deploy.ps1','Sync-DeploymentFiles.ps1','Apply-WebConfigTransform.ps1')

$root = (Resolve-Path -LiteralPath $WebRoot).ProviderPath.TrimEnd('\','/')
if (-not $StateRoot) { $StateRoot = Join-Path (Split-Path -Parent $root) 'deployment-state' }
if (-not (Test-Path -LiteralPath $StateRoot)) { New-Item -ItemType Directory -Path $StateRoot -Force | Out-Null }
$state = (Resolve-Path -LiteralPath $StateRoot).ProviderPath
$incomingPath = Join-Path $root 'deployment-manifest.json'
$baselinePath = Join-Path $state 'deployment-manifest.json'
if (-not (Test-Path -LiteralPath $incomingPath -PathType Leaf)) { throw 'Incoming deployment-manifest.json is missing. Refusing cleanup.' }
function Read-Manifest([string]$path) {
    $m = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($m.schemaVersion -ne 1 -or $null -eq $m.files -or @($m.files).Count -eq 0) { throw "Manifest invalid: $path" }
    $dict = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($p in @($m.files)) {
        if ($p -isnot [string] -or [string]::IsNullOrWhiteSpace($p)) { throw "Invalid manifest path in $path" }
        if ($p -match '(^/|^[A-Za-z]:|\\|(^|/)\.\.?(/|$)|//|:)' -or $p -match '[\x00-\x1F]') { throw "Unsafe manifest path: $p" }
        if (-not $dict.Add($p)) { throw "Duplicate manifest path: $p" }
    }
    return @{ Metadata=$m; Paths=$dict }
}
$incoming = Read-Manifest $incomingPath
if (-not (Test-Path -LiteralPath $baselinePath -PathType Leaf)) {
    if (-not $InitializeBaseline) { throw 'No baseline manifest found. Bootstrap requires -InitializeBaseline; no deletions can be inferred on first run.' }
    Copy-Item -LiteralPath $incomingPath -Destination $baselinePath -Force
    Write-Host 'Baseline initialized from current artifact; no deletions performed. Previously stale files require manual review.'
    return
}
$previous = Read-Manifest $baselinePath
$candidates = New-Object 'System.Collections.Generic.List[string]'
foreach ($p in $previous.Paths) {
    if ($incoming.Paths.Contains($p)) { continue }
    if ($p -in $protectedExact -or $p -match '(?i:-overrides\.less)$') { continue }
    $candidates.Add($p)
}
$report = Join-Path $state ("deleted-files-{0}.txt" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$candidates.Sort([StringComparer]::OrdinalIgnoreCase)
@($candidates) | Set-Content -LiteralPath $report -Encoding UTF8
Write-Host "Mode=$Mode; obsolete managed paths=$($candidates.Count); report=$report"
foreach ($p in $candidates) { Write-Host "OBSOLETE $p" }
if ($Mode -eq 'Report') {
    Write-Host 'Report only: production files and baseline are unchanged.'
    return
}
# Archive files before removal. Do not follow reparse points or traverse outside WebRoot.
$archive = Join-Path $state ("removed-{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $archive -Force | Out-Null
foreach ($p in $candidates) {
    $target = Join-Path $root ($p.Replace('/', [IO.Path]::DirectorySeparatorChar))
    $relativeParts = $p.Split('/')
    $cursor = $root
    foreach ($part in $relativeParts) {
        $cursor = Join-Path $cursor $part
        if (Test-Path -LiteralPath $cursor) {
            $entry = Get-Item -LiteralPath $cursor -Force
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Refusing reparse point: $cursor" }
        }
    }
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { continue }
    $dest = Join-Path $archive ($p.Replace('/', [IO.Path]::DirectorySeparatorChar))
    New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination $dest -Force
    Remove-Item -LiteralPath $target -Force
    Write-Host "REMOVED $p"
}
# Filesystem cleanup succeeded. Promote baseline;
Copy-Item -LiteralPath $baselinePath -Destination (Join-Path $state 'deployment-manifest.previous.json') -Force
Copy-Item -LiteralPath $incomingPath -Destination $baselinePath -Force
Write-Host "Cleanup complete. Previous baseline and archived removals retained at $state"
