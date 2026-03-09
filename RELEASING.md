# Quick Release Guide

## Creating a New Release (Recommended Method)

### Using GitHub Actions (No Local Build Needed!)

1. **Go to Actions tab:** https://github.com/JiarongGu/D3dxSkinManager/actions
2. **Select "Create Release" workflow** (left sidebar)
3. **Click "Run workflow"** (top right, green button)
4. **Configure:**
   - **Version bump type:** `minor` (1.0 → 1.1) or `major` (1.9 → 2.0)
   - **Create as draft:** ✅ **Checked** (RECOMMENDED - allows review before publishing!)
   - **Create git tag:** ✅ Checked
5. **Click "Run workflow"** and wait ~5-10 minutes
6. **Review draft release:**
   - Go to: https://github.com/JiarongGu/D3dxSkinManager/releases
   - Download ZIP and test the application
   - Verify release notes are accurate
7. **Publish when ready:**
   - Click "Edit" on draft
   - Click **"Publish release"**
   - ✅ Done! Release is now live!

### What Happens Automatically

- ✅ Frontend build (React)
- ✅ Backend build (.NET 10)
- ✅ C++ Launcher build
- ✅ Version bump in `.csproj`
- ✅ Git tag creation
- ✅ GitHub release creation
- ✅ ZIP file upload
- ✅ Release notes from CHANGELOG.md

---

## Alternative: Local Build

```powershell
# Bump minor version (1.0 → 1.1)
.\release.ps1 -BumpMinor

# Bump major version (1.9 → 2.0)
.\release.ps1 -BumpMajor

# Manual version
.\release.ps1 -Version "2.0"

# Then manually create GitHub release and upload files from release/
```

---

## Before Releasing

- [ ] Update [CHANGELOG.md](CHANGELOG.md) with **user-facing** changes (what users see)
- [ ] Update [docs/CHANGELOG.md](docs/CHANGELOG.md) with **technical** changes (for AI/developers)
- [ ] Test the application works
- [ ] No critical bugs

> **Note**: We maintain two changelogs:
> - `/CHANGELOG.md` - User-facing (used for GitHub releases)
> - `/docs/CHANGELOG.md` - Technical details (for development and AI assistants)

---

## Full Documentation

- **[Testing Guide](docs/how-to/TESTING_RELEASES.md)** - Test releases before publishing
- **[Complete Release Guide](docs/how-to/GITHUB_RELEASE_GUIDE.md)** - Detailed documentation
- **[System Overview](docs/how-to/RELEASE_SYSTEM_READY.md)** - What's configured and ready
