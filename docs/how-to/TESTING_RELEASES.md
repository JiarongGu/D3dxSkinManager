# Testing Releases Guide

**Last Updated:** 2026-03-09

This guide walks through testing the release system locally and on GitHub before creating official releases.

---

## ✅ Local Testing (Before GitHub)

### 1. Test Version Extraction

```powershell
# Run the test script
.\test-version.ps1
```

**Expected output:**
```
✓ Current version: 1.0
Version parts:
  Major: 1
  Minor: 0
Bump simulations:
  Bump minor: 1.0 → 1.1
  Bump major: 1.0 → 2.0
✓ All tests passed!
```

### 2. Test Version Bumping (Dry Run)

```powershell
# Test minor bump (doesn't actually change anything without build)
.\release.ps1 -BumpMinor -SkipBuild

# Expected: Should update .csproj to 1.1, create release notes, package if publish/ exists
```

**What to verify:**
- [ ] Version in `D3dxSkinManager.csproj` updated to 1.1
- [ ] No errors in console
- [ ] `release/` folder created with `RELEASE_NOTES.md`

**Reset after test:**
```bash
git checkout D3dxSkinManager/D3dxSkinManager.csproj
```

### 3. Test Full Local Build (Optional, ~3-5 minutes)

```powershell
# Full build and package
.\release.ps1
```

**What to verify:**
- [ ] Frontend builds successfully
- [ ] Backend builds successfully
- [ ] C++ launcher builds (or warning shown if VS not installed)
- [ ] `release/win-x64/` contains all files
- [ ] ZIP file created in `release/`
- [ ] ZIP file size ~14 MB

**Release package should contain:**
```
D3dxSkinManager-v1.0-win-x64.zip
├── D3dxSkinManager Launcher.exe
├── D3dxSkinManager.exe
├── libs/7z.dll
└── data/languages/
    ├── cn.json
    └── en.json
```

---

## 🧪 GitHub Actions Testing

### Test 1: Draft Release (Recommended First Test)

**Purpose:** Test the complete workflow without publishing publicly

**Steps:**
1. Go to: https://github.com/JiarongGu/D3dxSkinManager/actions
2. Select "Create Release" workflow
3. Click "Run workflow"
4. Configure:
   - **Version number:** (leave empty)
   - **Bump type:** `none`
   - **Create git tag:** ✅ Checked
   - **Create as draft:** ✅ Checked (DEFAULT)
   - **Mark as pre-release:** ☐ Unchecked
5. Click "Run workflow"
6. Wait ~5-10 minutes

**What to verify:**

During workflow:
- [ ] All workflow steps complete successfully (green checkmarks)
- [ ] Frontend build succeeds
- [ ] Backend build succeeds
- [ ] C++ launcher builds
- [ ] ZIP artifact created

After workflow:
- [ ] Go to: https://github.com/JiarongGu/D3dxSkinManager/releases
- [ ] You see a **DRAFT** release for v1.0
- [ ] Download the ZIP file
- [ ] Extract and test the application:
  - [ ] Launcher runs and installs .NET (if not already installed)
  - [ ] Application launches successfully
  - [ ] Main features work (mod import, categories, etc.)
  - [ ] No critical bugs

**If everything looks good:**
1. Click "Edit" on the draft release
2. Review release notes
3. Click **"Publish release"**
4. ✅ Done! v1.0 is now live!

**If issues found:**
1. Click "Delete" on the draft release
2. Delete the git tag: `git push origin :refs/tags/v1.0`
3. Fix the issues
4. Re-run the workflow

---

### Test 2: Version Bumping Test

**Purpose:** Test automatic version bumping

**Steps:**
1. Run workflow with:
   - **Bump type:** `minor`
   - **Create as draft:** ✅ Checked
2. Wait for completion

**What to verify:**
- [ ] New release is v1.1 (not v1.0)
- [ ] `.csproj` file was updated to 1.1 in the repository
- [ ] Git tag v1.1 was created
- [ ] Release package is named `D3dxSkinManager-v1.1-win-x64.zip`

**Clean up test release:**
1. Delete draft release from GitHub
2. Delete tag: `git push origin :refs/tags/v1.1`
3. Revert .csproj: `git revert <commit-hash>` or manually reset to 1.0

---

## 🎯 Recommended Testing Flow

### Before First Official Release (v1.0)

1. ✅ **Local test** - Run `.\test-version.ps1`
2. ✅ **GitHub draft test** - Create draft v1.0 release
3. ✅ **Download and test** - Verify app works
4. ✅ **Publish** - Click "Publish release" on draft

### Before Future Releases (v1.1, v1.2, etc.)

1. ✅ **Update CHANGELOG.md** - Add changes to `[Unreleased]` section
2. ✅ **Test locally** - Run app and verify features work
3. ✅ **GitHub draft release** - Use version bump
4. ✅ **Review and publish** - Test artifact, then publish

---

## 🔍 What Each Test Verifies

| Test | Verifies |
|------|----------|
| `test-version.ps1` | Version extraction, bumping logic |
| Local `release.ps1` | Script logic, local build process |
| GitHub draft release | Complete CI/CD pipeline, artifact creation |
| Download and test | Application actually works |

---

## 🐛 Common Issues and Fixes

### Issue: "Tag already exists"

**Fix:**
```bash
# Delete local tag
git tag -d v1.0

# Delete remote tag
git push origin :refs/tags/v1.0
```

### Issue: Frontend build fails

**Check:**
- `D3dxSkinManager.Client/package-lock.json` is committed
- Node version is compatible
- No syntax errors in TypeScript files

### Issue: C++ Launcher build fails

**Check:**
- Visual Studio 2022 with C++ tools installed (GitHub runners have this)
- `D3dxSkinManager.Launcher/Launcher.vcxproj` exists
- MSBuild is available

### Issue: Application won't launch

**Check:**
- .NET 10 runtime is installed (launcher should auto-install)
- `D3dxSkinManager.exe` exists
- `libs/7z.dll` exists (architecture-specific)
- No antivirus blocking the .exe

---

## 📋 Pre-Release Checklist

Before creating ANY release (even drafts):

- [ ] All code changes committed
- [ ] Code pushed to `master` branch
- [ ] CHANGELOG.md updated
- [ ] No critical bugs
- [ ] Local testing done (if possible)

Before PUBLISHING a draft release:

- [ ] Downloaded and tested the artifact
- [ ] Verified application launches
- [ ] Verified core features work
- [ ] Release notes are accurate
- [ ] Version number is correct

---

## 🎉 Success Criteria

**A successful release means:**

1. ✅ Workflow completed without errors
2. ✅ ZIP file downloads successfully
3. ✅ Application launches on clean Windows machine
4. ✅ .NET auto-installs if missing
5. ✅ No crash on startup
6. ✅ Main features work (import mods, categories, etc.)
7. ✅ Release notes are clear and accurate

---

## 🚀 Quick Reference

**First time (v1.0):**
```
1. Push code to GitHub
2. Run workflow → Draft v1.0
3. Download ZIP and test
4. Publish draft
```

**Regular releases:**
```
1. Update CHANGELOG.md [Unreleased]
2. Run workflow → Draft v1.X (bump minor)
3. Download and test
4. Publish draft
```

**Emergency fix:**
```
1. Fix bug
2. Update CHANGELOG.md
3. Run workflow → Draft v1.X (bump minor)
4. Test thoroughly
5. Publish immediately
```

---

**Next:** See [../../RELEASING.md](../../RELEASING.md) for the production release process.
