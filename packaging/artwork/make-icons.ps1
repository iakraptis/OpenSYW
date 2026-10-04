# Draws the OpenSYW application icon (icon_<size>x<size>.png, used for the Windows launcher and installer) in the style
# of the main menu logo: gold lettering with a dark outline on a dark brown tile. Small sizes show only "S", as three
# letters would not be readable there.
#   powershell -ExecutionPolicy Bypass -File packaging\artwork\make-icons.ps1
Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256, 512, 1024
$fontName = 'Trebuchet MS'

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
	$path = New-Object System.Drawing.Drawing2D.GraphicsPath
	$d = 2 * $r
	$path.AddArc($x, $y, $d, $d, 180, 90)
	$path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
	$path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
	$path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
	$path.CloseFigure()
	return $path
}

foreach ($size in $sizes) {
	$bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$g = [System.Drawing.Graphics]::FromImage($bitmap)
	$g.SmoothingMode = 'AntiAlias'
	$g.TextRenderingHint = 'AntiAliasGridFit'
	$g.InterpolationMode = 'HighQualityBicubic'
	$g.Clear([System.Drawing.Color]::Transparent)

	# Tile: dark brown gradient with a thin gold rim.
	$inset = [Math]::Max(0.5, $size * 0.03)
	$tile = New-RoundedRect $inset $inset ($size - 2 * $inset) ($size - 2 * $inset) ($size * 0.18)
	$tileBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
		(New-Object System.Drawing.PointF(0, 0)), (New-Object System.Drawing.PointF(0, $size)),
		[System.Drawing.Color]::FromArgb(255, 74, 52, 30), [System.Drawing.Color]::FromArgb(255, 28, 19, 11))
	$g.FillPath($tileBrush, $tile)
	$rim = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 201, 146, 46), [Math]::Max(1, $size * 0.025))
	$g.DrawPath($rim, $tile)

	# Lettering as a path, so it can be outlined and filled with a gradient.
	$text = if ($size -le 32) { 'S' } else { 'SYW' }
	$emSize = if ($text -eq 'S') { $size * 0.78 } else { $size * 0.40 }
	$family = New-Object System.Drawing.FontFamily($fontName)
	$format = New-Object System.Drawing.StringFormat
	$format.Alignment = 'Center'
	$format.LineAlignment = 'Center'
	$glyphs = New-Object System.Drawing.Drawing2D.GraphicsPath
	$glyphs.AddString($text, $family, [int][System.Drawing.FontStyle]::Bold, $emSize,
		(New-Object System.Drawing.RectangleF(0, 0, $size, $size)), $format)

	# Centre the glyphs exactly (font metrics leave uneven space above and below).
	$bounds = $glyphs.GetBounds()
	$matrix = New-Object System.Drawing.Drawing2D.Matrix
	$matrix.Translate(($size - $bounds.Width) / 2 - $bounds.X, ($size - $bounds.Height) / 2 - $bounds.Y)
	$glyphs.Transform($matrix)
	$bounds = $glyphs.GetBounds()

	$outline = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 20, 12, 6), [Math]::Max(1, $size * 0.06))
	$outline.LineJoin = 'Round'
	$g.DrawPath($outline, $glyphs)

	$gold = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
		[System.Drawing.PointF]::new(0, $bounds.Top), [System.Drawing.PointF]::new(0, $bounds.Bottom + 1),
		[System.Drawing.Color]::FromArgb(255, 255, 236, 160), [System.Drawing.Color]::FromArgb(255, 186, 104, 18))
	$blend = New-Object System.Drawing.Drawing2D.ColorBlend(3)
	$blend.Colors = @([System.Drawing.Color]::FromArgb(255, 255, 238, 170), [System.Drawing.Color]::FromArgb(255, 240, 176, 56),
		[System.Drawing.Color]::FromArgb(255, 170, 92, 16))
	$blend.Positions = @(0.0, 0.5, 1.0)
	$gold.InterpolationColors = $blend
	$g.FillPath($gold, $glyphs)

	$file = Join-Path $PSScriptRoot "icon_${size}x${size}.png"
	$bitmap.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
	$g.Dispose()
	$bitmap.Dispose()
	Write-Output "wrote $file"
}
