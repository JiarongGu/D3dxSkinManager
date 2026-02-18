using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services
{
    /// <summary>
    /// Implementation of file system operations service
    /// </summary>
    public class FileSystemService : IFileSystemService
    {
        /// <summary>
        /// Opens a file in Windows Explorer with the file selected
        /// </summary>
        public async Task OpenFileInExplorerAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

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
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

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
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}", filePath);

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
}
