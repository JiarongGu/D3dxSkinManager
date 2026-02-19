# PowerShell script to merge interface files into implementation files
# For D3dxSkinManager refactoring

$services = @(
    @{
        Interface = "D3dxSkinManager\Modules\Mods\Services\IModArchiveService.cs"
        Implementation = "D3dxSkinManager\Modules\Mods\Services\ModArchiveService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\Mods\Services\IModImportService.cs"
        Implementation = "D3dxSkinManager\Modules\Mods\Services\ModImportService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\Mods\Services\IModQueryService.cs"
        Implementation = "D3dxSkinManager\Modules\Mods\Services\ModQueryService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\Profiles\Services\IProfileService.cs"
        Implementation = "D3dxSkinManager\Modules\Profiles\Services\ProfileService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\Migration\Services\IMigrationService.cs"
        Implementation = "D3dxSkinManager\Modules\Migration\Services\MigrationService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\Settings\Services\IGlobalSettingsService.cs"
        Implementation = "D3dxSkinManager\Modules\Settings\Services\GlobalSettingsService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\Settings\Services\ISettingsFileService.cs"
        Implementation = "D3dxSkinManager\Modules\Settings\Services\SettingsFileService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\D3DMigoto\Services\I3DMigotoService.cs"
        Implementation = "D3dxSkinManager\Modules\D3DMigoto\Services\D3DMigotoService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\Tools\Services\ICacheService.cs"
        Implementation = "D3dxSkinManager\Modules\Tools\Services\CacheService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\Tools\Services\IConfigurationService.cs"
        Implementation = "D3dxSkinManager\Modules\Tools\Services\ConfigurationService.cs"
    },
    @{
        Interface = "D3dxSkinManager\Modules\Tools\Services\IStartupValidationService.cs"
        Implementation = "D3dxSkinManager\Modules\Tools\Services\StartupValidationService.cs"
    }
)

foreach ($service in $services) {
    $interfacePath = Join-Path $PSScriptRoot $service.Interface
    $implPath = Join-Path $PSScriptRoot $service.Implementation

    if (-not (Test-Path $interfacePath)) {
        Write-Host "Interface not found: $interfacePath" -ForegroundColor Yellow
        continue
    }

    if (-not (Test-Path $implPath)) {
        Write-Host "Implementation not found: $implPath" -ForegroundColor Yellow
        continue
    }

    Write-Host "Processing: $($service.Interface)" -ForegroundColor Cyan

    # Read both files
    $interfaceContent = Get-Content $interfacePath -Raw
    $implContent = Get-Content $implPath -Raw

    # Extract interface definition (between namespace and first class)
    if ($interfaceContent -match '(?s)namespace\s+([^;]+?)\s*\{(.*?)public\s+interface') {
        $namespace = $matches[1].Trim()
        $beforeInterface = $matches[2]
    }

    if ($interfaceContent -match '(?s)(public\s+interface\s+\w+[^}]+\})') {
        $interfaceDef = $matches[1]
    }

    # Extract implementation
    if ($implContent -match '(?s)(using\s+.*;[\s\S]*?)namespace') {
        $usings = $matches[1].Trim()
    }

    if ($implContent -match '(?s)namespace\s+([^;]+?);') {
        $implNamespace = $matches[1].Trim()
    }

    if ($implContent -match '(?s)(///\s*<summary>[\s\S]*?public\s+class\s+\w+[\s\S]+$)') {
        $classDef = $matches[1]
    }

    # Merge: usings + namespace + interface + blank line + class
    $merged = @"
$usings

namespace $implNamespace;

$interfaceDef

$classDef
"@

    # Write merged file
    Set-Content -Path $implPath -Value $merged -NoNewline

    # Delete interface file
    Remove-Item $interfacePath -Force

    Write-Host "  ✓ Merged and deleted interface file" -ForegroundColor Green
}

Write-Host "`nDone! Run 'dotnet build' to verify." -ForegroundColor Green
