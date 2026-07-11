using D3dxSkinManager.Modules.Core.Helpers;
using System.Diagnostics;

namespace D3dxSkinManager.Modules.System.Services;

/// <summary>
/// Service for file system operations like opening files and directories in explorer
/// </summary>
public interface ISystemFileService
{
    /// <summary>
    /// Opens a file in Windows Explorer with the file selected
    /// </summary>
    /// <param name="filePath">Full path to the file</param>
    Task OpenFileInExplorerAsync(string filePath);

    /// <summary>
    /// Opens a directory in Windows Explorer
    /// </summary>
    /// <param name="directoryPath">Full path to the directory</param>
    Task OpenDirectoryAsync(string directoryPath);

    /// <summary>
    /// Checks if a file exists
    /// </summary>
    /// <param name="filePath">Full path to the file</param>
    /// <returns>True if file exists</returns>
    bool FileExists(string filePath);

    /// <summary>
    /// Checks if a directory exists
    /// </summary>
    /// <param name="directoryPath">Full path to the directory</param>
    /// <returns>True if directory exists</returns>
    bool DirectoryExists(string directoryPath);
}

/// <summary>
/// Implementation of file system operations service
/// </summary>
public class SystemFileService : ISystemFileService
{
    private readonly IPathValidator _pathValidator;

    public SystemFileService(IPathValidator pathValidator)
    {
        _pathValidator = pathValidator;
    }

    /// <summary>
    /// Opens a file in Windows Explorer with the file selected
    /// </summary>
    public async Task OpenFileInExplorerAsync(string filePath)
    {
        _pathValidator.ValidatePathNotEmpty(filePath, nameof(filePath));
        _pathValidator.ValidateFileExists(filePath);

        try
        {
            // Use /select to open explorer with the file selected
            // Don't keep a reference to the process to prevent handle leaks in Windows 11
            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = false,  // Use false to avoid creating wrapper process
                CreateNoWindow = true
            };

            Process.Start(startInfo)?.Dispose();

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open file in explorer: {filePath}", ex);
        }
    }

    /// <summary>
    /// Opens a directory in Windows Explorer
    /// </summary>
    public async Task OpenDirectoryAsync(string directoryPath)
    {
        _pathValidator.ValidatePathNotEmpty(directoryPath, nameof(directoryPath));
        _pathValidator.ValidateDirectoryExists(directoryPath);

        try
        {
            // Open directory using shell handler instead of explorer.exe directly
            // This prevents orphaned explorer.exe processes in Windows 11
            var startInfo = new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true,
                Verb = "open"
            };

            using (Process.Start(startInfo))
            {
                // Process is started and disposed, Windows manages the shell handler
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open directory: {directoryPath}", ex);
        }
    }

    /// <summary>
    /// Checks if a file exists
    /// </summary>
    public bool FileExists(string filePath)
    {
        return !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);
    }

    /// <summary>
    /// Checks if a directory exists
    /// </summary>
    public bool DirectoryExists(string directoryPath)
    {
        return !string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath);
    }
}
