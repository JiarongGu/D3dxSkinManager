# Production Build Script for D3dxSkinManager
# Builds both the React frontend and .NET backend with single-file packaging
#
# Parameters:
#   -Platform: Target platform (win-x64, win-x86, or all) - Default: win-x64
#   -SelfContained: Build as self-contained (includes .NET runtime) - Default: $true
#   -SkipFrontend: Skip React frontend build - Default: $false
#
# Examples:
#   .\build-production.ps1                                    # Build x64 self-contained
#   .\build-production.ps1 -Platform win-x86                  # Build x86 self-contained
#   .\build-production.ps1 -Platform all                      # Build both x64 and x86
#   .\build-production.ps1 -SelfContained $false              # Build x64 framework-dependent
#   .\build-production.ps1 -Platform all -SelfContained $true # Build both platforms, self-contained

param(
    [ValidateSet("win-x64", "win-x86", "all")]
    [string]$Platform = "win-x64",

    [bool]$SelfContained = $false,  # Framework-dependent by default (requires .NET 10 runtime)

    [bool]$SkipFrontend = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  D3dxSkinManager Production Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Platform: $Platform" -ForegroundColor White
Write-Host "  Self-Contained: $SelfContained" -ForegroundColor White
Write-Host "  Skip Frontend: $SkipFrontend" -ForegroundColor White
Write-Host ""

# Determine which platforms to build
$platforms = @()
if ($Platform -eq "all") {
    $platforms = @("win-x64", "win-x86")
} else {
    $platforms = @($Platform)
}

# Step 1: Build React Frontend
if (-not $SkipFrontend) {
    Write-Host "[1/4] Building React frontend..." -ForegroundColor Yellow
    Set-Location D3dxSkinManager.Client

    if (Test-Path "build") {
        Remove-Item -Recurse -Force build
    }

    npm run build

    if ($LASTEXITCODE -ne 0) {
        Write-Host "X Frontend build failed!" -ForegroundColor Red
        Set-Location ..
        exit 1
    }

    Write-Host "- Frontend built successfully" -ForegroundColor Green
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

    Write-Host "- Frontend copied to wwwroot (will be embedded as resources)" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[1/4] Skipping frontend build (using existing wwwroot)" -ForegroundColor Yellow
    Write-Host "[2/4] Skipping frontend copy" -ForegroundColor Yellow
    Write-Host ""
}

# Step 3: Publish .NET Application for each platform
Write-Host "[3/4] Publishing .NET application..." -ForegroundColor Yellow
Set-Location D3dxSkinManager

$totalPlatforms = $platforms.Count
$currentPlatform = 0

foreach ($plat in $platforms) {
    $currentPlatform++
    Write-Host ""
    Write-Host "  [$currentPlatform/$totalPlatforms] Building for $plat..." -ForegroundColor Cyan

    # Determine output path
    $runtimeIdentifier = $plat
    $outputPath = "bin\Release\net10.0-windows\$runtimeIdentifier\publish"

    # Clean previous build
    if (Test-Path $outputPath) {
        Remove-Item -Recurse -Force $outputPath
    }

    # Build publish arguments
    $publishArgs = @(
        "publish",
        "-c", "Release",
        "-r", $runtimeIdentifier,
        "-o", $outputPath,
        "/p:PublishSingleFile=true",
        "/p:IncludeAllContentForSelfExtract=false",
        "/p:PublishReadyToRun=true",
        "/p:PublishTrimmed=false"
    )

    if ($SelfContained) {
        $publishArgs += "--self-contained"
    } else {
        $publishArgs += "--no-self-contained"
    }

    # Execute publish
    Write-Host "    Running: dotnet $($publishArgs -join ' ')" -ForegroundColor DarkGray

    & dotnet $publishArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "    X .NET publish failed for $plat!" -ForegroundColor Red
        Set-Location ..
        exit 1
    }

    # Check output
    $exePath = Join-Path $outputPath "D3dxSkinManager.exe"
    if (Test-Path $exePath) {
        $exeSize = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
        Write-Host "    - Published successfully ($exeSize MB)" -ForegroundColor Green

        # Count files in output
        $fileCount = (Get-ChildItem -Path $outputPath -File).Count
        Write-Host "    📦 Output contains $fileCount files" -ForegroundColor Gray
    } else {
        Write-Host "    X Executable not found!" -ForegroundColor Red
        Set-Location ..
        exit 1
    }
}

Write-Host ""
Write-Host "- All platforms built successfully" -ForegroundColor Green
Write-Host ""

# Step 4: Create distribution folder
Write-Host "[4/4] Organizing distribution files..." -ForegroundColor Yellow

$distPath = "..\dist"
if (Test-Path $distPath) {
    Remove-Item -Recurse -Force $distPath
}
New-Item -ItemType Directory -Path $distPath -Force | Out-Null

foreach ($plat in $platforms) {
    $sourcePath = "bin\Release\net10.0-windows\$plat\publish"
    $destPath = "..\dist\$plat"

    # Create platform directory
    New-Item -ItemType Directory -Path $destPath -Force | Out-Null

    # Copy essential files
    # Main executable (single file with everything embedded except language files!)
    Copy-Item -Path "$sourcePath\D3dxSkinManager.exe" -Destination $destPath -Force

    # Copy language files (kept separate for easy user modification)
    if (Test-Path "$sourcePath\data") {
        Copy-Item -Path "$sourcePath\data" -Destination $destPath -Recurse -Force
    }

    # Note: All managed DLLs, native libraries (e_sqlite3.dll), and web resources are embedded in the exe
    # Only language files are kept separate next to exe for easy user modification

    Write-Host "  ✅ $plat packaged to dist\$plat" -ForegroundColor Green
}

Write-Host ""
Write-Host "✅ Distribution files organized" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Build Type: $(if ($SelfContained) { 'Self-Contained' } else { 'Framework-Dependent' })" -ForegroundColor Yellow
Write-Host ""

foreach ($plat in $platforms) {
    $distPlatformPath = "..\dist\$plat"
    $exePath = Join-Path $distPlatformPath "D3dxSkinManager.exe"

    if (Test-Path $exePath) {
        $exeSize = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
        $fileCount = (Get-ChildItem -Path $distPlatformPath -Recurse -File).Count

        Write-Host "Platform: $plat" -ForegroundColor Cyan
        Write-Host "  Location: dist\$plat\" -ForegroundColor White
        Write-Host "  Executable Size: $exeSize MB" -ForegroundColor White
        Write-Host "  Total Files: $fileCount (exe + language files)" -ForegroundColor White
        Write-Host ""
    }
}

Write-Host "Package Contents:" -ForegroundColor Cyan
Write-Host "  D3dxSkinManager.exe - Single executable with embedded resources" -ForegroundColor White
Write-Host "  data/languages/*.json - Language files (separate for easy editing)" -ForegroundColor White
Write-Host "" -ForegroundColor White
Write-Host "Embedded in exe:" -ForegroundColor Yellow
Write-Host "  - All managed DLLs (merged via Costura.Fody)" -ForegroundColor Green
Write-Host "  - All web resources (React app, HTML, JS, CSS, images)" -ForegroundColor Green
Write-Host "  - Archive library (SharpCompress - pure managed, no native DLL)" -ForegroundColor Green
Write-Host "  - SQLite native library (e_sqlite3.dll - extracted to temp at runtime)" -ForegroundColor Green
Write-Host ""
Write-Host "Separate files:" -ForegroundColor Yellow
Write-Host "  • Language files (data/languages/*.json) - User can edit translations" -ForegroundColor White
Write-Host ""
Write-Host "Note: Using SharpCompress instead of 7z.dll - pure managed, no native dependencies!" -ForegroundColor Cyan
Write-Host ""

Write-Host "To run the application:" -ForegroundColor Cyan
Write-Host "  cd dist\$($platforms[0])\" -ForegroundColor White
Write-Host "  .\D3dxSkinManager.exe" -ForegroundColor White
Write-Host ""

if (-not $SelfContained) {
    Write-Host "⚠️  Note: Framework-dependent build requires .NET 10 runtime to be installed" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "- Build completed successfully!" -ForegroundColor Green
Write-Host ""

Set-Location ..
