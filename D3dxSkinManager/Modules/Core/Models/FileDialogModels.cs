using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Core.Models;

/// <summary>
/// File dialog filter
/// </summary>
public class FileDialogFilter
{
    public string Name { get; set; } = string.Empty;
    public List<string> Extensions { get; set; } = new();
}

/// <summary>
/// File dialog configuration options
/// Allows frontend to fully control dialog behavior
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

    /// <summary>
    /// Allow both files and folders to be selected (for folder dialog)
    /// When true, uses OpenFileDialog with folder support instead of FolderBrowserDialog
    /// </summary>
    public bool AllowFileSelection { get; set; }

    /// <summary>
    /// Check if the selected file exists (OpenFileDialog.CheckFileExists)
    /// Default: true for file dialogs, false for folder dialogs with AllowFileSelection
    /// </summary>
    public bool? CheckFileExists { get; set; }

    /// <summary>
    /// Check if the path exists (OpenFileDialog.CheckPathExists)
    /// Default: true
    /// </summary>
    public bool? CheckPathExists { get; set; }

    /// <summary>
    /// Validate file names according to Windows naming rules (OpenFileDialog.ValidateNames)
    /// Default: true for file dialogs, false for folder dialogs with AllowFileSelection
    /// </summary>
    public bool? ValidateNames { get; set; }

    /// <summary>
    /// Initial filename to show in the dialog
    /// Default: empty string
    /// </summary>
    public string? FileName { get; set; }
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
