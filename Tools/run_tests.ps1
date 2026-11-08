# Runs every automated check of Cats Smash in batch mode: EditMode tests, PlayMode tests, the full-run autoplay
# and the screen captures. Close the Unity editor first (a project can be open only once), or point -Project at the
# verification copy (Tools/sync_verify.ps1 makes it).
#   powershell -File Tools/run_tests.ps1 [-Project %TEMP%\cs_verify] [-Only editmode,fullrun] [-Unity <Unity.exe>]
# Each run quits by itself (test runner / EditorApplication.Exit; -quit for synchronous methods) within a time limit; a run that hangs is asked to close and killed only as a last
# resort. Library repairs (PackageCache check, cleanup after a kill) are in UnityBatch.ps1 and logged to
# TestResults\unity_batch.log.
param(
  [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe",
  [string]$Project = (Join-Path $PSScriptRoot '..'),
  [string[]]$Only = @()
)
. (Join-Path $PSScriptRoot 'UnityBatch.ps1')
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ }) # -File passes "a,b" as one string
$project = (Resolve-Path -LiteralPath $Project).ProviderPath
$out = Join-Path $project 'TestResults'
New-Item -ItemType Directory -Force $out | Out-Null
$runs = [ordered]@{
  editmode = @{ Args = @('-runTests', '-testPlatform', 'EditMode', '-testResults', "`"$out\editmode.xml`""); Clear = "$out\editmode.xml" }
  playmode = @{ Args = @('-runTests', '-testPlatform', 'PlayMode', '-testResults', "`"$out\playmode.xml`""); Clear = "$out\playmode.xml" }
  fullrun  = @{ Args = @('-executeMethod', 'FullRunPlaytest.Run', '-fullRunPlaytest', '-playtestOut', "`"$out\fullrun`""); Done = "$out\fullrun\complete.txt"; Clear = "$out\fullrun" }
  screens  = @{ Args = @('-executeMethod', 'ScreenCaptureTool.RunBatch') }
}
foreach ($name in $runs.Keys) {
  if ($Only.Count -and $Only -notcontains $name) { continue }
  $r = $runs[$name]
  if ($r.Clear) { Remove-Item -LiteralPath $r.Clear -Recurse -Force -ErrorAction SilentlyContinue }
  Write-Host "== $name"
  [void](Invoke-UnityBatch -Unity $Unity -Project $project -Name $name -UnityArgs $r.Args -DoneFile $r.Done -Minutes 20)
}
foreach ($x in 'editmode', 'playmode') {
  $xml = Join-Path $out "$x.xml"
  if (Test-Path $xml) { $r = ([xml](Get-Content $xml)).'test-run'; Write-Host "$x : $($r.result)  passed $($r.passed) / $($r.total)" }
}
# The result board as a real run left it (the full run's own capture at its end), beside the staged one.
$victory = Join-Path $out 'fullrun\victory.png'
if (Test-Path $victory) { New-Item -ItemType Directory -Force (Join-Path $project 'Screenshots') | Out-Null; Copy-Item $victory (Join-Path $project 'Screenshots\result_run.png') -Force }
$report = Join-Path $out 'fullrun\report.json'
if (Test-Path $report) { Write-Host "full run: $((Get-Content $report -Raw | ConvertFrom-Json).victoryScreen)" }
Write-Host "screens: $project\Screenshots"
