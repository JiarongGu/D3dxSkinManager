using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for file operations
/// </summary>
public interface IFileService
{
    Task<string> CalculateSha256Async(string filePath);
    Task<bool> ExtractArchiveAsync(string archivePath, string targetDirectory);
    Task<bool> CopyDirectoryAsync(string sourceDir, string targetDir, bool overwrite = true);
    Task<bool> DeleteDirectoryAsync(string directory);
}

/// <summary>
/// Service for file operations: hashing, archive extraction, file copying
/// Uses SharpCompress for archive extraction (supports ZIP, RAR, 7Z, TAR, GZIP, etc.)
/// Responsibility: Low-level file system and archive operations
/// </summary>
public class FileService : IFileService
{
    /// <summary>
    /// Calculate SHA256 hash of a file
    /// </summary>
    public async Task<string> CalculateSha256Async(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found for hash calculation", filePath);

        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);

        var hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Extract archive (ZIP, RAR, 7Z, TAR, GZIP, etc.) to directory using SharpCompress
    /// Supports all common archive formats without external dependencies
    /// </summary>
    public async Task<bool> ExtractArchiveAsync(string archivePath, string targetDirectory)
    {
        // Check file existence before async operation to throw FileNotFoundException directly
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found", archivePath);

        return await Task.Run(() =>
        {
            try
            {
                // Create target directory if it doesn't exist
                if (!Directory.Exists(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                Console.WriteLine($"[FileService] Extracting {Path.GetFileName(archivePath)}...");

                // Open archive (auto-detects format: ZIP, RAR, 7Z, TAR, GZIP, etc.)
                using var archive = ArchiveFactory.Open(archivePath);

                var extractionOptions = new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                };

                // Extract all entries
                var entryCount = 0;
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    entry.WriteToDirectory(targetDirectory, extractionOptions);
                    entryCount++;
                }

                Console.WriteLine($"[FileService] Extracted {entryCount} files from {Path.GetFileName(archivePath)}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileService] Extraction failed: {ex.Message}");
                throw new InvalidOperationException($"Archive extraction failed: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Copy directory recursively
    /// </summary>
    public async Task<bool> CopyDirectoryAsync(string sourceDir, string targetDir, bool overwrite = true)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        // Create target directory
        Directory.CreateDirectory(targetDir);

        // Copy all files
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        foreach (var sourceFile in files)
        {
            var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            var targetFile = Path.Combine(targetDir, relativePath);

            // Create subdirectories if needed
            var targetFileDir = Path.GetDirectoryName(targetFile);
            if (targetFileDir != null && !Directory.Exists(targetFileDir))
                Directory.CreateDirectory(targetFileDir);

            // Copy file
            File.Copy(sourceFile, targetFile, overwrite);
        }

        return await Task.FromResult(true);
    }

    /// <summary>
    /// Delete directory recursively
    /// </summary>
    public async Task<bool> DeleteDirectoryAsync(string directory)
    {
        if (!Directory.Exists(directory))
            return true; // Already deleted

        try
        {
            Directory.Delete(directory, recursive: true);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete directory {directory}: {ex.Message}");
            return false;
        }
    }
}
