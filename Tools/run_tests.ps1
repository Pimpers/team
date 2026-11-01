# Runs every automated check of Cats Smash in batch mode: EditMode tests, PlayMode tests, the full-run autoplay
# and the screen captures. Close the Unity editor first (a project can be open only once).
#   powershell -File Tools/run_tests.ps1 [-Unity "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe"]
param([string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe")
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out = Join-Path $project 'TestResults'
New-Item -ItemType Directory -Force $out | Out-Null
function Run($name, $extra) {
  Write-Host "== $name"
  $p = Start-Process $Unity -ArgumentList (@('-batchmode', '-projectPath', "`"$project`"", '-logFile', "`"$out\$name.log`"") + $extra) -PassThru -Wait
  Write-Host "   exit $($p.ExitCode)"
}
Run 'editmode' @('-runTests', '-testPlatform', 'EditMode', '-testResults', "`"$out\editmode.xml`"")
Run 'playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testResults', "`"$out\playmode.xml`"")
Run 'fullrun' @('-executeMethod', 'FullRunPlaytest.Run', '-fullRunPlaytest', '-playtestOut', "`"$out\fullrun`"")
Run 'screens' @('-executeMethod', 'ScreenCaptureTool.RunBatch')
foreach ($x in 'editmode', 'playmode') {
  $xml = Join-Path $out "$x.xml"
  if (Test-Path $xml) { $r = ([xml](Get-Content $xml)).'test-run'; Write-Host "$x : $($r.result)  passed $($r.passed) / $($r.total)" }
}
$report = Join-Path $out 'fullrun\report.json'
if (Test-Path $report) { Write-Host "full run: $((Get-Content $report -Raw | ConvertFrom-Json).victoryScreen)" }
Write-Host "screens: $project\Screenshots"
