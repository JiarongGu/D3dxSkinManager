using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services
{
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
}
