# PowerShell script to deploy D3dxSkinManager with WebView2 bundled

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\Deployment"
)

Write-Host "=== D3dxSkinManager Deployment Script ===" -ForegroundColor Cyan
Write-Host "This will create a self-contained package with WebView2" -ForegroundColor Yellow

# Build the application
Write-Host "`nBuilding application..." -ForegroundColor Green
dotnet publish D3dxSkinManager\D3dxSkinManager.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=false `
    -o "$OutputPath\App"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Build React frontend
Write-Host "`nBuilding React frontend..." -ForegroundColor Green
Push-Location D3dxSkinManager.Client
npm install
npm run build
Pop-Location

# Copy React build to output
Write-Host "`nCopying React build..." -ForegroundColor Green
Copy-Item -Path "D3dxSkinManager.Client\dist\*" -Destination "$OutputPath\App\wwwroot" -Recurse -Force

# Download WebView2 Evergreen Bootstrapper
Write-Host "`nDownloading WebView2 Bootstrapper..." -ForegroundColor Green
$webView2Url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
$webView2Installer = "$OutputPath\MicrosoftEdgeWebview2Setup.exe"

if (!(Test-Path $webView2Installer)) {
    Invoke-WebRequest -Uri $webView2Url -OutFile $webView2Installer
    Write-Host "Downloaded WebView2 bootstrapper" -ForegroundColor Green
}

# Create installer script
Write-Host "`nCreating installer script..." -ForegroundColor Green
$installerScript = @'
@echo off
echo === D3dxSkinManager Installer ===
echo.

echo Checking WebView2 Runtime...
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" >nul 2>&1
if %errorlevel% neq 0 (
    echo WebView2 Runtime not found. Installing...
    start /wait MicrosoftEdgeWebview2Setup.exe /silent /install
    if %errorlevel% neq 0 (
        echo Failed to install WebView2 Runtime
        pause
        exit /b 1
    )
    echo WebView2 Runtime installed successfully!
) else (
    echo WebView2 Runtime is already installed.
)

echo.
echo Installation complete! You can now run D3dxSkinManager.exe
pause
'@

$installerScript | Out-File -FilePath "$OutputPath\Install.bat" -Encoding ASCII

# Create run script
$runScript = @'
@echo off
cd /d "%~dp0App"
start D3dxSkinManager.exe
'@

$runScript | Out-File -FilePath "$OutputPath\Run.bat" -Encoding ASCII

Write-Host "`n=== Deployment Complete ===" -ForegroundColor Cyan
Write-Host "Output location: $OutputPath" -ForegroundColor Green
Write-Host "`nDeployment package contains:" -ForegroundColor Yellow
Write-Host "  - App\                  : Application files"
Write-Host "  - MicrosoftEdgeWebview2Setup.exe : WebView2 installer"
Write-Host "  - Install.bat          : Installation script"
Write-Host "  - Run.bat              : Run script"
Write-Host "`nTo deploy:" -ForegroundColor Yellow
Write-Host "  1. Copy entire $OutputPath folder to target machine"
Write-Host "  2. Run Install.bat (installs WebView2 if needed)"
Write-Host "  3. Run D3dxSkinManager using Run.bat or App\D3dxSkinManager.exe"