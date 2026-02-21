using System;
using System.IO;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for file transfer operations
/// </summary>
public interface IFileTransferService
{
    /// <summary>
    /// Copy external file to target directory with SHA-256 based naming and deduplication
    /// </summary>
    /// <param name="sourcePath">Absolute path to source file (can be anywhere on filesystem)</param>
    /// <param name="targetDirectory">Absolute path to target directory</param>
    /// <param name="preserveExtension">Whether to preserve file extension in target filename</param>
    /// <returns>Relative path (from data folder) to the copied file, or null if source doesn't exist</returns>
    Task<string?> CopyToManagedDirectoryAsync(string sourcePath, string targetDirectory, bool preserveExtension = true);

    /// <summary>
    /// Check if a file is external to a specific directory
    /// </summary>
    /// <param name="filePath">Absolute path to check</param>
    /// <param name="directory">Absolute directory path to check against</param>
    /// <returns>True if file is external to the directory</returns>
    bool IsExternalToDirectory(string filePath, string directory);
}

/// <summary>
/// Service for managed file transfer operations with deduplication
/// Handles copying external files into managed directories with SHA-256 naming
/// </summary>
public class FileTransferService : IFileTransferService
{
    private readonly IFileService _fileService;
    private readonly IHashService _hashService;
    private readonly IPathHelper _pathHelper;

    public FileTransferService(
        IFileService fileService,
        IHashService hashService,
        IPathHelper pathHelper)
    {
        _fileService = fileService;
        _hashService = hashService;
        _pathHelper = pathHelper;
    }

    /// <summary>
    /// Copy external file to target directory with SHA-256 based naming and deduplication
    /// </summary>
    /// <param name="sourcePath">Absolute path to source file (can be anywhere on filesystem)</param>
    /// <param name="targetDirectory">Absolute path to target directory</param>
    /// <param name="preserveExtension">Whether to preserve file extension in target filename</param>
    /// <returns>Relative path (from data folder) to the copied file, or null if source doesn't exist</returns>
    public async Task<string?> CopyToManagedDirectoryAsync(
        string sourcePath,
        string targetDirectory,
        bool preserveExtension = true)
    {
        if (string.IsNullOrEmpty(sourcePath))
            return null;

        // Convert to absolute path if needed
        var absoluteSourcePath = Path.IsPathRooted(sourcePath)
            ? sourcePath
            : _pathHelper.ToAbsolutePath(sourcePath);

        if (absoluteSourcePath == null || !File.Exists(absoluteSourcePath))
        {
            Console.WriteLine($"[FileTransferService] Source file not found: {sourcePath}");
            return null;
        }

        Console.WriteLine($"[FileTransferService] Processing file: {absoluteSourcePath}");

        // Normalize target directory
        var normalizedTargetDir = Path.GetFullPath(targetDirectory);
        Console.WriteLine($"[FileTransferService] Target directory: {normalizedTargetDir}");

        // Always calculate SHA-256 to determine target filename
        var extension = preserveExtension ? Path.GetExtension(sourcePath) : "";
        var fileSha = await _hashService.CalculateFileSHA256Async(absoluteSourcePath);
        Console.WriteLine($"[FileTransferService] File SHA-256: {fileSha}");

        var fileName = $"{fileSha}{extension}";
        var targetPath = Path.Combine(normalizedTargetDir, fileName);
        Console.WriteLine($"[FileTransferService] Target path: {targetPath}");

        // Ensure target directory exists
        Directory.CreateDirectory(normalizedTargetDir);

        // Check if SHA-named file already exists in destination (deduplication)
        if (File.Exists(targetPath))
        {
            Console.WriteLine($"[FileTransferService] File already exists at destination (deduplication), skipping copy");
        }
        else
        {
            Console.WriteLine($"[FileTransferService] Copying file to destination...");
            await _fileService.CopyFileAsync(absoluteSourcePath, targetPath, overwrite: false);
            Console.WriteLine($"[FileTransferService] Copy complete");
        }

        // Return relative path for portability
        var relativePath = _pathHelper.ToRelativePath(targetPath);
        Console.WriteLine($"[FileTransferService] Relative path: {relativePath}");
        return relativePath;
    }

    /// <summary>
    /// Check if a file is external to a specific directory
    /// </summary>
    /// <param name="filePath">Absolute path to check</param>
    /// <param name="directory">Absolute directory path to check against</param>
    /// <returns>True if file is external to the directory</returns>
    public bool IsExternalToDirectory(string filePath, string directory)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(directory))
            return true;

        var normalizedFilePath = Path.GetFullPath(filePath);
        var normalizedDirectory = Path.GetFullPath(directory);

        return !normalizedFilePath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
