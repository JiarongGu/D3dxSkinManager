using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for file operations
/// </summary>
public interface IFileService
{
    Task<string> CalculateSha256Async(string filePath);
    Task<bool> ExtractArchiveAsync(string archivePath, string targetDirectory);
    Task<bool> CopyFileAsync(string sourceFile, string destinationFile, bool overwrite = false);
    Task<bool> MoveFileAsync(string sourceFile, string destinationFile);
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
    private readonly ILogHelper _logger;

    public FileService(ILogHelper logger)
    {
        _logger = logger;
    }

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

                _logger.Info($"Extracting {Path.GetFileName(archivePath)}...", "FileService");

                // Use Reader API for SharpCompress 0.46+
                using var reader = ReaderFactory.OpenReader(archivePath);

                // Extract all entries
                var entryCount = 0;
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        reader.WriteEntryToDirectory(targetDirectory);
                        entryCount++;
                    }
                }

                _logger.Info($"Extracted {entryCount} files from {Path.GetFileName(archivePath)}", "FileService");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Extraction failed: {ex.Message}", "FileService", ex);
                throw new InvalidOperationException($"Archive extraction failed: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Copy a single file
    /// </summary>
    public async Task<bool> CopyFileAsync(string sourceFile, string destinationFile, bool overwrite = false)
    {
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException($"Source file not found: {sourceFile}");

        // Create destination directory if it doesn't exist
        var destDir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        // Copy file
        File.Copy(sourceFile, destinationFile, overwrite);

        return await Task.FromResult(true);
    }

    /// <summary>
    /// Move a single file
    /// </summary>
    public async Task<bool> MoveFileAsync(string sourceFile, string destinationFile)
    {
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException($"Source file not found: {sourceFile}");

        // Create destination directory if it doesn't exist
        var destDir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        // Move file
        File.Move(sourceFile, destinationFile);

        return await Task.FromResult(true);
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
            _logger.Error($"Failed to delete directory {directory}: {ex.Message}", "FileService", ex);
            return false;
        }
    }
}
