# Runs unity-kit:precommit's compile and test checks for this project. The skill's scripts take the project from
# CLAUDE_PROJECT_DIR and stop at the space in its path ("2학년 2학기"): this syncs the verification copy (%TEMP%\cs_verify,
# no spaces; Tools/sync_verify.ps1) and runs them there, so they report "checked" instead of "not a Unity project
# root". The skill's files are not touched. Exit code: the highest of the scripts' (0 = all passed).
#   powershell -ExecutionPolicy Bypass -File Tools/precommit.ps1 [-Checks compile,test] [-Kit <unity-kit scripts dir>]
param(
  [string[]]$Checks = @('compile', 'test'),
  [string]$Kit = (Join-Path $env:USERPROFILE '.claude\plugins\cache\akaashg-unity\unity-kit\0.2.0\scripts'),
  [string]$State = (Join-Path $env:USERPROFILE '.claude\plugins\data\unity-kit-akaashg-unity'),
  [string]$Copy = (Join-Path $env:TEMP 'cs_verify')
)
$Checks = @($Checks | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) # -File passes "a,b" as one string
$sh = Join-Path $env:ProgramFiles 'Git\bin\sh.exe'
if (-not (Test-Path $sh)) { Write-Host "precommit: Git for Windows' sh.exe not found at $sh"; exit 3 }
if ($Copy -match ' ') { Write-Host "precommit: the copy's path has a space too: $Copy"; exit 3 }
& powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'sync_verify.ps1') -Dest $Copy | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { Write-Host 'precommit: the copy is not the same code - not checked'; exit 3 }
# Assets that only the copy has (outside the code folders sync_verify mirrors) - the art too, so tests see it.
robocopy (Join-Path $PSScriptRoot '..\Assets') (Join-Path $Copy 'Assets') /MIR /XD (Join-Path $PSScriptRoot '..\Assets\Plugins') /NJH /NJS /NP /NFL /NDL | Out-Null
$env:CLAUDE_PROJECT_DIR = $Copy
$worst = 0
Push-Location $Copy
try {
  foreach ($c in $Checks) {
    $script = switch ($c) { 'compile' { 'compile-check.sh' } 'test' { 'test-check.sh' } default { $null } }
    if (-not $script) { Write-Host "precommit: unknown check '$c'"; continue }
    $args2 = @((Join-Path $Kit $script).Replace('\', '/'))
    # the copy has no editor open: compile in batch mode (the script allows it only when asked)
    if ($c -eq 'compile') { $args2 += @('--batch', '--state', $State.Replace('\', '/')) }
    Write-Host "== $c ($script on $Copy)"
    & $sh @args2
    $code = $LASTEXITCODE
    Write-Host "== $c exit $code"
    if ($code -gt $worst) { $worst = $code }
  }
} finally { Pop-Location }
exit $worst
