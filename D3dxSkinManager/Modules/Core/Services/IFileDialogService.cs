using System.Collections.Generic;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services
{
    /// <summary>
    /// File dialog filter
    /// </summary>
    public class FileDialogFilter
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Extensions { get; set; } = new();
    }

    /// <summary>
    /// File dialog options
    /// </summary>
    public class FileDialogOptions
    {
        public string? Title { get; set; }
        public string? DefaultPath { get; set; }
        public List<FileDialogFilter>? Filters { get; set; }
        /// <summary>
        /// Optional key to remember the last used path for this dialog type
        /// If provided, the dialog will remember and restore the last location
        /// </summary>
        public string? RememberPathKey { get; set; }
    }

    /// <summary>
    /// File dialog result
    /// </summary>
    public class FileDialogResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Service for native file dialogs
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Open file dialog to select a file
        /// </summary>
        Task<FileDialogResult> OpenFileDialogAsync(FileDialogOptions? options = null);

        /// <summary>
        /// Open folder dialog to select a directory
        /// </summary>
        Task<FileDialogResult> OpenFolderDialogAsync(FileDialogOptions? options = null);

        /// <summary>
        /// Open save file dialog
        /// </summary>
        Task<FileDialogResult> SaveFileDialogAsync(FileDialogOptions? options = null);
    }
}
