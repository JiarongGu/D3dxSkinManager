using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Service for file system operations like opening files and directories in explorer
/// </summary>
public interface IFileSystemService
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
    /// Opens a file with its default associated application
    /// </summary>
    /// <param name="filePath">Full path to the file</param>
    Task OpenFileAsync(string filePath);

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
public class FileSystemService : IFileSystemService
{
    private readonly IPathValidator _pathValidator;

    public FileSystemService(IPathValidator pathValidator)
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
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
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
            Process.Start("explorer.exe", $"\"{directoryPath}\"");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open directory: {directoryPath}", ex);
        }
    }

    /// <summary>
    /// Opens a file with its default associated application
    /// </summary>
    public async Task OpenFileAsync(string filePath)
    {
        _pathValidator.ValidatePathNotEmpty(filePath, nameof(filePath));
        _pathValidator.ValidateFileExists(filePath);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open file: {filePath}", ex);
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
