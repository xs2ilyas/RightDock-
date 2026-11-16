param(
    [string]$Output = (Join-Path (Split-Path $PSScriptRoot -Parent) "Resources\RightDock.ico")
)

Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$master = New-Object System.Drawing.Bitmap 256, 256
$graphics = [System.Drawing.Graphics]::FromImage($master)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.Clear([System.Drawing.Color]::Transparent)

function New-RoundedPath([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$bodyPath = New-RoundedPath 20 20 216 216 48
$bodyBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 28, 28, 28))
$graphics.FillPath($bodyBrush, $bodyPath)

$borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(90, 255, 255, 255)), 3
$graphics.DrawPath($borderPen, $bodyPath)

$dockPath = New-RoundedPath 168 44 44 168 14
$dockBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 46, 46, 46))
$graphics.FillPath($dockBrush, $dockPath)

$dockPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(80, 255, 255, 255)), 2
$graphics.DrawPath($dockPen, $dockPath)

$iconBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 235, 235, 235))
foreach ($top in 64, 104, 144, 184) {
    $graphics.FillEllipse($iconBrush, 181, $top, 18, 10)
}

$accentBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 92, 176, 242))
$graphics.FillEllipse($accentBrush, 181, 184, 18, 10)

$graphics.Dispose()

$images = @()
foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($master, 0, 0, $size, $size)
    $g.Dispose()

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    $images += [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose()
}

$master.Dispose()

$folder = Split-Path $Output -Parent
if (-not (Test-Path $folder)) { New-Item -ItemType Directory -Path $folder | Out-Null }
if (Test-Path $Output) { Remove-Item $Output -Force }

$file = [System.IO.File]::Create($Output)
$writer = New-Object System.IO.BinaryWriter $file

$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$images.Count)

$offset = 6 + (16 * $images.Count)
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([Byte]$dimension)
    $writer.Write([Byte]$dimension)
    $writer.Write([Byte]0)
    $writer.Write([Byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$image.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $image.Bytes.Length
}

foreach ($image in $images) {
    $writer.Write($image.Bytes)
}

$writer.Dispose()
$file.Dispose()

Write-Output "Wrote $Output ($($images.Count) sizes)"