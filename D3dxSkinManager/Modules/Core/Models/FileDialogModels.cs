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
    /// <summary>
    /// Allow both files and folders to be selected (for folder dialog)
    /// When true, folder dialog will accept both folder and file selections
    /// </summary>
    public bool AllowFileSelection { get; set; }
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
