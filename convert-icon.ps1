# Convert PNG to ICO using System.Drawing
# Creates a multi-resolution ICO file with sizes: 16, 32, 48, 64, 128, 256

param(
    [string]$InputPng = "D3dxSkinManager\favicon.png",
    [string]$OutputIco = "D3dxSkinManager\favicon.ico"
)

Add-Type -AssemblyName System.Drawing

Write-Host "Converting PNG to ICO..." -ForegroundColor Cyan
Write-Host "  Input:  $InputPng" -ForegroundColor Gray
Write-Host "  Output: $OutputIco" -ForegroundColor Gray
Write-Host ""

if (-not (Test-Path $InputPng)) {
    Write-Host "ERROR: Input file not found: $InputPng" -ForegroundColor Red
    exit 1
}

try {
    # Load the PNG image
    $image = [System.Drawing.Image]::FromFile((Resolve-Path $InputPng).Path)

    # Create output stream
    $output = [System.IO.File]::Create($OutputIco)

    # Define icon sizes (standard Windows icon sizes)
    $sizes = @(256, 128, 64, 48, 32, 16)

    # ICO file header
    $output.WriteByte(0x00) # Reserved
    $output.WriteByte(0x00) # Reserved
    $output.WriteByte(0x01) # Type: 1 = ICO
    $output.WriteByte(0x00) # Type
    $output.WriteByte($sizes.Count) # Number of images
    $output.WriteByte(0x00) # Number of images (high byte)

    $imageDataList = New-Object System.Collections.ArrayList
    $offset = 6 + ($sizes.Count * 16) # Header + directory entries

    Write-Host "Creating icon with sizes:" -ForegroundColor Yellow

    # Process each size
    foreach ($size in $sizes) {
        Write-Host "  • ${size}x${size}" -ForegroundColor Gray

        # Create resized bitmap
        $bitmap = New-Object System.Drawing.Bitmap($size, $size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($image, 0, 0, $size, $size)
        $graphics.Dispose()

        # Save to memory stream as PNG
        $ms = New-Object System.IO.MemoryStream
        $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $imageData = $ms.ToArray()
        $ms.Dispose()
        $bitmap.Dispose()

        # Add to list
        [void]$imageDataList.Add($imageData)

        # Write directory entry
        $widthHeight = if ($size -eq 256) { 0 } else { $size }
        $output.WriteByte($widthHeight) # Width (0 = 256)
        $output.WriteByte($widthHeight) # Height
        $output.WriteByte(0x00) # Color palette (0 for PNG)
        $output.WriteByte(0x00) # Reserved
        $output.WriteByte(0x01) # Color planes (low byte)
        $output.WriteByte(0x00) # Color planes (high byte)
        $output.WriteByte(0x20) # Bits per pixel (32 bit, low byte)
        $output.WriteByte(0x00) # Bits per pixel (high byte)

        # Size of image data
        $sizeBytes = [BitConverter]::GetBytes([uint32]$imageData.Length)
        $output.Write($sizeBytes, 0, 4)

        # Offset to image data
        $offsetBytes = [BitConverter]::GetBytes([uint32]$offset)
        $output.Write($offsetBytes, 0, 4)

        $offset += $imageData.Length
    }

    # Write all image data
    foreach ($imageData in $imageDataList) {
        $output.Write($imageData, 0, $imageData.Length)
    }

    $output.Close()
    $image.Dispose()

    $fileSize = [math]::Round((Get-Item $OutputIco).Length / 1KB, 2)
    Write-Host ""
    Write-Host "✅ Icon created successfully!" -ForegroundColor Green
    Write-Host "   File: $OutputIco ($fileSize KB)" -ForegroundColor White
    Write-Host "   Sizes: 256x256, 128x128, 64x64, 48x48, 32x32, 16x16" -ForegroundColor White

} catch {
    Write-Host ""
    Write-Host "ERROR: Failed to convert icon" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
