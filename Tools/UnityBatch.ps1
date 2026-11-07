# Shared by run_tests.ps1 (dot-sourced): one Unity batch run that cleans up after itself and repairs the Library.
#   Invoke-UnityBatch -Unity <exe> -Project <dir> -Name editmode -UnityArgs @(...) [-DoneFile <path>] [-Minutes 20]
# Every decision goes to <Project>\TestResults\unity_batch.log with a time stamp.
# Why: batch runs hung on quit, a force-killed Unity left half-written ScriptAssemblies, and PackageCache lost its
# .meta files (2d.common / UGUI compile errors) - each cost a full re-run before anyone noticed.

function Write-BatchLog([string]$Project, [string]$Text) {
  $dir = Join-Path $Project 'TestResults'; New-Item -ItemType Directory -Force $dir | Out-Null
  $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Text
  Add-Content -LiteralPath (Join-Path $dir 'unity_batch.log') -Value $line -Encoding UTF8
  Write-Host $line
}

# Problems in Library/PackageCache: package folders whose top-level entries lack a .meta, or whose package.json
# version is not the one packages-lock.json pins. Returns a list of text lines; empty = healthy (or no cache yet).
function Test-PackageCache([string]$Project) {
  $cache = Join-Path $Project 'Library\PackageCache'
  $lockPath = Join-Path $Project 'Packages\packages-lock.json'
  $bad = @()
  if (-not (Test-Path -LiteralPath $cache)) {
    # no cache but a resolved state: Unity would trust the state and not extract anything
    if (Test-Path -LiteralPath (Join-Path $Project 'Library\PackageManager')) { $bad += 'PackageCache missing, resolved state kept' }
    return $bad
  }
  $lock = $null
  if (Test-Path -LiteralPath $lockPath) { $lock = (Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json).dependencies }
  foreach ($pkg in Get-ChildItem -LiteralPath $cache -Directory) {
    $json = Join-Path $pkg.FullName 'package.json'
    if (-not (Test-Path -LiteralPath $json)) { $bad += "$($pkg.Name): no package.json"; continue }
    $info = Get-Content -LiteralPath $json -Raw | ConvertFrom-Json
    $want = if ($lock) { $lock.($info.name) } else { $null }
    if ($want -and $want.source -eq 'registry' -and $want.version -ne $info.version) {
      $bad += "$($pkg.Name): version $($info.version), lock wants $($want.version)"
    }
    $noMeta = @(Get-ChildItem -LiteralPath $pkg.FullName -Force | Where-Object {
      $_.Name -notlike '*.meta' -and $_.Name -notlike '*~' -and $_.Name -notlike '.*' -and
      -not (Test-Path -LiteralPath "$($_.FullName).meta") })
    if ($noMeta.Count) { $bad += "$($pkg.Name): no .meta for $(($noMeta | Select-Object -First 3 -ExpandProperty Name) -join ', ')" }
  }
  return $bad
}

function Remove-LibraryPart([string]$Project, [string]$Part, [string]$Why) {
  $parts = @($Part)
  # Library\PackageManager holds the "already resolved" state: left alone, Unity restores it ("Restoring resolved
  # packages state from cache"), never extracts the deleted packages again and every UI/TMPro using fails to compile
  if ($Part -eq 'PackageCache') { $parts += 'PackageManager' }
  foreach ($x in $parts) {
    $p = if ($x) { Join-Path $Project "Library\$x" } else { Join-Path $Project 'Library' }
    if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }
  }
  Write-BatchLog $Project "repair: deleted Library\$($parts -join ', Library\') ($Why)"
}

# Before a run: undo what a last-resort kill may have left, then check the package cache.
function Initialize-Library([string]$Project) {
  $mark = Join-Path $Project 'Library\cs_killed.txt'
  if (Test-Path -LiteralPath $mark) {
    $what = (Get-Content -LiteralPath $mark -Raw).Trim()
    foreach ($part in 'ScriptAssemblies', 'Bee') { Remove-LibraryPart $Project $part "the previous run was killed: $what" }
    Remove-Item -LiteralPath $mark -Force -ErrorAction SilentlyContinue
  }
  $bad = @(Test-PackageCache $Project)
  if ($bad.Count) {
    Write-BatchLog $Project "check: PackageCache damaged ($($bad.Count)): $(($bad | Select-Object -First 4) -join ' | ')"
    Remove-LibraryPart $Project 'PackageCache' 'damaged'
  } else { Write-BatchLog $Project 'check: PackageCache healthy' }
}

# Compile errors caused by the packages, not our code: an error inside a package, or errors while fewer packages
# are extracted than the lock pins (then every 'using UnityEngine.UI' / TMPro fails in Assets).
function Test-PackageCompileError([string]$Project, [string]$LogFile) {
  if (-not (Test-Path -LiteralPath $LogFile)) { return $false }
  if (Select-String -LiteralPath $LogFile -Pattern 'PackageCache[\\/].*error CS\d+' -Quiet) { return $true }
  if (-not (Select-String -LiteralPath $LogFile -Pattern 'error CS\d+' -Quiet)) { return $false }
  $lock = (Get-Content -LiteralPath (Join-Path $Project 'Packages\packages-lock.json') -Raw | ConvertFrom-Json).dependencies
  $want = @($lock.PSObject.Properties | Where-Object { $_.Value.source -in 'registry', 'git' }).Count
  $have = @(Get-ChildItem -LiteralPath (Join-Path $Project 'Library\PackageCache') -Directory -ErrorAction SilentlyContinue).Count
  return $have -lt $want
}

function Start-UnityOnce($Unity, $Project, $Name, $UnityArgs, $DoneFile, $Minutes) {
  $log = Join-Path $Project "TestResults\$Name.log"
  $argv = @('-batchmode', '-projectPath', "`"$Project`"", '-logFile', "`"$log`"") + $UnityArgs
  # -quit only for a synchronous -executeMethod: -runTests quits by itself, and methods that enter play mode
  # (FullRunPlaytest, ScreenCaptureTool) call EditorApplication.Exit when done - -quit would end them at once
  $p = Start-Process $Unity -ArgumentList $argv -PassThru
  $deadline = (Get-Date).AddMinutes($Minutes)
  # WaitForExit, not Start-Process -Wait: -Wait also waits for helper processes Unity leaves behind
  while (-not $p.HasExited -and (Get-Date) -lt $deadline) {
    if ($DoneFile -and (Test-Path -LiteralPath $DoneFile)) {
      if (-not $p.WaitForExit(90000)) { break } # work is done; 90 s to quit on its own before calling it a hang
    } else { [void]$p.WaitForExit(5000) }
  }
  if (-not $p.HasExited) {
    $why = if ($DoneFile -and (Test-Path -LiteralPath $DoneFile)) { 'finished but did not quit in 90 s' } else { "no exit after $Minutes min" }
    Write-BatchLog $Project "$Name : $why - asking it to close"
    [void]$p.CloseMainWindow()
    if (-not $p.WaitForExit(30000)) {
      # last resort; the next run clears ScriptAssemblies and Bee, which a killed compile can leave half-written
      Stop-Process -Id $p.Id -Force
      Set-Content -LiteralPath (Join-Path $Project 'Library\cs_killed.txt') -Value "$Name $(Get-Date -Format s)"
      Write-BatchLog $Project "$Name : killed (last resort), marked for cleanup"
      if ($DoneFile -and (Test-Path -LiteralPath $DoneFile)) { return 0 } # its results were already written
      return -1
    }
  }
  return $p.ExitCode
}

function Invoke-UnityBatch {
  param([string]$Unity, [string]$Project, [string]$Name, [string[]]$UnityArgs, [string]$DoneFile, [int]$Minutes = 20)
  $log = Join-Path $Project "TestResults\$Name.log"
  $code = -1
  for ($try = 0; $try -lt 3; $try++) {
    Initialize-Library $Project
    $code = Start-UnityOnce $Unity $Project $Name $UnityArgs $DoneFile $Minutes
    Write-BatchLog $Project "$Name : exit $code (try $($try + 1))"
    if (-not (Test-PackageCompileError $Project $log)) { return $code }
    # escalate: packages do not compile although the cache looked fine - first the cache, then the whole Library
    if ($try -eq 0) { Remove-LibraryPart $Project 'PackageCache' 'package compile errors in the log' }
    else { Remove-LibraryPart $Project '' 'package compile errors again after a fresh PackageCache - full rebuild' }
  }
  Write-BatchLog $Project "$Name : still failing after a full Library rebuild - giving up"
  return $code
}
