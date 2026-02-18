# Production Build Script for D3dxSkinManager
# This script builds both the React frontend and .NET backend

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Building D3dxSkinManager Production" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build React Frontend
Write-Host "[1/4] Building React frontend..." -ForegroundColor Yellow
Set-Location D3dxSkinManager.Client

if (Test-Path "build") {
    Remove-Item -Recurse -Force build
}

npm run build

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Frontend build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Frontend built successfully" -ForegroundColor Green
Write-Host ""

# Step 2: Copy build to backend wwwroot
Write-Host "[2/4] Copying frontend build to backend..." -ForegroundColor Yellow
Set-Location ..

$wwwrootPath = "D3dxSkinManager\wwwroot"

if (Test-Path $wwwrootPath) {
    Remove-Item -Recurse -Force $wwwrootPath
}

New-Item -ItemType Directory -Path $wwwrootPath -Force | Out-Null
Copy-Item -Path "D3dxSkinManager.Client\build\*" -Destination $wwwrootPath -Recurse -Force

Write-Host "✅ Frontend copied to wwwroot" -ForegroundColor Green
Write-Host ""

# Step 3: Publish .NET Application
Write-Host "[3/4] Publishing .NET application..." -ForegroundColor Yellow
Set-Location D3dxSkinManager

$publishPath = "bin\Release\net8.0\win-x64\publish"

if (Test-Path $publishPath) {
    Remove-Item -Recurse -Force $publishPath
}

dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ .NET publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ .NET application published" -ForegroundColor Green
Write-Host ""

# Step 4: Summary
Write-Host "[4/4] Build Summary" -ForegroundColor Yellow
Write-Host "==================" -ForegroundColor Yellow
Write-Host ""
Write-Host "✅ Build completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Output location:" -ForegroundColor Cyan
Write-Host "  $publishPath" -ForegroundColor White
Write-Host ""
Write-Host "Executable:" -ForegroundColor Cyan
Write-Host "  D3dxSkinManager.exe" -ForegroundColor White
Write-Host ""
Write-Host "To run the application:" -ForegroundColor Cyan
Write-Host "  cd $publishPath" -ForegroundColor White
Write-Host "  .\D3dxSkinManager.exe" -ForegroundColor White
Write-Host ""

Set-Location ..\..
