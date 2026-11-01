# Draws the small skill props that no effect pack has (fishbone arrow, yarn ball, can, churu,
# mouse trap, "!" mark, heart, bell, eye glint) into Assets/Effects/Custom as 16x16 pixel-art strips.
# Run from the project root:  powershell -ExecutionPolicy Bypass -File generate_skill_sprites.ps1
# Then Unity: Cats Smash > Apply Effects (VfxAssets slices them like the DryRain strips).
Add-Type -AssemblyName System.Drawing

$out = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../..')) 'Assets\Effects\Custom'
New-Item -ItemType Directory -Force $out | Out-Null

# Case-sensitive ('B' brown vs 'b' dark brown), so not a plain @{} hashtable.
$palette = New-Object 'System.Collections.Generic.Dictionary[string,int[]]'
foreach ($e in @(
    @('K', 40, 24, 30), @('W', 255, 255, 255), @('R', 230, 70, 90), @('P', 255, 160, 180),
    @('Y', 255, 214, 90), @('O', 240, 140, 40), @('B', 140, 80, 40), @('b', 100, 55, 30),
    @('G', 170, 175, 185), @('g', 110, 115, 130), @('S', 225, 230, 240), @('C', 120, 220, 255),
    @('L', 150, 230, 90), @('D', 60, 140, 60), @('M', 235, 95, 165), @('m', 165, 50, 120))) {
    $palette[$e[0]] = [int[]]@($e[1], $e[2], $e[3])
}

# Each sprite: one or more 16x16 frames, rows of palette letters ('.' = transparent).
function Save-Strip([string]$name, [object[]]$frames) {
    $bmp = New-Object Drawing.Bitmap (16 * $frames.Count), 16
    for ($f = 0; $f -lt $frames.Count; $f++) {
        $rows = $frames[$f]
        if ($rows.Count -gt 16) { throw "$name frame $f has $($rows.Count) rows" }
        for ($y = 0; $y -lt $rows.Count; $y++) {
            $row = $rows[$y]
            if ($row.Length -gt 16) { throw "$name frame $f row $y is $($row.Length) wide" }
            for ($x = 0; $x -lt $row.Length; $x++) {
                $c = [string]$row[$x]
                if ($c -eq '.') { continue }
                if (-not $palette.ContainsKey($c)) { throw "${name}: unknown colour '$c'" }
                $rgb = $palette[$c]
                $bmp.SetPixel($f * 16 + $x, $y, [Drawing.Color]::FromArgb(255, $rgb[0], $rgb[1], $rgb[2]))
            }
        }
    }
    $bmp.Save((Join-Path $out "$name.png"), [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "  $name.png ($($frames.Count) frame(s))"
}

Save-Strip 'heart' @(, @(
    '', '', '', '',
    '...KK...KK',
    '..KPRK.KRRK',
    '.KPWRRKRRRRK',
    '.KPRRRRRRRRK',
    '.KRRRRRRRRRK',
    '..KRRRRRRRK',
    '...KRRRRRK',
    '....KRRRK',
    '.....KRK',
    '......K'))

Save-Strip 'exclaim' @(, @(
    '......KKKK',
    '.....KYYYYK',
    '.....KYOOYK',
    '.....KYOOYK',
    '.....KYOOYK',
    '.....KYOOYK',
    '......KOOK',
    '......KOOK',
    '......KOOK',
    '.......KK',
    '',
    '......KKKK',
    '.....KYOOYK',
    '.....KYOOYK',
    '......KKKK'))

# Points right (the arrow code rotates it toward the aim).
Save-Strip 'fishbone' @(, @(
    '', '', '', '', '',
    '.K....g.g.g.gKK',
    'KSK..S.S.S.SSSSK',
    '.KSSSSSSSSSSSKSK',
    'KSK..S.S.S.SSSSK',
    '.K....g.g.g.gKK'))

Save-Strip 'yarn' @(, @(
    '', '', '',
    '.....KKKKK',
    '....KMMMMMKK',
    '...KMMmMMMMMK',
    '..KMmMMMmMMMMK',
    '..KMMMMmMMmMMK',
    '..KMmMMMMmMMMK',
    '..KMMmMMMMMmMK',
    '..KMMMMmMMMMMK',
    '...KMMMMMmMMK',
    '....KMMMMMKK',
    '.....KKKKK..m',
    '.............m'))

# A tin can lying on its side, lid to the right.
Save-Strip 'can' @(, @(
    '', '', '',
    '...KKKKKKKKKK',
    '..KGSSSSSSSSGK',
    '..KSOOOOOOOOSKK',
    '..KSOYYYYYYOSKGK',
    '..KSOYRRRRYOSKGK',
    '..KSOYYYYYYOSKGK',
    '..KSOOOOOOOOSKK',
    '..KGSSSSSSSSGK',
    '...KKKKKKKKKK'))

Save-Strip 'churu' @(, @(
    '',
    '.............K',
    '............KYK',
    '...........KYOK',
    '..........KYOK',
    '.........KYOK',
    '........KWOK',
    '.......KWOK',
    '......KYOK',
    '.....KYOK',
    '....KYOK',
    '...KYOK',
    '..KOOK',
    '..KKK'))

# Sticky mouse trap: wooden board, steel bar, a dab of yellow goo.
Save-Strip 'trap' @(, @(
    '', '', '', '', '', '', '',
    '.....GGGGGG',
    '....G......G',
    '....G..YY..G',
    '..KKKKKKKKKKKK',
    '..KBOOYOOYOOBK',
    '..KbBBBBBBBBbK',
    '..KKKKKKKKKKKK'))

Save-Strip 'bell' @(, @(
    '', '', '', '',
    '.......KK',
    '......KYYK',
    '.....KYWYYK',
    '.....KYYYYK',
    '....KYYYYYYK',
    '....KOOOOOOK',
    '.....KKOOKK',
    '.......KK'))

# Two-frame twinkle for the night-vision eyes.
Save-Strip 'glint' (
    @('', '', '', '',
      '.......W',
      '.......W',
      '......WYW',
      '....WWYWYWW',
      '......WYW',
      '.......W',
      '.......W'),
    @('', '', '', '', '', '',
      '.......W',
      '......WYW',
      '.......W'))
