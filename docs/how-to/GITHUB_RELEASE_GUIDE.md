# GitHub Release Guide

**Last Updated:** 2026-03-09
**Purpose:** Step-by-step guide for creating GitHub releases

---

## Overview

D3dxSkinManager uses **GitHub Actions with manual triggers** for releases. This approach provides:

✅ **Automated builds** on GitHub's infrastructure
✅ **Manual control** over when releases happen
✅ **Automatic artifact upload** (no manual zip upload needed)
✅ **Consistent build environment** (no "works on my machine" issues)
✅ **No local build required** (GitHub runners have all dependencies)

---

## Release Workflow

### Method 1: GitHub Actions (Recommended)

This is the **recommended approach** - build happens on GitHub, you just trigger it.

#### Step 1: Navigate to Actions Tab

1. Go to: https://github.com/JiarongGu/D3dxSkinManager/actions
2. Click on **"Create Release"** workflow in the left sidebar

#### Step 2: Trigger the Workflow

1. Click **"Run workflow"** button (top right)
2. Choose your options:

| Option | Description | Example |
|--------|-------------|---------|
| **Version number** | Manual version override | `1.2` |
| **Version bump type** | Auto-increment version | `minor`, `major` |
| **Create git tag** | Automatically create tag | ✅ (default: yes) |
| **Create as draft** | Create draft for review before publishing | ✅ (default: yes) ⭐ **RECOMMENDED** |
| **Mark as pre-release** | Beta/alpha release | ☐ (default: no) |

3. Click **"Run workflow"** to start

> **💡 Tip**: Keep "Create as draft" checked! This lets you download and test the artifact before publishing to users.

#### Step 3: Wait for Build

- Build takes ~5-10 minutes
- Watch progress in the Actions tab
- All steps are automated:
  - ✅ Frontend build (React/TypeScript/Vite)
  - ✅ Backend build (.NET 10)
  - ✅ C++ Launcher build (Visual Studio)
  - ✅ Package creation (ZIP with all files)
  - ✅ Git tag creation
  - ✅ GitHub release creation
  - ✅ Artifact upload

#### Step 4: Review Draft Release

Once completed:
- **Draft release** appears at: https://github.com/JiarongGu/D3dxSkinManager/releases
- Download link for `D3dxSkinManager-vX.Y-win-x64.zip` is automatically available
- Release notes are auto-extracted from [CHANGELOG.md](../../CHANGELOG.md)

**Before publishing:**
1. Download the ZIP file
2. Extract and test the application on a clean Windows machine
3. Verify all features work correctly
4. Review the release notes for accuracy

**To publish:**
1. Click "Edit" on the draft release
2. Make any final adjustments to release notes
3. Click **"Publish release"**
4. ✅ Release is now live and visible to users!

---

## Version Bumping Examples

### Minor Release (New Features & Bug Fixes)
**Current:** `1.0` → **New:** `1.1`

```
Bump type: minor
```

Use for: New features, bug fixes, improvements, non-breaking changes

### Major Release (Breaking Changes)
**Current:** `1.9` → **New:** `2.0`

```
Bump type: major
```

Use for: Breaking changes, major rewrites, significant architectural changes

### Manual Version
**Current:** `1.0` → **New:** `1.5`

```
Version number: 1.5
Bump type: none
```

Use for: Skipping versions, aligning with external versioning

---

**Versioning Scheme:** We use **MAJOR.MINOR** format (e.g., 1.0, 1.1, 2.0)
- **Minor bumps** (1.0 → 1.1) for all regular updates
- **Major bumps** (1.9 → 2.0) for breaking changes only

---

## Pre-Release Checklist

Before creating a release, ensure:

- [ ] All features are working in development mode
- [ ] [CHANGELOG.md](../CHANGELOG.md) is updated with new changes
- [ ] No critical bugs or issues
- [ ] All tests pass (if applicable)
- [ ] Documentation is up to date

---

## Release Notes

Release notes are **automatically extracted** from the root [CHANGELOG.md](../../CHANGELOG.md) (user-facing changelog).

> **Note**: There are two changelog files:
> - `/CHANGELOG.md` - User-facing changelog for GitHub releases
> - `/docs/CHANGELOG.md` - Technical changelog for developers and AI assistants

### Format

The workflow extracts the `## [Unreleased]` section from the root CHANGELOG.md:

```markdown
## [Unreleased]

### Added - 2026-03-09 - Feature Name ⭐⭐⭐
**Summary**: Brief description

**Features**:
- Feature 1
- Feature 2

**Backend Changes**:
- Change 1

**Frontend Changes**:
- Change 2
```

This becomes the GitHub release description.

### Best Practices

1. **Keep Unreleased section updated** during development
2. **Use star ratings** (⭐⭐⭐⭐⭐) to indicate importance
3. **Include migration notes** if breaking changes
4. **Reference commits/PRs** if needed

---

## Method 2: Local Build (Alternative)

If you prefer to build locally and create releases manually.

### Step 1: Run Local Build

```powershell
# Bump patch version and build
.\release.ps1 -BumpPatch

# Bump minor version and build
.\release.ps1 -BumpMinor

# Bump major version and build
.\release.ps1 -BumpMajor

# Manual version
.\release.ps1 -Version "1.2.0"

# Build only (no version change)
.\release.ps1
```

### Step 2: Create Git Tag

```bash
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0
```

### Step 3: Create GitHub Release

1. Go to: https://github.com/JiarongGu/D3dxSkinManager/releases/new
2. Select tag: `v1.0.0`
3. Release title: `D3dxSkinManager v1.0.0`
4. Copy release notes from `release/RELEASE_NOTES.md`
5. Upload `release/*.zip` files
6. Click **"Publish release"**

---

## GitHub Actions Configuration

### Workflow File Location

[`.github/workflows/release.yml`](../../.github/workflows/release.yml)

### Required Secrets

None! The workflow uses `GITHUB_TOKEN` which is automatically provided.

### Runner Requirements

- **OS:** Windows (latest)
- **Tools:** .NET 10, Node.js 20, MSBuild (for C++ launcher)
- **Duration:** ~5-10 minutes per build

---

## Troubleshooting

### Build Failed in GitHub Actions

1. **Check the Actions log** for specific error
2. **Common issues:**
   - Frontend build failed → Check `package.json` dependencies
   - Backend build failed → Check .NET version compatibility
   - C++ Launcher failed → Check MSBuild configuration

### Tag Already Exists

```bash
# Delete local tag
git tag -d v1.0

# Delete remote tag
git push origin :refs/tags/v1.0

# Re-run workflow
```

### Version Not Updated

The workflow commits version changes to `.csproj` automatically. If it fails:

1. Check repository permissions (Actions needs `write` access)
2. Manually update version in `D3dxSkinManager/D3dxSkinManager.csproj`
3. Commit and re-run workflow

---

## Release Distribution

### Package Contents

Each release includes:

```
D3dxSkinManager-vX.Y-win-x64.zip
├── D3dxSkinManager Launcher.exe  (~50 KB, C++ launcher)
├── D3dxSkinManager.exe            (~12 MB, main app)
├── libs/
│   └── 7z.dll                     (~1.9 MB, native library)
└── data/languages/
    ├── cn.json                    (~32 KB)
    └── en.json                    (~33 KB)
```

**Total Size:** ~14 MB (framework-dependent, requires .NET 10 runtime)

### Installation Instructions (for users)

**Auto-Install .NET (Recommended):**
1. Download `D3dxSkinManager-vX.Y-win-x64.zip`
2. Extract to a folder
3. Run `D3dxSkinManager Launcher.exe`
4. The launcher automatically installs .NET 10 runtime if missing

**Manual .NET Install:**
1. Install .NET 10 Runtime: https://dotnet.microsoft.com/download/dotnet/10.0
2. Run `D3dxSkinManager.exe` directly

---

## Version History

See [CHANGELOG.md](../CHANGELOG.md) for complete version history.

---

## Quick Reference

### Common Commands

```powershell
# GitHub Actions (Recommended)
# Go to Actions tab → Run workflow → Select bump type → Run

# Local build with minor bump
.\release.ps1 -BumpMinor

# Local build with major bump
.\release.ps1 -BumpMajor

# Local build with manual version
.\release.ps1 -Version "2.0"
```

### Version Schema

```
MAJOR.MINOR

1.2
│ └─── Minor: New features, bug fixes, improvements
└───── Major: Breaking changes, major rewrites
```

**Examples:**
- `1.0` → `1.1` (minor: new features, bug fixes)
- `1.9` → `2.0` (major: breaking changes)

---

## Related Documentation

- [BUILD_AND_DEPLOY.md](BUILD_AND_DEPLOY.md) - Detailed build documentation
- [CHANGELOG.md](../CHANGELOG.md) - Version history
- [AI_GUIDE.md](../AI_GUIDE.md) - Development guidelines

---

**End of GitHub Release Guide**
