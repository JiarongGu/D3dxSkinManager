# GitHub Release Helper Script for D3dxSkinManager
# This script helps create GitHub releases by:
# 1. Extracting/bumping version in .csproj
# 2. Running production build
# 3. Creating release package
# 4. Generating release notes from CHANGELOG.md
# 5. Creating git tags
#
# Usage:
#   .\release.ps1                           # Create release with current version
#   .\release.ps1 -Version "1.2"            # Set specific version
#   .\release.ps1 -BumpMajor                # Bump major (1.0 -> 2.0)
#   .\release.ps1 -BumpMinor                # Bump minor (1.0 -> 1.1)
#   .\release.ps1 -SkipBuild                # Skip build, just package
#   .\release.ps1 -CreateTag                # Auto create and push git tag
#   .\release.ps1 -BumpMinor -CreateTag     # Bump version, build, tag, and package

param(
    [string]$Version,
    [switch]$BumpMajor,
    [switch]$BumpMinor,
    [switch]$SkipBuild,
    [switch]$CreateTag,
    [ValidateSet("win-x64", "win-x86", "all")]
    [string]$Platform = "all"
)

$ErrorActionPreference = "Stop"

# Helper function to extract version from .csproj
function Get-ProjectVersion {
    $csprojPath = "D3dxSkinManager\D3dxSkinManager.csproj"
    if (-not (Test-Path $csprojPath)) {
        throw "Could not find .csproj file at $csprojPath"
    }

    [xml]$csproj = Get-Content $csprojPath
    $versionNode = $csproj.Project.PropertyGroup.Version | Where-Object { $_ -ne $null } | Select-Object -First 1

    if (-not $versionNode) {
        throw "No <Version> element found in .csproj"
    }

    return $versionNode
}

# Helper function to update version in .csproj
function Set-ProjectVersion {
    param([string]$NewVersion)

    $csprojPath = "D3dxSkinManager\D3dxSkinManager.csproj"
    if (-not (Test-Path $csprojPath)) {
        throw "Could not find .csproj file at $csprojPath"
    }

    # Read file content
    [xml]$csproj = Get-Content $csprojPath

    # Find PropertyGroup with Version
    $propertyGroup = $csproj.Project.PropertyGroup | Where-Object { $_.Version -ne $null } | Select-Object -First 1

    if (-not $propertyGroup) {
        throw "No <Version> element found in .csproj"
    }

    # Update all version fields
    $propertyGroup.Version = $NewVersion
    $propertyGroup.AssemblyVersion = "$NewVersion.0"
    $propertyGroup.FileVersion = "$NewVersion.0"
    $propertyGroup.InformationalVersion = $NewVersion

    # Save with proper formatting
    $csproj.Save($csprojPath)

    Write-Host "  ✓ Updated version in .csproj to $NewVersion" -ForegroundColor Green
}

# Helper function to bump version
function Get-BumpedVersion {
    param(
        [string]$CurrentVersion,
        [string]$BumpType  # "major" or "minor"
    )

    if ($CurrentVersion -notmatch '^(\d+)\.(\d+)$') {
        throw "Invalid version format: $CurrentVersion (expected: X.Y)"
    }

    $major = [int]$matches[1]
    $minor = [int]$matches[2]

    switch ($BumpType) {
        "major" {
            $major++
            $minor = 0
        }
        "minor" {
            $minor++
        }
    }

    return "$major.$minor"
}

# Helper function to generate release notes from git log
function Get-ReleaseNotes {
    param([string]$Version)

    $currentTag = "v$Version"

    # Find previous tag
    $allTags = git tag --sort=-version:refname | Where-Object { $_ -match '^v\d' }
    $previousTag = $null
    foreach ($t in $allTags) {
        if ($t -ne $currentTag) {
            $previousTag = $t
            break
        }
    }

    # Get commits between previous tag and HEAD
    if ($previousTag) {
        Write-Host "  Generating notes: $previousTag..$currentTag" -ForegroundColor Gray
        $commits = git log "$previousTag..HEAD" --pretty=format:"%s" --no-merges
    } else {
        Write-Host "  No previous tag found — using recent 50 commits" -ForegroundColor Gray
        $commits = git log -50 --pretty=format:"%s" --no-merges
    }

    # Categorize by conventional commit prefix
    $features = @()
    $fixes = @()
    $other = @()

    foreach ($msg in $commits) {
        if ($msg -match '^feat:\s*(.+)') {
            $features += $matches[1].Trim()
        } elseif ($msg -match '^fix:\s*(.+)') {
            $fixes += $matches[1].Trim()
        } elseif ($msg -match '^(chore|docs|refactor|style|perf|test|ci):\s*') {
            # Skip non-user-facing commits
        } else {
            $other += $msg
        }
    }

    # Build markdown
    $notes = @()

    if ($features.Count -gt 0) {
        $notes += "### New Features"
        $notes += ""
        foreach ($f in $features) { $notes += "- $f" }
        $notes += ""
    }

    if ($fixes.Count -gt 0) {
        $notes += "### Bug Fixes"
        $notes += ""
        foreach ($f in $fixes) { $notes += "- $f" }
        $notes += ""
    }

    if ($other.Count -gt 0) {
        $notes += "### Other Changes"
        $notes += ""
        foreach ($f in $other) { $notes += "- $f" }
        $notes += ""
    }

    if ($notes.Count -eq 0) {
        return "No notable changes in this release."
    }

    return ($notes -join "`n")
}

# ========================================
# Main Script
# ========================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  D3dxSkinManager Release Helper" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Determine version (with optional bumping)
$currentVersion = Get-ProjectVersion
Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host ""

# Handle version bumping
if ($BumpMajor -or $BumpMinor) {
    if ($Version) {
        Write-Host "⚠️  Warning: Both -Version and -Bump* specified. -Version takes precedence." -ForegroundColor Yellow
        Write-Host ""
    } else {
        $bumpType = if ($BumpMajor) { "major" } else { "minor" }
        $Version = Get-BumpedVersion -CurrentVersion $currentVersion -BumpType $bumpType

        Write-Host "Bumping $bumpType version: $currentVersion → $Version" -ForegroundColor Yellow

        # Update .csproj file
        Set-ProjectVersion -NewVersion $Version
        Write-Host ""
    }
}

# Use current version if no version specified or bumped
if (-not $Version) {
    $Version = $currentVersion
    Write-Host "Using current version: $Version" -ForegroundColor Yellow
} elseif ($Version -ne $currentVersion) {
    # Manual version override
    Write-Host "Setting version to: $Version (was $currentVersion)" -ForegroundColor Yellow
    Set-ProjectVersion -NewVersion $Version
    Write-Host ""
}

$releaseTag = "v$Version"
Write-Host "Release tag: $releaseTag" -ForegroundColor Cyan
Write-Host ""

# Step 2: Check git status
Write-Host "Checking git status..." -ForegroundColor Yellow
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Host ""
    Write-Host "⚠️  WARNING: You have uncommitted changes:" -ForegroundColor Yellow
    Write-Host $gitStatus -ForegroundColor Gray
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne "y" -and $continue -ne "Y") {
        Write-Host "Aborted." -ForegroundColor Red
        exit 1
    }
}

# Check if tag already exists
$existingTag = git tag -l $releaseTag
if ($existingTag) {
    Write-Host ""
    Write-Host "⚠️  WARNING: Tag $releaseTag already exists!" -ForegroundColor Yellow
    Write-Host "You may want to delete it first: git tag -d $releaseTag" -ForegroundColor Gray
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne "y" -and $continue -ne "Y") {
        Write-Host "Aborted." -ForegroundColor Red
        exit 1
    }
}

Write-Host "✓ Git status checked" -ForegroundColor Green
Write-Host ""

# Step 3: Run production build (unless skipped)
if (-not $SkipBuild) {
    Write-Host "Running production build..." -ForegroundColor Yellow
    Write-Host "Platform: $Platform" -ForegroundColor Gray
    Write-Host ""

    $buildArgs = @("-Platform", $Platform)
    & .\build-production.ps1 @buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "✗ Build failed!" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "✓ Build completed successfully" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "Skipping build (using existing publish/ directory)" -ForegroundColor Yellow
    Write-Host ""
}

# Step 4: Create release package
Write-Host "Creating release package..." -ForegroundColor Yellow

$releasePath = "release"
if (Test-Path $releasePath) {
    Remove-Item -Recurse -Force $releasePath
}
New-Item -ItemType Directory -Path $releasePath -Force | Out-Null

# Determine which platforms were built
$platforms = @()
if ($Platform -eq "all") {
    $platforms = @("win-x64", "win-x86")
} else {
    $platforms = @($Platform)
}

# Package each platform
foreach ($plat in $platforms) {
    $publishDir = "publish\$plat"
    if (-not (Test-Path $publishDir)) {
        Write-Host "  ⚠️  Warning: $publishDir not found, skipping" -ForegroundColor Yellow
        continue
    }

    $zipName = "D3dxSkinManager-$releaseTag-$plat.zip"
    $zipPath = Join-Path $releasePath $zipName

    Write-Host "  Creating $zipName..." -ForegroundColor Cyan

    # Create zip using PowerShell
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

    $zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
    Write-Host "    ✓ Created $zipName ($zipSize MB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "✓ Release packages created in release/" -ForegroundColor Green
Write-Host ""

# Step 5: Generate release notes
Write-Host "Generating release notes..." -ForegroundColor Yellow

$releaseNotes = Get-ReleaseNotes -Version $Version
$releaseNotesPath = Join-Path $releasePath "RELEASE_NOTES.md"

$fullReleaseNotes = @"
# D3dxSkinManager $releaseTag

## What's New

$releaseNotes

## Installation

### Option 1: Framework-Dependent (Recommended - Smaller Download)
1. Download ``D3dxSkinManager-$releaseTag-win-x64.zip``
2. Extract to a folder
3. Run ``D3dxSkinManager Launcher.exe``
4. The launcher will automatically install .NET 10 runtime if needed

**Requirements**: Windows 10/11 (x64)
**Download Size**: ~12-15 MB

### Option 2: Self-Contained (No Installation Required)
1. Download the self-contained version if available
2. Extract and run ``D3dxSkinManager.exe``

**Requirements**: Windows 10/11 (x64)
**Download Size**: ~150 MB

## Package Contents

- **D3dxSkinManager Launcher.exe** - Native C++ launcher with auto .NET installer
- **D3dxSkinManager.exe** - Main application
- **data/languages/** - Translation files (editable)
- **libs/7z.dll** - Native 7-Zip library for fast archive extraction

## Changelog

See [CHANGELOG.md](https://github.com/JiarongGu/D3dxSkinManager/blob/master/docs/CHANGELOG.md) for full details.

## Support

- Report issues: https://github.com/JiarongGu/D3dxSkinManager/issues
- Documentation: https://github.com/JiarongGu/D3dxSkinManager/tree/master/docs
"@

$fullReleaseNotes | Out-File -FilePath $releaseNotesPath -Encoding UTF8

Write-Host "  ✓ Release notes saved to release/RELEASE_NOTES.md" -ForegroundColor Green
Write-Host ""

# Step 6: Display summary and next steps
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Release Package Ready!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Tag: $releaseTag" -ForegroundColor Yellow
Write-Host ""

Write-Host "Release files:" -ForegroundColor Cyan
Get-ChildItem $releasePath -File | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  • $($_.Name) ($size MB)" -ForegroundColor White
}
Write-Host ""

# Step 7: Git tagging
if ($CreateTag) {
    Write-Host "Creating git tag..." -ForegroundColor Yellow

    git tag -a $releaseTag -m "Release $releaseTag"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Failed to create git tag" -ForegroundColor Red
        exit 1
    }

    Write-Host "✓ Git tag created: $releaseTag" -ForegroundColor Green
    Write-Host ""

    Write-Host "Push tag to GitHub with:" -ForegroundColor Cyan
    Write-Host "  git push origin $releaseTag" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Create and push git tag:" -ForegroundColor Yellow
    Write-Host "   git tag -a $releaseTag -m `"Release $releaseTag`"" -ForegroundColor White
    Write-Host "   git push origin $releaseTag" -ForegroundColor White
    Write-Host ""
    Write-Host "2. Create GitHub release:" -ForegroundColor Yellow
    Write-Host "   • Go to: https://github.com/JiarongGu/D3dxSkinManager/releases/new" -ForegroundColor White
    Write-Host "   • Select tag: $releaseTag" -ForegroundColor White
    Write-Host "   • Title: `"D3dxSkinManager $releaseTag`"" -ForegroundColor White
    Write-Host "   • Copy content from: release\RELEASE_NOTES.md" -ForegroundColor White
    Write-Host "   • Upload files from: release\" -ForegroundColor White
    Write-Host "   • Click `"Publish release`"" -ForegroundColor White
    Write-Host ""
}

Write-Host "✓ Release preparation complete!" -ForegroundColor Green
Write-Host ""
