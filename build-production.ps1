# Production Build Script for D3dxSkinManager
# Builds both the React frontend and .NET backend with single-file packaging
#
# Parameters:
#   -Platform: Target platform (win-x64, win-x86, or all) - Default: win-x64
#   -SelfContained: Build as self-contained (includes .NET runtime) - Default: $false
#   -SkipFrontend: Skip React frontend build - Default: $false
#   -SkipBootstrapper: Skip building the bootstrapper (for framework-dependent builds) - Default: $false
#
# Examples:
#   .\build-production.ps1                                    # Build x64 framework-dependent with bootstrapper
#   .\build-production.ps1 -Platform win-x86                  # Build x86 framework-dependent with bootstrapper
#   .\build-production.ps1 -Platform all                      # Build both x64 and x86 with bootstrapper
#   .\build-production.ps1 -SelfContained $true               # Build x64 self-contained (no bootstrapper needed)
#   .\build-production.ps1 -Platform all -SelfContained $true # Build both platforms, self-contained

param(
    [ValidateSet("win-x64", "win-x86", "all")]
    [string]$Platform = "win-x64",

    [bool]$SelfContained = $false,  # Framework-dependent by default (uses bootstrapper for auto-install)

    [bool]$SkipFrontend = $false,

    [bool]$SkipBootstrapper = $false
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
Write-Host "  Skip Bootstrapper: $SkipBootstrapper" -ForegroundColor White
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
        "/p:PublishReadyToRun=true",
        "/p:PublishTrimmed=false"
    )

    # Single-file publishing only for self-contained builds
    if ($SelfContained) {
        $publishArgs += "/p:PublishSingleFile=true"
        $publishArgs += "/p:IncludeAllContentForSelfExtract=false"
        $publishArgs += "--self-contained"
    } else {
        # Framework-dependent: don't use single-file to ensure runtimeconfig.json is generated
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

# Step 3.5: Build C++ launcher for framework-dependent builds
if (-not $SelfContained -and -not $SkipBootstrapper) {
    Write-Host "[3.5/5] Building C++ launcher..." -ForegroundColor Yellow
    Set-Location ..\D3dxSkinManager.Launcher

    $totalPlatforms = $platforms.Count
    $currentPlatform = 0

    # Find MSBuild
    $msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
        -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
        -prerelease 2>$null | Select-Object -First 1

    if (-not $msbuildPath -or -not (Test-Path $msbuildPath)) {
        Write-Host "    ⚠️  MSBuild not found. Skipping C++ launcher build." -ForegroundColor Yellow
        Write-Host "    Install Visual Studio 2022 with C++ development tools to enable launcher build." -ForegroundColor Yellow
        Write-Host ""
        Set-Location ..\D3dxSkinManager
    } else {
        Write-Host "    Using MSBuild: $msbuildPath" -ForegroundColor Gray
        Write-Host ""

        foreach ($plat in $platforms) {
            $currentPlatform++
            Write-Host ""
            Write-Host "  [$currentPlatform/$totalPlatforms] Building C++ launcher for $plat..." -ForegroundColor Cyan

            $msbuildPlat = if ($plat -eq "win-x86") { "Win32" } else { "x64" }

            & $msbuildPath Launcher.vcxproj `
                /p:Configuration=Release `
                /p:Platform=$msbuildPlat `
                /t:Rebuild `
                /m `
                /v:minimal `
                /nologo

            if ($LASTEXITCODE -ne 0) {
                Write-Host "    X C++ launcher build failed for $plat!" -ForegroundColor Red
                Set-Location ..\D3dxSkinManager
                exit 1
            }

            # Check output
            $launcherExePath = Join-Path $PSScriptRoot "D3dxSkinManager.Launcher\bin\$msbuildPlat\Release\D3dxSkinManager Launcher.exe"
            if (Test-Path $launcherExePath) {
                $exeSize = [math]::Round((Get-Item $launcherExePath).Length / 1KB, 2)
                Write-Host "    - C++ launcher built successfully ($exeSize KB)" -ForegroundColor Green
            } else {
                Write-Host "    X Launcher executable not found: $launcherExePath" -ForegroundColor Red
                Set-Location ..\D3dxSkinManager
                exit 1
            }
        }

        Write-Host ""
        Write-Host "- C++ launcher built successfully" -ForegroundColor Green
        Write-Host ""

        Set-Location ..\D3dxSkinManager
    }
} elseif (-not $SelfContained) {
    Write-Host "[3.5/5] Skipping C++ launcher build (SkipBootstrapper=$SkipBootstrapper)" -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host "[3.5/5] Skipping C++ launcher build (self-contained mode)" -ForegroundColor Yellow
    Write-Host ""
}

# Step 4: Create publish folder
Write-Host "[4/5] Organizing publish files..." -ForegroundColor Yellow

$publishPath = Join-Path $PSScriptRoot "publish"
if (Test-Path $publishPath) {
    Remove-Item -Recurse -Force $publishPath
}
New-Item -ItemType Directory -Path $publishPath -Force | Out-Null

foreach ($plat in $platforms) {
    $sourcePath = Join-Path $PSScriptRoot "D3dxSkinManager\bin\Release\net10.0-windows\$plat\publish"
    $destPath = Join-Path $PSScriptRoot "publish\$plat"

    # Create platform directory
    New-Item -ItemType Directory -Path $destPath -Force | Out-Null

    # For framework-dependent builds with C++ launcher
    if (-not $SelfContained -and -not $SkipBootstrapper) {
        $msbuildPlat = if ($plat -eq "win-x86") { "Win32" } else { "x64" }
        $launcherSourcePath = Join-Path $PSScriptRoot "D3dxSkinManager.Launcher\bin\$msbuildPlat\Release\D3dxSkinManager Launcher.exe"

        # Check if C++ launcher was built
        if (Test-Path $launcherSourcePath) {
            # Copy C++ launcher (user launches this)
            [System.IO.File]::Copy($launcherSourcePath, (Join-Path $destPath "D3dxSkinManager Launcher.exe"), $true)

            # Copy main app exe (Costura-merged single file)
            $mainExePath = Join-Path $sourcePath "D3dxSkinManager.exe"
            if (Test-Path $mainExePath) {
                [System.IO.File]::Copy($mainExePath, (Join-Path $destPath "D3dxSkinManager.exe"), $true)
            }

            $launcherSize = [math]::Round((Get-Item (Join-Path $destPath "D3dxSkinManager Launcher.exe")).Length / 1KB, 2)
            $mainSize = [math]::Round((Get-Item (Join-Path $destPath "D3dxSkinManager.exe")).Length / 1MB, 2)
            Write-Host "    📦 Copied C++ launcher as 'D3dxSkinManager Launcher.exe' ($launcherSize KB)" -ForegroundColor Gray
            Write-Host "    📦 Copied main app as 'D3dxSkinManager.exe' ($mainSize MB)" -ForegroundColor Gray
        } else {
            # Fallback: C++ launcher not available, copy main exe directly
            Write-Host "    ⚠️  C++ launcher not found, using main exe directly" -ForegroundColor Yellow
            Copy-Item -Path "$sourcePath\D3dxSkinManager.exe" -Destination $destPath -Force
        }
    } else {
        # For self-contained builds, just copy the main executable
        Copy-Item -Path "$sourcePath\D3dxSkinManager.exe" -Destination $destPath -Force
    }

    # Copy language files (kept separate for easy user modification)
    if (Test-Path "$sourcePath\data\languages") {
        $langDestPath = Join-Path $destPath "data\languages"
        New-Item -ItemType Directory -Path $langDestPath -Force | Out-Null
        Copy-Item -Path "$sourcePath\data\languages\*" -Destination $langDestPath -Force
    }

    # Copy the shipped resources\ folder (site default configs / seeds). RemoteSourceStore copies these
    # into {data}/remote-sources on first run — without them the remote library shows NO sites.
    if (Test-Path "$sourcePath\resources") {
        Copy-Item -Path "$sourcePath\resources" -Destination $destPath -Recurse -Force
        Write-Host "    📦 Copied resources folder (site default configs)" -ForegroundColor Gray
    } else {
        Write-Host "    ⚠️  Warning: resources folder not found! Remote library will show no sites." -ForegroundColor Yellow
    }

    # Copy native libraries (7z.dll for fast archive extraction)
    if (Test-Path "$sourcePath\libs") {
        Copy-Item -Path "$sourcePath\libs" -Destination $destPath -Recurse -Force
        Write-Host "    📦 Copied libs folder (7z.dll)" -ForegroundColor Gray
    } else {
        Write-Host "    ⚠️  Warning: libs folder not found! 7z.dll will be missing." -ForegroundColor Yellow
    }

    # Note: All managed DLLs and web resources are embedded in the exe
    # Language files and native libraries (7z.dll) are kept separate

    Write-Host "  ✅ $plat packaged to publish\$plat" -ForegroundColor Green
}

Write-Host ""
Write-Host "✅ Publish files organized" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Build Type: $(if ($SelfContained) { 'Self-Contained' } else { 'Framework-Dependent' })" -ForegroundColor Yellow
Write-Host ""

foreach ($plat in $platforms) {
    $publishPlatformPath = Join-Path $PSScriptRoot "publish\$plat"
    $exePath = Join-Path $publishPlatformPath "D3dxSkinManager.exe"

    if (Test-Path $exePath) {
        $exeSize = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
        $fileCount = (Get-ChildItem -Path $publishPlatformPath -Recurse -File).Count

        Write-Host "Platform: $plat" -ForegroundColor Cyan
        Write-Host "  Location: publish\$plat\" -ForegroundColor White
        Write-Host "  Executable Size: $exeSize MB" -ForegroundColor White
        Write-Host "  Total Files: $fileCount (exe + language files)" -ForegroundColor White
        Write-Host ""
    }
}

Write-Host "Package Contents:" -ForegroundColor Cyan

if (-not $SelfContained -and -not $SkipBootstrapper) {
    Write-Host "  D3dxSkinManager.exe - Native C++ launcher (~50KB) that auto-installs .NET 10" -ForegroundColor White
    Write-Host "  D3dxSkinManager.dll - Main application with embedded resources (framework-dependent)" -ForegroundColor White
} else {
    Write-Host "  D3dxSkinManager.exe - Single executable with embedded resources" -ForegroundColor White
}

Write-Host "  data/languages/*.json - Language files (separate for easy editing)" -ForegroundColor White
Write-Host "  libs/7z.dll - 7-Zip native library (for fast archive extraction)" -ForegroundColor White
Write-Host "" -ForegroundColor White
Write-Host "Embedded in main exe:" -ForegroundColor Yellow
Write-Host "  - All managed DLLs (merged via Costura.Fody)" -ForegroundColor Green
Write-Host "  - All web resources (React app, HTML, JS, CSS, images)" -ForegroundColor Green
Write-Host "  - Archive library (SharpSevenZip - managed wrapper)" -ForegroundColor Green
Write-Host "  - SQLite native library (e_sqlite3.dll - extracted to temp at runtime)" -ForegroundColor Green
Write-Host ""
Write-Host "Separate files:" -ForegroundColor Yellow
Write-Host "  • Language files (data/languages/*.json) - User can edit translations" -ForegroundColor White
Write-Host "  • Native library (libs/7z.dll) - 10x faster 7z/LZMA extraction vs pure managed" -ForegroundColor White
Write-Host ""
Write-Host "Note: Using native 7z.dll for performance - 10x+ faster archive extraction!" -ForegroundColor Cyan
Write-Host ""

Write-Host "To run the application:" -ForegroundColor Cyan
Write-Host "  cd publish\$($platforms[0])\" -ForegroundColor White
Write-Host "  .\D3dxSkinManager.exe" -ForegroundColor White
Write-Host ""

if (-not $SelfContained -and -not $SkipBootstrapper) {
    Write-Host "✓ C++ launcher features:" -ForegroundColor Green
    Write-Host "  • Tiny footprint (~50KB native executable)" -ForegroundColor Cyan
    Write-Host "  • Automatically detects and installs .NET 10 runtime if missing" -ForegroundColor Cyan
    Write-Host "  • Auto-update support ready (to be implemented)" -ForegroundColor Cyan
    Write-Host "  • Users can run the app without manually installing .NET!" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Total download size: ~12-15 MB (vs ~150 MB for self-contained)" -ForegroundColor Yellow
    Write-Host ""
} elseif (-not $SelfContained) {
    Write-Host "⚠️  Note: Framework-dependent build requires .NET 10 runtime to be installed" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "- Build completed successfully!" -ForegroundColor Green
Write-Host ""

Set-Location ..
