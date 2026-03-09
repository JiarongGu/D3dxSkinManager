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
    void DeleteFile(string filePath);
    void MoveFile(string sourceFile, string destinationFile, bool overwrite = false);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
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

    /// <summary>
    /// Delete file/directory with retry logic for locked files
    /// More robust than DeleteFileAsync - handles transient file locks
    /// </summary>
    public async Task<bool> DeleteWithRetryAsync(
        string path,
        int maxRetries = 3,
        int delayMs = 100)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    await Task.Run(() => File.Delete(path));
                }
                else if (Directory.Exists(path))
                {
                    await Task.Run(() => Directory.Delete(path, recursive: true));
                }
                return true;
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                _logger.Warn($"Delete attempt {attempt + 1} failed for '{path}', retrying...", "FileHelper");
                await Task.Delay(delayMs);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to delete '{path}' after {attempt + 1} attempts: {ex.Message}", "FileHelper", ex);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Synchronous version - Ensures directory exists, creates if missing
    /// Useful for inline calls: EnsureDirectoryExists(path);
    /// </summary>
    public void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Synchronous delete file - for simple operations where async is not needed
    /// </summary>
    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Synchronous move file - for simple operations where async is not needed
    /// </summary>
    public void MoveFile(string sourceFile, string destinationFile, bool overwrite = false)
    {
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException($"Source file not found: {sourceFile}");

        // Create destination directory if it doesn't exist
        var destDir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        // If overwrite and destination exists, delete it first
        if (overwrite && File.Exists(destinationFile))
        {
            File.Delete(destinationFile);
        }

        // Move file
        File.Move(sourceFile, destinationFile);
    }

    /// <summary>
    /// Enumerate files in a directory - more efficient than GetFiles for large directories
    /// </summary>
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(path))
            return Enumerable.Empty<string>();

        return Directory.EnumerateFiles(path, searchPattern, searchOption);
    }
}
