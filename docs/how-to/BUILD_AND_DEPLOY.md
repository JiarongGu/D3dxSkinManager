# Build and Deploy Guide

**Last Updated:** 2026-03-04
**Purpose:** Complete guide for building and deploying D3dxSkinManager

---

## Overview

This guide covers the entire build and deployment process for D3dxSkinManager, including development builds, production builds, and deployment configurations.

---

## Build Architecture

### Build Components

1. **Frontend (React + TypeScript)**
   - Built with Vite
   - Output: Static files in `wwwroot/`
   - Embedded as .NET resources

2. **Backend (.NET 10)**
   - WinForms + WebView2
   - Single-file executable
   - Framework-dependent by default

3. **Resources**
   - Web resources: Embedded via EmbeddedResource
   - Language files: Separate (user-editable)
   - SQLite native library: Embedded in exe
   - Archive library: Pure managed (SharpCompress)

---

## Quick Build Commands

### Development Build

```bash
# Build frontend
cd D3dxSkinManager.Client
npm install
npm run build

# Build backend
cd ../D3dxSkinManager
dotnet build -c Debug
```

### Production Build

```powershell
# Automated production build (Windows)
.\build-production.ps1

# Manual production build
cd D3dxSkinManager.Client
npm install
npm run build

cd ../D3dxSkinManager
dotnet publish -c Release -r win-x64 --no-self-contained
```

---

## Build Script (build-production.ps1)

### Overview

The `build-production.ps1` script automates the entire production build process:

1. Builds React frontend
2. Copies frontend to backend `wwwroot/`
3. Publishes .NET application
4. Organizes distribution files

### Usage

```powershell
# Standard build (framework-dependent)
.\build-production.ps1

# Skip frontend rebuild
.\build-production.ps1 -SkipFrontend

# Self-contained build (includes .NET runtime)
.\build-production.ps1 -SelfContained
```

### Build Script Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-SkipFrontend` | Skip React build (use existing wwwroot) | `$false` |
| `-SelfContained` | Include .NET runtime in output | `$false` |

### Build Output

```
dist/win-x64/
├── D3dxSkinManager.exe  (14 MB, single-file)
└── data/languages/
    ├── cn.json
    └── en.json
```

---

## Build Configurations

### Debug vs Release

#### Debug Configuration
- **OutputType:** `Exe` (console window visible)
- **DevTools:** Enabled
- **Context Menus:** Enabled
- **Keyboard Shortcuts:** Not blocked
- **Purpose:** Development and debugging

#### Release Configuration
- **OutputType:** `WinExe` (no console window)
- **DevTools:** Disabled
- **Context Menus:** Disabled
- **Keyboard Shortcuts:** Blocked
- **Purpose:** Production deployment

### Configuration Files

**D3dxSkinManager.csproj:**
```xml
<!-- Release configuration: Disable debug symbols and hide console window -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
    <OutputType>WinExe</OutputType>
</PropertyGroup>

<!-- Single-file publish configuration -->
<PropertyGroup>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <IncludeAllContentForSelfExtract>false</IncludeAllContentForSelfExtract>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishTrimmed>false</PublishTrimmed>
</PropertyGroup>
```

---

## Resource Embedding

### Embedded Resources

The following resources are **embedded inside the executable**:

1. **All managed DLLs**
   - Merged via Costura.Fody
   - No separate DLL files in output

2. **Web resources (React app)**
   - HTML, CSS, JavaScript, images
   - Configured as EmbeddedResource in .csproj
   - Served via custom scheme handler (`https://app.local/`)

3. **SQLite native library (e_sqlite3.dll)**
   - Embedded in single-file exe
   - Extracted to temp at runtime

4. **Archive library (SharpCompress)**
   - Pure managed code (no native DLL)
   - Fully embedded

### Separate Resources

The following resources are **kept separate** for user access:

1. **Language files (data/languages/*.json)**
   - Allows users to edit translations
   - Not embedded in exe
   - Configured with `ExcludeFromSingleFile=true`

### Resource Configuration

**Embedded Web Resources (.csproj):**
```xml
<ItemGroup>
    <EmbeddedResource Include="wwwroot\**\*">
        <LogicalName>%(RecursiveDir)%(FileName)%(Extension)</LogicalName>
    </EmbeddedResource>
</ItemGroup>
```

**Separate Language Files (.csproj):**
```xml
<ItemGroup>
    <Content Include="Languages\**\*.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
        <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
        <TargetPath>data\languages\%(RecursiveDir)%(Filename)%(Extension)</TargetPath>
    </Content>
</ItemGroup>
```

---

## Archive Library (SharpCompress)

### Migration from 7z.dll

Previously, the application used SharpSevenZip with a native `7z.dll` dependency. This has been **migrated to SharpCompress 0.46.4**, a pure managed library.

### Benefits

- ✅ No native DLL dependencies
- ✅ Simpler deployment (no libs/ folder)
- ✅ Fully embedded (everything in single exe)
- ✅ Cross-platform compatible
- ✅ Supports all common formats (ZIP, 7Z, RAR, TAR, GZIP, BZIP2)

### Implementation

**Package Reference (.csproj):**
```xml
<PackageReference Include="SharpCompress" Version="0.46.4" />
```

**Usage (ArchiveHelper.cs):**
```csharp
// Extract archive
using var archive = ArchiveFactory.OpenArchive(archivePath);
foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
{
    entry.WriteToDirectory(targetDirectory);
}

// Create archive
using var stream = File.Create(outputPath);
using var writer = WriterFactory.OpenWriter(stream, archiveType, writerOptions);
foreach (var file in files)
{
    writer.Write(relativePath, file);
}
```

---

## Browser Security (Production Mode)

### Security Features Disabled in Production

When running in **production mode** (Release build without `.dev` file):

1. **DevTools:** F12 and Ctrl+Shift+I blocked
2. **Context Menus:** Right-click menus disabled
3. **Browser Shortcuts:** Keyboard shortcuts blocked via JavaScript:
   - Blocked: Ctrl+F (Find), Ctrl+H (History), Ctrl+J (Downloads)
   - Blocked: Ctrl+P (Print), Ctrl+S (Save), Ctrl+U (View Source)
   - Blocked: Ctrl+0/+/- (Zoom controls)
   - Allowed: Ctrl+C/V/X/A/Z/Y (Editing shortcuts)
4. **Password Autosave:** Disabled
5. **Status Bar:** Disabled
6. **Zoom Controls:** Disabled

### Implementation

**WebViewInitializer.cs:**
```csharp
private void ConfigureWebViewSettings()
{
    var settings = _webView.CoreWebView2.Settings;
    var isDevelopment = IsDevelopmentMode();

    // Enable dev tools only in development mode
    settings.AreDevToolsEnabled = isDevelopment;

    // Enable default context menus only in development mode
    settings.AreDefaultContextMenusEnabled = isDevelopment;

    // Block browser keyboard shortcuts in production
    if (!isDevelopment)
    {
        ConfigureKeyboardShortcutBlocking();
    }
}

private bool IsDevelopmentMode()
{
    return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ||
           File.Exists(Path.Combine(_baseDirectory, ".dev"));
}
```

### Enabling Development Mode

To enable DevTools and other features for debugging:

**Option 1: Environment Variable**
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
.\D3dxSkinManager.exe
```

**Option 2: .dev File**
```bash
# Create .dev file in application directory
touch .dev
.\D3dxSkinManager.exe
```

---

## Build Troubleshooting

### Common Issues

#### 1. Frontend Build Fails

**Symptom:** `npm run build` fails with module errors

**Solution:**
```bash
cd D3dxSkinManager.Client
rm -rf node_modules package-lock.json
npm install
npm run build
```

#### 2. Missing wwwroot Resources

**Symptom:** Application shows "React Build Not Found" error

**Solution:**
```bash
# Rebuild frontend
cd D3dxSkinManager.Client
npm run build

# Copy to backend
cd ../D3dxSkinManager
# wwwroot folder should exist with index.html
```

#### 3. SQLite DLL Not Embedded

**Symptom:** e_sqlite3.dll appears as separate file in output

**Solution:**
Verify `.csproj` has:
```xml
<PropertyGroup>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

Note: Only works for framework-dependent builds, not self-contained.

#### 4. Language Files Missing

**Symptom:** Application starts but no languages available

**Solution:**
Language files should be in `D3dxSkinManager/Languages/`:
```
D3dxSkinManager/
└── Languages/
    ├── cn.json
    └── en.json
```

#### 5. Build Script Fails on Publish

**Symptom:** `build-production.ps1` fails during .NET publish step

**Solution:**
```powershell
# Clean build artifacts
cd D3dxSkinManager
dotnet clean -c Release
rm -rf bin obj

# Retry build
.\build-production.ps1
```

---

## Deployment

### Framework-Dependent Deployment (Default)

**Requirements:**
- .NET 10 Runtime must be installed on target machine
- Download: https://dotnet.microsoft.com/download/dotnet/10.0

**Advantages:**
- Smaller executable size (~14 MB)
- Faster build times
- SQLite native library can be embedded

**Distribution:**
```
MyApp-v1.0.0-win-x64/
├── D3dxSkinManager.exe  (14 MB)
└── data/languages/
    ├── cn.json
    └── en.json
```

### Self-Contained Deployment

**Requirements:**
- No .NET runtime installation needed

**Advantages:**
- Runs on any Windows machine
- No dependencies

**Disadvantages:**
- Larger executable size (~80 MB)
- SQLite DLL cannot be embedded (appears as separate file)

**Build Command:**
```powershell
.\build-production.ps1 -SelfContained
```

**Distribution:**
```
MyApp-v1.0.0-win-x64/
├── D3dxSkinManager.exe  (80 MB, includes .NET runtime)
└── data/languages/
    ├── cn.json
    └── en.json
```

---

## Build Checklist

### Before Building Production Release

- [ ] Update version number in `D3dxSkinManager.csproj`
- [ ] Update CHANGELOG.md with release notes
- [ ] Verify all features work in development mode
- [ ] Clean build artifacts (`dotnet clean`)
- [ ] Remove any `.dev` files

### During Build

- [ ] Frontend builds without errors
- [ ] Backend publishes without errors
- [ ] No console warnings about missing resources
- [ ] Output size is expected (~14 MB framework-dependent)

### After Build

- [ ] Test application launches without errors
- [ ] Verify DevTools are disabled (F12 does nothing)
- [ ] Verify context menus are disabled (right-click does nothing)
- [ ] Check all features work correctly
- [ ] Verify language files are present and editable
- [ ] Test on clean Windows machine (framework-dependent only)

---

## Performance Optimizations

### Build Performance

The build script includes performance optimizations:

1. **Parallel Building:** Frontend and backend build independently
2. **ReadyToRun:** Pre-compiles IL to native code
3. **Resource Caching:** wwwroot resources are embedded once

### Runtime Performance

1. **WebView2 Settings:**
   - GPU rasterization enabled
   - Hardware overlays enabled
   - Background throttling disabled

2. **Database:**
   - Connection pooling enabled
   - Write-ahead logging (WAL) mode

3. **Archive Operations:**
   - Streaming extraction/compression
   - Progress reporting for large files

---

## Advanced Build Scenarios

### Building Specific Platforms

```powershell
# Windows x64 (default)
dotnet publish -c Release -r win-x64

# Windows x86
dotnet publish -c Release -r win-x86

# Windows ARM64
dotnet publish -c Release -r win-arm64
```

### Custom Output Directory

```powershell
dotnet publish -c Release -r win-x64 -o "C:\MyCustomPath"
```

### Disable ReadyToRun (Smaller Size)

```powershell
dotnet publish -c Release -r win-x64 /p:PublishReadyToRun=false
```

---

## Build Script Internals

### build-production.ps1 Workflow

```powershell
# 1. Validate environment
Test-Path "D3dxSkinManager.Client"
Test-Path "D3dxSkinManager"

# 2. Build frontend (optional)
if (!$SkipFrontend) {
    cd D3dxSkinManager.Client
    npm run build
}

# 3. Copy frontend to backend
Copy-Item -Path "build/*" -Destination "../D3dxSkinManager/wwwroot/" -Recurse

# 4. Publish backend
cd D3dxSkinManager
dotnet publish -c Release -r win-x64 --no-self-contained

# 5. Organize distribution files
New-Item -Path "dist/win-x64" -ItemType Directory
Copy-Item "bin/Release/net10.0-windows/win-x64/publish/D3dxSkinManager.exe" "dist/win-x64/"
Copy-Item "bin/Release/net10.0-windows/win-x64/publish/data" "dist/win-x64/" -Recurse
```

### Build Summary Output

After successful build, the script displays:

```
========================================
  Build Summary
========================================

Build Type: Framework-Dependent

Platform: win-x64
  Location: dist\win-x64\
  Executable Size: 13.86 MB
  Total Files: 3 (exe + language files)

Package Contents:
  • D3dxSkinManager.exe - Single executable with embedded resources
  • data/languages/*.json - Language files (separate for easy editing)

Embedded in exe:
  ✓ All managed DLLs (merged via Costura.Fody)
  ✓ All web resources (React app, HTML, JS, CSS, images)
  ✓ Archive library (SharpCompress - pure managed, no native DLL)
  ✓ SQLite native library (e_sqlite3.dll - extracted to temp at runtime)

Note: Using SharpCompress instead of 7z.dll - pure managed, no native dependencies!
```

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'

    - name: Setup Node.js
      uses: actions/setup-node@v3
      with:
        node-version: '18'

    - name: Build
      run: .\build-production.ps1
      shell: powershell

    - name: Upload Artifact
      uses: actions/upload-artifact@v3
      with:
        name: D3dxSkinManager-win-x64
        path: dist/win-x64/
```

---

## Version Management

### Updating Version Number

**D3dxSkinManager.csproj:**
```xml
<PropertyGroup>
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0</AssemblyVersion>
    <FileVersion>1.0.0</FileVersion>
</PropertyGroup>
```

### Version Display

The application displays version in:
- About dialog
- Console output on startup
- Log files

---

## References

### Related Documentation

- [AI_GUIDE.md](../AI_GUIDE.md) - AI assistant guide
- [DEVELOPMENT.md](../core/DEVELOPMENT.md) - Development setup
- [PROJECT_STRUCTURE.md](../core/PROJECT_STRUCTURE.md) - Project organization
- [CURRENT_ARCHITECTURE.md](../architecture/CURRENT_ARCHITECTURE.md) - Architecture overview

### External Links

- [.NET Single-File Publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/)
- [SharpCompress Documentation](https://github.com/adamhathcock/sharpcompress)
- [WebView2 Documentation](https://learn.microsoft.com/en-us/microsoft-edge/webview2/)
- [Vite Build Options](https://vitejs.dev/guide/build.html)

---

**End of Build and Deploy Guide**
