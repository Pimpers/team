# Cuts the blacksmith's speech bubble out of icelivin's "Pixel Speech Bubbles" sheet (64 x 64, recoloured: rim
# #513320, fill #e9d6ae) into Assets/Resources/UIBook:
#   SpeechBoard.png - the big bubble (x 1-62, y 1-43) with its tail gaps closed: a 9-sliced board, 2 px border
#   SpeechTail.png  - its left tail (x 7-11, y 43-47, 5 x 5); its top row is fill where it opens into the board
# Usage: powershell -File Tools/bubble_from_icelivin.ps1 <path to speech_recolor.png>
param([Parameter(Mandatory)] [string] $Source)
Add-Type -AssemblyName System.Drawing
$src = [Drawing.Bitmap]::new($Source)
$out = Join-Path $PSScriptRoot '..\Assets\Resources\UIBook'

function Crop($x, $y, $w, $h) {
    $b = [Drawing.Bitmap]::new($w, $h, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($j = 0; $j -lt $h; $j++) { for ($i = 0; $i -lt $w; $i++) { $b.SetPixel($i, $j, $src.GetPixel($x + $i, $y + $j)) } }
    return $b
}

$rim = $src.GetPixel(1, 1)
$board = Crop 1 1 62 43
for ($i = 0; $i -lt 62; $i++) { $board.SetPixel($i, 42, $rim) } # close the three tail openings in the bottom rim
$board.Save((Join-Path $out 'SpeechBoard.png'), [Drawing.Imaging.ImageFormat]::Png)

$tail = Crop 7 43 5 5
$tail.Save((Join-Path $out 'SpeechTail.png'), [Drawing.Imaging.ImageFormat]::Png)

$board.Dispose(); $tail.Dispose(); $src.Dispose()
Write-Host "SpeechBoard.png 62x43, SpeechTail.png 5x5 -> $out"
