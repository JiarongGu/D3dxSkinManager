using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using D3dxSkinManager.Modules.Core.Services;
namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Service for mod archive operations (extract, load, unload)
/// Responsibility: File system operations for loading/unloading mods
/// </summary>
public class ModArchiveService : IModArchiveService
{
    private readonly string _modsDirectory;
    private readonly string _workModsDirectory;
    private readonly IFileService _fileService;

    public ModArchiveService(string dataPath, IFileService fileService)
    {
        _modsDirectory = Path.Combine(dataPath, "mods");
        _workModsDirectory = Path.Combine(dataPath, "work_mods");
        _fileService = fileService;

        // Ensure directories exist
        Directory.CreateDirectory(_modsDirectory);
        Directory.CreateDirectory(_workModsDirectory);
    }

    /// <summary>
    /// Load a mod by extracting its archive to work directory
    /// </summary>
    public async Task<bool> LoadAsync(string sha)
    {
        try
        {
            var archivePath = GetArchivePath(sha);
            if (!File.Exists(archivePath))
            {
                Console.WriteLine($"[ModArchiveService] Archive not found: {archivePath}");
                return false;
            }

            var targetDirectory = Path.Combine(_workModsDirectory, sha);

            // If already extracted, just rename it (remove DISABLED_ prefix if present)
            if (Directory.Exists(targetDirectory + "_DISABLED"))
            {
                Directory.Move(targetDirectory + "_DISABLED", targetDirectory);
                Console.WriteLine($"[ModArchiveService] Enabled mod: {sha}");
                return true;
            }

            // Extract archive
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, true);
            }

            var success = await _fileService.ExtractArchiveAsync(archivePath, targetDirectory);
            if (success)
            {
                Console.WriteLine($"[ModArchiveService] Loaded mod: {sha}");
            }

            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModArchiveService] Error loading mod {sha}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unload a mod by disabling its work directory
    /// </summary>
    public async Task<bool> UnloadAsync(string sha)
    {
        try
        {
            var workDirectory = Path.Combine(_workModsDirectory, sha);
            if (!Directory.Exists(workDirectory))
            {
                Console.WriteLine($"[ModArchiveService] Mod not loaded: {sha}");
                return false;
            }

            // Rename to DISABLED instead of deleting
            var disabledDirectory = workDirectory + "_DISABLED";
            if (Directory.Exists(disabledDirectory))
            {
                Directory.Delete(disabledDirectory, true);
            }

            Directory.Move(workDirectory, disabledDirectory);
            Console.WriteLine($"[ModArchiveService] Unloaded mod: {sha}");

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModArchiveService] Error unloading mod {sha}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Delete a mod permanently (archive + work directories + images)
    /// </summary>
    public async Task<bool> DeleteAsync(string sha, string? thumbnailPath, string? previewPath)
    {
        try
        {
            var deleted = false;

            // Delete archive
            var archivePath = GetArchivePath(sha);
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
                Console.WriteLine($"[ModArchiveService] Deleted archive: {archivePath}");
                deleted = true;
            }

            // Delete work directory
            var workDirectory = Path.Combine(_workModsDirectory, sha);
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, true);
                Console.WriteLine($"[ModArchiveService] Deleted work directory: {workDirectory}");
                deleted = true;
            }

            // Delete disabled directory
            var disabledDirectory = workDirectory + "_DISABLED";
            if (Directory.Exists(disabledDirectory))
            {
                Directory.Delete(disabledDirectory, true);
                Console.WriteLine($"[ModArchiveService] Deleted disabled directory: {disabledDirectory}");
                deleted = true;
            }

            // Delete thumbnail
            if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
            {
                File.Delete(thumbnailPath);
                Console.WriteLine($"[ModArchiveService] Deleted thumbnail: {thumbnailPath}");
            }

            // Delete preview
            if (!string.IsNullOrEmpty(previewPath) && File.Exists(previewPath))
            {
                File.Delete(previewPath);
                Console.WriteLine($"[ModArchiveService] Deleted preview: {previewPath}");
            }

            return await Task.FromResult(deleted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModArchiveService] Error deleting mod {sha}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check if mod archive exists
    /// </summary>
    public bool ArchiveExists(string sha)
    {
        return File.Exists(GetArchivePath(sha));
    }

    /// <summary>
    /// Get the path to a mod's archive file
    /// </summary>
    public string GetArchivePath(string sha)
    {
        // Check for .7z first, then .zip
        var sevenZipPath = Path.Combine(_modsDirectory, $"{sha}.7z");
        if (File.Exists(sevenZipPath))
        {
            return sevenZipPath;
        }

        var zipPath = Path.Combine(_modsDirectory, $"{sha}.zip");
        return zipPath;
    }

    /// <summary>
    /// Copy archive file to mods directory
    /// </summary>
    public async Task<string> CopyArchiveAsync(string sourcePath, string sha)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var targetPath = Path.Combine(_modsDirectory, $"{sha}{extension}");

        await Task.Run(() => File.Copy(sourcePath, targetPath, overwrite: true));
        Console.WriteLine($"[ModArchiveService] Copied archive to: {targetPath}");

        return targetPath;
    }
}
