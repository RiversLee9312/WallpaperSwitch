$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing.Common

$assetsDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Assets'))
$sourceDirectory = Join-Path $assetsDirectory 'UiIconSources'
$outputDirectory = Join-Path $assetsDirectory 'UiIcons'
$outputSize = 96
$visualExtent = 90
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

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
                $alpha = $pixels[$rowOffset + ($x * 4) + 3]
                if ($alpha -gt 8) {
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

function Remove-SmallAlphaComponents(
    [System.Drawing.Bitmap] $bitmap,
    [int] $keepCount) {
    if ($keepCount -le 0) {
        return
    }

    $width = $bitmap.Width
    $height = $bitmap.Height
    $visited = [bool[]]::new($width * $height)
    $components = [System.Collections.Generic.List[object]]::new()
    $offsets = @(
        [pscustomobject]@{ X = -1; Y = -1 },
        [pscustomobject]@{ X = 0; Y = -1 },
        [pscustomobject]@{ X = 1; Y = -1 },
        [pscustomobject]@{ X = -1; Y = 0 },
        [pscustomobject]@{ X = 1; Y = 0 },
        [pscustomobject]@{ X = -1; Y = 1 },
        [pscustomobject]@{ X = 0; Y = 1 },
        [pscustomobject]@{ X = 1; Y = 1 })

    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $index = ($y * $width) + $x
            if ($visited[$index] -or $bitmap.GetPixel($x, $y).A -le 8) {
                continue
            }

            $queue = [System.Collections.Generic.Queue[int]]::new()
            $pixels = [System.Collections.Generic.List[int]]::new()
            $queue.Enqueue($index)
            $visited[$index] = $true

            while ($queue.Count -gt 0) {
                $current = $queue.Dequeue()
                $pixels.Add($current)
                $currentX = $current % $width
                $currentY = [Math]::Floor($current / $width)

                foreach ($offset in $offsets) {
                    $nextX = $currentX + $offset.X
                    $nextY = $currentY + $offset.Y
                    if ($nextX -lt 0 -or $nextX -ge $width -or $nextY -lt 0 -or $nextY -ge $height) {
                        continue
                    }

                    $nextIndex = ($nextY * $width) + $nextX
                    if (-not $visited[$nextIndex] -and $bitmap.GetPixel($nextX, $nextY).A -gt 8) {
                        $visited[$nextIndex] = $true
                        $queue.Enqueue($nextIndex)
                    }
                }
            }

            $components.Add($pixels)
        }
    }

    $keep = [bool[]]::new($width * $height)
    foreach ($component in ($components | Sort-Object Count -Descending | Select-Object -First $keepCount)) {
        foreach ($index in $component) {
            $keep[$index] = $true
        }
    }

    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $index = ($y * $width) + $x
            if (-not $keep[$index] -and $bitmap.GetPixel($x, $y).A -gt 0) {
                $bitmap.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            }
        }
    }
}

foreach ($definition in @(
    [pscustomobject]@{ Name = 'AutoSwitch'; Source = 'AutoSwitch-source.png'; KeepComponents = 0 },
    [pscustomobject]@{ Name = 'TrayMinimize'; Source = 'TrayMinimize-source.png'; KeepComponents = 2 },
    [pscustomobject]@{ Name = 'Dawn'; Source = 'Dawn-source.png'; KeepComponents = 0 },
    [pscustomobject]@{ Name = 'Day'; Source = 'Day-source.png'; KeepComponents = 0 },
    [pscustomobject]@{ Name = 'Dusk'; Source = 'Dusk-source.png'; KeepComponents = 0 })) {
    $sourcePath = Join-Path $sourceDirectory $definition.Source
    $outputPath = Join-Path $outputDirectory ($definition.Name + '.png')
    $source = [System.Drawing.Bitmap]::FromFile($sourcePath)
    $sourceBounds = Get-AlphaBounds $source
    $scale = $visualExtent / [Math]::Max($sourceBounds.Width, $sourceBounds.Height)
    $drawWidth = [Math]::Round($sourceBounds.Width * $scale)
    $drawHeight = [Math]::Round($sourceBounds.Height * $scale)
    $drawX = [Math]::Round(($outputSize - $drawWidth) / 2)
    $drawY = [Math]::Round(($outputSize - $drawHeight) / 2)
    $destinationBounds = [System.Drawing.Rectangle]::new($drawX, $drawY, $drawWidth, $drawHeight)
    $output = [System.Drawing.Bitmap]::new(
        $outputSize,
        $outputSize,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($output)

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.DrawImage(
            $source,
            $destinationBounds,
            $sourceBounds.X,
            $sourceBounds.Y,
            $sourceBounds.Width,
            $sourceBounds.Height,
            [System.Drawing.GraphicsUnit]::Pixel)
        Remove-SmallAlphaComponents $output $definition.KeepComponents
        $output.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Output "Generated $outputPath"
    }
    finally {
        $graphics.Dispose()
        $output.Dispose()
        $source.Dispose()
    }
}
