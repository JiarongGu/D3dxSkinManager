using System.Security.Cryptography;

namespace D3dxSkinManager.Modules.Core.Helpers;

public interface IFileHelper
{
    Task<bool> CopyFileAsync(string sourceFile, string destinationFile, bool overwrite = false);
    Task<bool> MoveFileAsync(string sourceFile, string destinationFile);
    Task<bool> CopyDirectoryAsync(string sourceDir, string targetDir, bool overwrite = true);
    Task<bool> DeleteDirectoryAsync(string directory);
    Task<bool> CreateDirectoryAsync(string directory);
    bool FileExists(string filePath);
    bool DirectoryExists(string directoryPath);
    Task<bool> DeleteFileAsync(string filePath);
    string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
}

/// <summary>
/// File system operations: hashing, copying, moving, directory management.
/// </summary>
public class FileHelper : IFileHelper
{
    private readonly ILogHelper _logger;

    public FileHelper(ILogHelper logger)
    {
        _logger = logger;
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

        return await Task.FromResult(true).ConfigureAwait(false);
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

        return await Task.FromResult(true).ConfigureAwait(false);
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

        return await Task.FromResult(true).ConfigureAwait(false);
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
            return await Task.FromResult(true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to delete directory {directory}: {ex.Message}", "FileService", ex);
            return false;
        }
    }

    public Task<bool> CreateDirectoryAsync(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create directory {directory}: {ex.Message}", "FileService", ex);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Check if a file exists
    /// </summary>
    public bool FileExists(string filePath)
    {
        return File.Exists(filePath);
    }

    /// <summary>
    /// Check if a directory exists
    /// </summary>
    public bool DirectoryExists(string directoryPath)
    {
        return Directory.Exists(directoryPath);
    }

    /// <summary>
    /// Delete a file
    /// </summary>
    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return await Task.FromResult(true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to delete file {filePath}: {ex.Message}", "FileHelper", ex);
            return false;
        }
    }

    /// <summary>
    /// Get files in a directory
    /// </summary>
    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(path))
            return Array.Empty<string>();

        return Directory.GetFiles(path, searchPattern, searchOption);
    }
}
