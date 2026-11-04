# Copies the project's code and data into the verification copy (%TEMP%\cs_verify by default) and proves the copy
# matches: the batch tests can then run there while the Unity editor keeps this project open.
#   powershell -ExecutionPolicy Bypass -File Tools/sync_verify.ps1 [-Dest C:\path\to\copy] [-Extra font,UI]
# Mirrors Assets/<CS, Editor, Tests, Resources, Scenes, Enemies> (+ -Extra folders) with their .meta files, deleting
# files the project no longer has, then copies every other .meta under Assets that is missing or different, plus
# Packages/manifest.json. Afterwards every mirrored file is SHA-256 compared with the original; exit code 1 when
# anything differs or is missing, 0 when the copy is identical.
# Why this script: an earlier copy step broke on the Korean folder names in the project path and the tests then
# passed against old code. Everything here takes paths from $PSScriptRoot (no path text in the script itself),
# hands them to robocopy as separate arguments, and checks the result instead of trusting the copy.
param(
  [string]$Dest = (Join-Path $env:TEMP 'cs_verify'),
  [string[]]$Extra = @()
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$folders = @('CS', 'Editor', 'Tests', 'Resources', 'Scenes', 'Enemies') + $Extra
$srcAssets = Join-Path $project 'Assets'
$dstAssets = Join-Path $Dest 'Assets'

function Invoke-Robo([string[]]$roboArgs) {
  & robocopy @roboArgs /NJH /NJS /NP /NFL /NDL /R:2 /W:1 | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE): $($roboArgs -join ' ')" }
}

if (-not (Test-Path -LiteralPath $dstAssets)) {
  Write-Host "No verification copy at $Dest - creating it (the first Unity open there imports everything, slow)."
  foreach ($d in 'ProjectSettings', 'Packages') { Invoke-Robo @((Join-Path $project $d), (Join-Path $Dest $d), '/MIR') }
  Invoke-Robo @($srcAssets, $dstAssets, '/E')
}

# Its own product name, so its batch runs keep their save under LocalLow/<company>/team_verify instead of writing
# the player's real save (the same company/product would share one file).
$settings = Join-Path $Dest 'ProjectSettings\ProjectSettings.asset'
if (Test-Path -LiteralPath $settings) {
  $text = [IO.File]::ReadAllText($settings)
  $named = [regex]::Replace($text, '(?m)^  productName: .*$', '  productName: team_verify')
  if ($named -ne $text) { [IO.File]::WriteAllText($settings, $named) }
}

foreach ($f in $folders) {
  $s = Join-Path $srcAssets $f
  if (-not (Test-Path -LiteralPath $s)) { Write-Warning "skip missing Assets/$f"; continue }
  Invoke-Robo @($s, (Join-Path $dstAssets $f), '/MIR')
  if (Test-Path -LiteralPath "$s.meta") { Copy-Item -LiteralPath "$s.meta" -Destination (Join-Path $dstAssets "$f.meta") -Force }
}
# Other .meta files (new folders / assets elsewhere in Assets), copied when missing or changed - nothing deleted.
Invoke-Robo @($srcAssets, $dstAssets, '*.meta', '/S')
# The manifest without the Unity-MCP package: the copy has no use for it, and it updates itself in the project
# (1.6.0 -> 1.6.1), which made every batch run here re-resolve packages, reload scripts mid-run and hang on its
# server connection. The lock file goes with it (Unity rewrites it from the manifest).
$manifest = [IO.File]::ReadAllText((Join-Path $project 'Packages\manifest.json'))
$manifest = [regex]::Replace($manifest, '(?m)^\s*"com\.ivanmurzak\.unity\.mcp"\s*:\s*"[^"]*",?\r?\n', '')
$manifest = [regex]::Replace($manifest, ',(\s*\r?\n\s*\})', '$1') # a trailing comma left when it was the last entry
$dstManifest = Join-Path $Dest 'Packages\manifest.json'
if (-not (Test-Path -LiteralPath $dstManifest) -or [IO.File]::ReadAllText($dstManifest) -ne $manifest) {
  [IO.File]::WriteAllText($dstManifest, $manifest)
  Remove-Item -LiteralPath (Join-Path $Dest 'Packages\packages-lock.json') -ErrorAction SilentlyContinue
}

# ---- Verify: same relative paths, same SHA-256.
function Get-Tree([string]$root) {
  $map = @{}
  if (-not (Test-Path -LiteralPath $root)) { return $map }
  $prefix = (Resolve-Path -LiteralPath $root).ProviderPath.TrimEnd('\').Length + 1
  Get-ChildItem -LiteralPath $root -Recurse -File -Force | ForEach-Object {
    $map[$_.FullName.Substring($prefix)] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
  }
  return $map
}
$bad = 0; $count = 0
foreach ($f in $folders) {
  $a = Get-Tree (Join-Path $srcAssets $f)
  $b = Get-Tree (Join-Path $dstAssets $f)
  foreach ($k in $a.Keys) {
    $count++
    if (-not $b.ContainsKey($k)) { Write-Host "MISSING  Assets/$f/$k"; $bad++ }
    elseif ($a[$k] -ne $b[$k]) { Write-Host "DIFFERS  Assets/$f/$k"; $bad++ }
  }
  foreach ($k in $b.Keys) { if (-not $a.ContainsKey($k)) { Write-Host "EXTRA    Assets/$f/$k"; $bad++ } }
}
Get-ChildItem -LiteralPath $srcAssets -Recurse -File -Filter *.meta -Force | ForEach-Object {
  $rel = $_.FullName.Substring($srcAssets.Length + 1)
  $other = Join-Path $dstAssets $rel
  $count++
  if (-not (Test-Path -LiteralPath $other)) { Write-Host "MISSING  Assets/$rel"; $bad++ }
  elseif ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $other -Algorithm SHA256).Hash) { Write-Host "DIFFERS  Assets/$rel"; $bad++ }
}
if ($bad -gt 0) { Write-Host "sync_verify: $bad problem(s) in $count files -> $Dest"; exit 1 }
Write-Host "sync_verify: OK, $count files identical -> $Dest"
exit 0
