# Build script for all plugins
# Builds all plugin projects in the Plugins directory

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building All D3dxSkinManager Plugins" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Continue"
$pluginDir = $PSScriptRoot
$successCount = 0
$failCount = 0
$plugins = @()

# Find all .csproj files
Get-ChildItem -Path $pluginDir -Filter "*.csproj" -Recurse | ForEach-Object {
    $plugins += $_
}

Write-Host "Found $($plugins.Count) plugin projects" -ForegroundColor Green
Write-Host ""

foreach ($project in $plugins) {
    $pluginName = $project.Directory.Name
    Write-Host "Building: $pluginName" -ForegroundColor Yellow

    Push-Location $project.Directory.FullName

    $output = dotnet build 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [SUCCESS] $pluginName" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "  [FAILED] $pluginName" -ForegroundColor Red
        Write-Host "  Error: $output" -ForegroundColor Red
        $failCount++
    }

    Pop-Location
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Summary:" -ForegroundColor Cyan
Write-Host "  Success: $successCount" -ForegroundColor Green
Write-Host "  Failed:  $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "========================================" -ForegroundColor Cyan

# Copy all DLLs to a central output folder
$outputDir = Join-Path $pluginDir "_output"
if (!(Test-Path $outputDir)) {
    New-Item -Path $outputDir -ItemType Directory | Out-Null
}

Write-Host ""
Write-Host "Copying plugin DLLs to: $outputDir" -ForegroundColor Yellow

Get-ChildItem -Path $pluginDir -Filter "*.dll" -Recurse | Where-Object {
    $_.Directory.Name -eq "net8.0" -and $_.Directory.Parent.Name -eq "Debug"
} | ForEach-Object {
    Copy-Item $_.FullName -Destination $outputDir -Force
    Write-Host "  Copied: $($_.Name)" -ForegroundColor Green
}

Write-Host ""
Write-Host "All plugin DLLs available in: $outputDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "To install plugins, copy DLLs to: {AppData}\plugins\" -ForegroundColor Yellow
