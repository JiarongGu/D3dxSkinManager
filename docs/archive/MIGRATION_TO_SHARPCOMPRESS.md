# Migration to SharpCompress

## Why Migrate?

- ✅ Remove external 7-Zip dependency
- ✅ Faster for small-medium archives (most mods)
- ✅ Easy progress reporting
- ✅ Cross-platform support
- ✅ Cleaner code

## Step 1: Add Package

```bash
cd D3dxSkinManager
dotnet add package SharpCompress
```

## Step 2: Replace FileService Implementation

### Before (Current - External 7-Zip)

```csharp
public async Task<bool> ExtractArchiveAsync(string archivePath, string targetDirectory)
{
    var sevenZipPath = Get7ZipPath(); // Can fail if 7z not installed

    var arguments = $"x -y -o\"{targetDirectory}\" \"{archivePath}\"";
    var processStartInfo = new ProcessStartInfo
    {
        FileName = sevenZipPath,
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    using var process = new Process { StartInfo = processStartInfo };
    // ... process handling code ...
    await process.WaitForExitAsync();

    return process.ExitCode == 0;
}
```

### After (SharpCompress)

```csharp
using SharpCompress.Archives;
using SharpCompress.Common;

public async Task<bool> ExtractArchiveAsync(string archivePath, string targetDirectory)
{
    return await Task.Run(() =>
    {
        try
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Archive not found", archivePath);

            // Create target directory if needed
            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            // Open archive (auto-detects format: ZIP, RAR, 7Z, TAR, etc.)
            using var archive = ArchiveFactory.Open(archivePath);

            // Extract all entries
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                entry.WriteToDirectory(targetDirectory,
                    new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileService] Extraction failed: {ex.Message}");
            return false;
        }
    });
}
```

## Step 3: Add Progress Reporting (Optional)

```csharp
public async Task<bool> ExtractArchiveAsync(
    string archivePath,
    string targetDirectory,
    IProgress<double>? progress = null)
{
    return await Task.Run(() =>
    {
        using var archive = ArchiveFactory.Open(archivePath);

        var totalSize = archive.TotalUncompressSize;
        var extractedSize = 0L;

        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            entry.WriteToDirectory(targetDirectory,
                new ExtractionOptions { ExtractFullPath = true, Overwrite = true });

            extractedSize += entry.Size;
            progress?.Report((double)extractedSize / totalSize * 100);
        }

        return true;
    });
}
```

## Step 4: Remove 7-Zip Detection Code

You can now remove these methods from FileService:

```csharp
// ❌ DELETE THESE - No longer needed
public bool Is7ZipAvailable() { ... }
public string Get7ZipPath() { ... }
private readonly string[] _sevenZipPaths = { ... };
```

And remove this check from Program.cs:

```csharp
// ❌ DELETE THIS from VerifyDependencies()
if (!fileService.Is7ZipAvailable())
{
    Console.WriteLine("[Warning] 7-Zip not found...");
}
```

## Step 5: Update Interface

```csharp
public interface IFileService
{
    Task<string> CalculateSha256Async(string filePath);
    Task<bool> ExtractArchiveAsync(string archivePath, string targetDirectory, IProgress<double>? progress = null);
    Task<bool> CopyDirectoryAsync(string sourceDir, string targetDir, bool overwrite = true);
    Task<bool> DeleteDirectoryAsync(string directory);
    // ❌ Remove these:
    // bool Is7ZipAvailable();
    // string Get7ZipPath();
}
```

## Benefits After Migration

1. **No Installation Required**
   - Users don't need to install 7-Zip
   - Simpler deployment

2. **Better Performance**
   - No process spawn overhead (~50-200ms saved per extraction)
   - Direct memory operations

3. **Progress Reporting**
   - Easy to show extraction progress in UI
   - Better user experience

4. **Cross-Platform**
   - Works on Windows, Linux, macOS
   - Future-proof

5. **Cleaner Code**
   - No process management
   - No path detection
   - No error parsing from external process

## Testing

```csharp
// Test various formats
await fileService.ExtractArchiveAsync("mod.zip", "output/");  // ✅
await fileService.ExtractArchiveAsync("mod.7z", "output/");   // ✅
await fileService.ExtractArchiveAsync("mod.rar", "output/");  // ✅
await fileService.ExtractArchiveAsync("mod.tar.gz", "output/"); // ✅
```

## Rollback Plan

If you need to rollback, just:
1. Revert FileService.cs changes
2. Remove SharpCompress package
3. Ensure 7-Zip is installed

## Estimated Migration Time

- Code changes: **15 minutes**
- Testing: **10 minutes**
- Total: **25 minutes**

Simple and safe migration!
