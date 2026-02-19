using System;
using System.IO;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for path conversion operations
/// </summary>
public interface IPathHelper
{
    string? ToRelativePath(string? absolutePath);
    string? ToAbsolutePath(string? relativePath);
    bool IsRelativePath(string? path);
    bool IsUnderDataPath(string? absolutePath);
    string? NormalizePath(string? path);
    string BaseDataPath { get; }
}

/// <summary>
/// Helper class for converting between absolute and relative paths
/// All paths stored in database and configuration should be relative to the base data path
/// This ensures portability when the application folder is moved or renamed
/// </summary>
public class PathHelper : IPathHelper
{
    private readonly string _baseDataPath;

    public PathHelper(string baseDataPath)
    {
        _baseDataPath = Path.GetFullPath(baseDataPath);
    }

    /// <summary>
    /// Convert an absolute path to a relative path (relative to data folder)
    /// Returns null if the path is null, empty, or not under the data path
    /// </summary>
    /// <param name="absolutePath">Absolute file path</param>
    /// <returns>Relative path from data folder, or null if not convertible</returns>
    public string? ToRelativePath(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return null;

        try
        {
            var fullPath = Path.GetFullPath(absolutePath);
            var basePathWithSeparator = _baseDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                        + Path.DirectorySeparatorChar;

            // Check if the absolute path is under the base data path
            if (fullPath.StartsWith(basePathWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                // Remove the base path prefix to get relative path
                return fullPath.Substring(basePathWithSeparator.Length);
            }

            // Path is outside data folder - keep as absolute for external paths (like WorkDirectory pointing to game folder)
            return absolutePath;
        }
        catch
        {
            return absolutePath;
        }
    }

    /// <summary>
    /// Convert a relative path (relative to data folder) to an absolute path
    /// </summary>
    /// <param name="relativePath">Relative path from data folder</param>
    /// <returns>Absolute file path, or null if input is null/empty</returns>
    public string? ToAbsolutePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        try
        {
            // If already absolute, return as-is
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            // Combine with base data path
            return Path.GetFullPath(Path.Combine(_baseDataPath, relativePath));
        }
        catch
        {
            return relativePath;
        }
    }

    /// <summary>
    /// Check if a path is relative (not rooted)
    /// </summary>
    public bool IsRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return !Path.IsPathRooted(path);
    }

    /// <summary>
    /// Check if an absolute path is under the base data path
    /// </summary>
    public bool IsUnderDataPath(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(absolutePath);
            var basePathWithSeparator = _baseDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                        + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(basePathWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Normalize a path to use consistent separators and format
    /// </summary>
    public string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    /// <summary>
    /// Get the base data path
    /// </summary>
    public string BaseDataPath => _baseDataPath;
}
