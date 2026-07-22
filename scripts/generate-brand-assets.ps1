[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\src\LightPilot.App\Assets')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null

function New-HorizonBitmap {
    param(
        [int]$Size,
        [ValidateSet('Active', 'Paused', 'Degraded', 'Error')]
        [string]$State = 'Active'
    )

    $bitmap = [Drawing.Bitmap]::new($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.SmoothingMode = if ($Size -le 24) {
        [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    } else {
        [Drawing.Drawing2D.SmoothingMode]::HighQuality
    }

    $scale = $Size / 64.0
    $graphics.ScaleTransform($scale, $scale)
    $graphite = [Drawing.ColorTranslator]::FromHtml('#18211F')
    $teal = switch ($State) {
        'Paused' { [Drawing.ColorTranslator]::FromHtml('#7B8581') }
        'Degraded' { [Drawing.ColorTranslator]::FromHtml('#D7A24B') }
        'Error' { [Drawing.ColorTranslator]::FromHtml('#B94B4B') }
        default { [Drawing.ColorTranslator]::FromHtml('#1A7478') }
    }
    $amber = if ($State -eq 'Active') {
        [Drawing.ColorTranslator]::FromHtml('#D7A24B')
    } else {
        $teal
    }

    if ($Size -le 24) {
        $thickness = if ($Size -le 16) { 7.0 } else { 6.0 }
        foreach ($line in @(
            @{ Y = 18.0; Color = $graphite },
            @{ Y = 32.0; Color = $teal },
            @{ Y = 46.0; Color = $amber }
        )) {
            $pen = [Drawing.Pen]::new($line.Color, $thickness)
            $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
            $graphics.DrawLine($pen, 10, $line.Y, 54, $line.Y)
            $pen.Dispose()
        }
    } else {
        $top = [Drawing.Drawing2D.GraphicsPath]::new()
        $top.AddBezier(6, 10, 14, 21, 23, 26.4, 32, 28)
        $top.AddBezier(32, 28, 41, 26.4, 50, 21, 58, 10)
        $top.CloseFigure()

        $middle = [Drawing.Drawing2D.GraphicsPath]::new()
        $middle.AddBezier(6, 27, 15, 30.6, 23.5, 32.4, 32, 32.4)
        $middle.AddBezier(32, 32.4, 40.5, 32.4, 49, 30.6, 58, 27)
        $middle.AddLine(58, 37, 58, 37)
        $middle.AddBezier(58, 37, 49, 33.4, 40.5, 31.6, 32, 31.6)
        $middle.AddBezier(32, 31.6, 23.5, 31.6, 15, 33.4, 6, 37)
        $middle.CloseFigure()

        $bottom = [Drawing.Drawing2D.GraphicsPath]::new()
        $bottom.AddBezier(6, 54, 14, 43, 23, 37.6, 32, 36)
        $bottom.AddBezier(32, 36, 41, 37.6, 50, 43, 58, 54)
        $bottom.CloseFigure()

        foreach ($shape in @(
            @{ Path = $top; Color = $graphite },
            @{ Path = $middle; Color = $teal },
            @{ Path = $bottom; Color = $amber }
        )) {
            $brush = [Drawing.SolidBrush]::new($shape.Color)
            $graphics.FillPath($brush, $shape.Path)
            $brush.Dispose()
            $shape.Path.Dispose()
        }
    }

    $graphics.Dispose()
    return $bitmap
}

function Write-Ico {
    param([string]$Path, [object[]]$Images)

    $streams = foreach ($image in $Images) {
        $stream = [IO.MemoryStream]::new()
        $image.Bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        [pscustomobject]@{ Size = $image.Size; Bytes = $stream.ToArray(); Stream = $stream }
    }

    $file = [IO.File]::Create($Path)
    $writer = [IO.BinaryWriter]::new($file)
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$streams.Count)
    $offset = 6 + (16 * $streams.Count)
    foreach ($stream in $streams) {
        $writer.Write([byte]$(if ($stream.Size -eq 256) { 0 } else { $stream.Size }))
        $writer.Write([byte]$(if ($stream.Size -eq 256) { 0 } else { $stream.Size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$stream.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $stream.Bytes.Length
    }
    foreach ($stream in $streams) {
        $writer.Write($stream.Bytes)
        $stream.Stream.Dispose()
    }
    $writer.Dispose()
}

$sizes = 16, 20, 24, 32, 48, 64, 128, 256
$states = 'Active', 'Paused', 'Degraded', 'Error'
foreach ($state in $states) {
    $images = foreach ($size in $sizes) {
        $bitmap = New-HorizonBitmap -Size $size -State $state
        $name = if ($state -eq 'Active') { "Aptema-$size.png" } else { "Aptema-$($state.ToLowerInvariant())-$size.png" }
        $bitmap.Save((Join-Path $output $name), [Drawing.Imaging.ImageFormat]::Png)
        [pscustomobject]@{ Size = $size; Bitmap = $bitmap }
    }

    $icoName = if ($state -eq 'Active') { 'Aptema.ico' } else { "Aptema-$($state.ToLowerInvariant()).ico" }
    Write-Ico -Path (Join-Path $output $icoName) -Images $images
    $images | ForEach-Object { $_.Bitmap.Dispose() }
}

Write-Host "Aptema brand assets generated in $output"
