Add-Type -AssemblyName System.Drawing
$yutaBitmap = New-Object System.Drawing.Bitmap 64,64
$yutaGraphics = [System.Drawing.Graphics]::FromImage($yutaBitmap)
$yutaGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$yutaGraphics.Clear([System.Drawing.Color]::FromArgb(16,18,25))
$yutaBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(194,182,236))
$yutaFont = New-Object System.Drawing.Font 'Segoe UI',42,([System.Drawing.FontStyle]::Bold),([System.Drawing.GraphicsUnit]::Pixel)
$yutaGraphics.DrawString('y',$yutaFont,$yutaBrush,11,0)
$yutaGraphics.FillEllipse($yutaBrush,47,42,6,6)
$yutaIcon = [System.Drawing.Icon]::FromHandle($yutaBitmap.GetHicon())
$yutaIconStream = [System.IO.File]::Create((Join-Path $PSScriptRoot '..\ui\assets\yuta.ico'))
$yutaIcon.Save($yutaIconStream)
$yutaIconStream.Dispose()
$yutaIcon.Dispose()
$yutaFont.Dispose()
$yutaBrush.Dispose()
$yutaGraphics.Dispose()
$yutaBitmap.Dispose()
