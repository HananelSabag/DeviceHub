# Draws the DeviceHub icon (a hub ring with three linked devices on arena navy) and writes app.ico with 256/48/32/16 px images.
param([string]$Out = (Join-Path $PSScriptRoot '..\src\DeviceHub\app.ico'))
Add-Type -AssemblyName System.Drawing

function New-Frame([int]$s) {
  $bmp = New-Object Drawing.Bitmap $s, $s
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.Clear([Drawing.Color]::Transparent)

  $r = [Math]::Max(2, [int]($s * 0.22))
  $path = New-Object Drawing.Drawing2D.GraphicsPath
  $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90); $path.AddArc($s - $r * 2 - 1, 0, $r * 2, $r * 2, 270, 90)
  $path.AddArc($s - $r * 2 - 1, $s - $r * 2 - 1, $r * 2, $r * 2, 0, 90); $path.AddArc(0, $s - $r * 2 - 1, $r * 2, $r * 2, 90, 90)
  $path.CloseFigure()
  $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(11, 24, 56))), $path)

  $w = [single]($s * 0.11)
  $m = [single]($s * 0.24)
  $box = New-Object Drawing.RectangleF $m, $m, ($s - 2 * $m), ($s - 2 * $m)
  $grad = New-Object Drawing.Drawing2D.LinearGradientBrush $box, ([Drawing.Color]::FromArgb(61, 139, 255)), ([Drawing.Color]::FromArgb(67, 215, 154)), 45
  $pen = New-Object Drawing.Pen $grad, $w
  $g.DrawEllipse($pen, $box)

  # Three devices sit on the ring, the hub in the middle.
  $c = $s / 2; $rad = ($s - 2 * $m) / 2; $node = [single]($s * 0.17)
  foreach ($deg in -90, 30, 150) {
    $a = $deg * [Math]::PI / 180
    $x = $c + $rad * [Math]::Cos($a) - $node / 2; $y = $c + $rad * [Math]::Sin($a) - $node / 2
    $g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(238, 243, 255))), [single]$x, [single]$y, $node, $node)
  }
  $dot = [single]($s * 0.18)
  $g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 139, 43))), ($s - $dot) / 2, ($s - $dot) / 2, $dot, $dot)
  $g.Dispose()

  $ms = New-Object IO.MemoryStream
  $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return , $ms.ToArray()
}

$sizes = 256, 48, 32, 16
$frames = $sizes | ForEach-Object { , (New-Frame $_) }
$fs = [IO.File]::Create($Out)
$bw = New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
  $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([uint16]1); $bw.Write([uint16]32)
  $bw.Write([uint32]$frames[$i].Length); $bw.Write([uint32]$offset)
  $offset += $frames[$i].Length
}
foreach ($f in $frames) { $bw.Write($f) }
$bw.Dispose()
"wrote $Out"
