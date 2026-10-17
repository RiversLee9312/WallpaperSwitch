param(
    [string]$AppIconSource = (Join-Path $PSScriptRoot '..\Assets\AppIcon-source.png')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing.Common

$assetsDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Assets'))

function Get-AlphaBounds([System.Drawing.Bitmap] $bitmap) {
    $fullBounds = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
    $bitmapData = $bitmap.LockBits(
        $fullBounds,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    try {
        $rowLength = [Math]::Abs($bitmapData.Stride)
        $pixels = [byte[]]::new($rowLength * $bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy(
            $bitmapData.Scan0,
            $pixels,
            0,
            $pixels.Length)

        $minX = $bitmap.Width
        $minY = $bitmap.Height
        $maxX = -1
        $maxY = -1

        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            $rowOffset = if ($bitmapData.Stride -ge 0) {
                $y * $rowLength
            }
            else {
                ($bitmap.Height - 1 - $y) * $rowLength
            }

            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                if ($pixels[$rowOffset + ($x * 4) + 3] -gt 8) {
                    $minX = [Math]::Min($minX, $x)
                    $minY = [Math]::Min($minY, $y)
                    $maxX = [Math]::Max($maxX, $x)
                    $maxY = [Math]::Max($maxY, $y)
                }
            }
        }

        if ($maxX -lt $minX -or $maxY -lt $minY) {
            throw "No visible pixels found in the source icon."
        }

        return [System.Drawing.Rectangle]::FromLTRB($minX, $minY, $maxX + 1, $maxY + 1)
    }
    finally {
        $bitmap.UnlockBits($bitmapData)
    }
}

function New-ResizedPngBytes {
    param(
        [System.Drawing.Image]$Source,
        [int]$Size,
        [double]$VisualExtentRatio = 1.0
    )

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $stream = [System.IO.MemoryStream]::new()

    $sourceBounds = [System.Drawing.Rectangle]::new(0, 0, $Source.Width, $Source.Height)
    $destinationBounds = [System.Drawing.Rectangle]::new(0, 0, $Size, $Size)
    if ($VisualExtentRatio -lt 1.0) {
        $sourceBounds = Get-AlphaBounds ([System.Drawing.Bitmap]$Source)
        $targetExtent = $Size * $VisualExtentRatio
        $scale = $targetExtent / [Math]::Max($sourceBounds.Width, $sourceBounds.Height)
        $drawWidth = [Math]::Round($sourceBounds.Width * $scale)
        $drawHeight = [Math]::Round($sourceBounds.Height * $scale)
        $drawX = [Math]::Round(($Size - $drawWidth) / 2)
        $drawY = [Math]::Round(($Size - $drawHeight) / 2)
        $destinationBounds = [System.Drawing.Rectangle]::new($drawX, $drawY, $drawWidth, $drawHeight)
    }

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.DrawImage(
            $Source,
            $destinationBounds,
            $sourceBounds.X,
            $sourceBounds.Y,
            $sourceBounds.Width,
            $sourceBounds.Height,
            [System.Drawing.GraphicsUnit]::Pixel)
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally {
        $stream.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Write-MultiSizeIcon {
    param(
        [System.Drawing.Image]$Source,
        [string]$OutputPath,
        [int[]]$Sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    )

    $images = @(
        foreach ($size in $Sizes) {
            [pscustomobject]@{
                Size = $size
                Data = [byte[]](New-ResizedPngBytes -Source $Source -Size $size)
            }
        }
    )

    $stream = [System.IO.File]::Create($OutputPath)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$images.Count)

        $offset = 6 + (16 * $images.Count)
        foreach ($image in $images) {
            $dimension = if ($image.Size -eq 256) { [byte]0 } else { [byte]$image.Size }
            $writer.Write($dimension)
            $writer.Write($dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$image.Data.Length)
            $writer.Write([uint32]$offset)
            $offset += $image.Data.Length
        }

        foreach ($image in $images) {
            $writer.Write([byte[]]$image.Data)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Export-IconAsset {
    param(
        [string]$SourcePath,
        [string]$Name,
        [int]$PngSize,
        [double]$VisualExtentRatio = 1.0
    )

    $resolvedSource = [System.IO.Path]::GetFullPath($SourcePath)
    $pngPath = Join-Path $assetsDirectory ($Name + '.png')
    $icoPath = Join-Path $assetsDirectory ($Name + '.ico')
    $sourceImage = [System.Drawing.Bitmap]::FromFile($resolvedSource)
    try {
        $pngBytes = [byte[]](New-ResizedPngBytes -Source $sourceImage -Size $PngSize -VisualExtentRatio $VisualExtentRatio)
    }
    finally {
        $sourceImage.Dispose()
    }

    [System.IO.File]::WriteAllBytes($pngPath, $pngBytes)
    $pngStream = [System.IO.MemoryStream]::new($pngBytes)
    $pngImage = [System.Drawing.Bitmap]::FromStream($pngStream)
    try {
        Write-MultiSizeIcon -Source $pngImage -OutputPath $icoPath
    }
    finally {
        $pngImage.Dispose()
        $pngStream.Dispose()
    }

    Write-Output "Generated $pngPath"
    Write-Output "Generated $icoPath"
}

Export-IconAsset -SourcePath $AppIconSource -Name 'AppIcon' -PngSize 1024

foreach ($trayDefinition in @(
    [pscustomobject]@{ Name = 'TrayDawn'; Source = 'TrayDawn-source.png' },
    [pscustomobject]@{ Name = 'TrayDay'; Source = 'TrayDay-source.png' },
    [pscustomobject]@{ Name = 'TrayDusk'; Source = 'TrayDusk-source.png' },
    [pscustomobject]@{ Name = 'TrayNight'; Source = 'TrayNight-source.png' })) {
    Export-IconAsset `
        -SourcePath (Join-Path $assetsDirectory ('TraySources\' + $trayDefinition.Source)) `
        -Name $trayDefinition.Name `
        -PngSize 256 `
        -VisualExtentRatio 0.9375
}
