# Native Libraries

This folder contains native (unmanaged) DLL files required for performance-critical operations.

## 7z.dll - 7-Zip Library

The application uses SharpSevenZip which requires the native 7z.dll library for **fast extraction** of mod archives.

**Why native 7z.dll?**
- Pure managed C# extraction (SharpCompress) is 10x+ slower for 7z/LZMA archives
- Mod archives can be large (100MB+), so extraction performance is critical
- The 7z.dll is kept separate (not embedded) similar to language files

### How to Obtain 7z.dll

Download the official 7-Zip Extra package from: https://7-zip.org/download.html

#### For version 25.01 (or latest):
1. Download 7-Zip Extra (7z2501-extra.7z or latest) from https://7-zip.org/download.html
2. Extract the archive
3. Copy the DLLs to the appropriate folders:
   - Copy `x64/7z.dll` from the archive to `D3dxSkinManager/libs/x64/7z.dll`
   - Copy `x86/7z.dll` from the archive to `D3dxSkinManager/libs/x86/7z.dll`
4. Build the project - the correct DLL will be automatically copied based on your build architecture

### File Structure
```
libs/
├── x64/
│   └── 7z.dll     (64-bit version)
├── x86/
│   └── 7z.dll     (32-bit version)
└── README.md
```

### Important Notes
- **Only download from the official 7-zip.org website**
- Both x64 and x86 versions should be placed in the repository
- The build system automatically copies the correct version based on build architecture
- The DLL is NOT included in git (add to .gitignore) and must be obtained separately
- The DLL will be copied to `output/libs/7z.dll` during build/publish (similar to language files)
- The DLL is NOT embedded in the single-file EXE (it remains external for runtime loading)

### Version Compatibility
The application is tested with 7-Zip version 25.01 but should work with any 7-Zip 19.00+ version.

### Performance Impact
Using native 7z.dll provides **10x+ faster extraction** compared to pure managed C# implementations, which is critical for large mod archives.
