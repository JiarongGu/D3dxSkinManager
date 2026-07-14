namespace D3dxSkinManager.Modules.Core.Helpers;

/// <summary>
/// Centralized validation service for file and directory paths.
/// Provides consistent exception handling and error messages.
/// </summary>
public interface IPathValidator
{
    /// <summary>
    /// Validates that a file exists at the specified path.
    /// </summary>
    /// <exception cref="ArgumentException">If path is null or empty</exception>
    /// <exception cref="FileNotFoundException">If file does not exist</exception>
    void ValidateFileExists(string filePath);

    /// <summary>
    /// Validates that a directory exists at the specified path.
    /// </summary>
    /// <exception cref="ArgumentException">If path is null or empty</exception>
    /// <exception cref="DirectoryNotFoundException">If directory does not exist</exception>
    void ValidateDirectoryExists(string directoryPath);

    /// <summary>
    /// Validates that a path is not null or empty.
    /// </summary>
    /// <exception cref="ArgumentException">If path is null or empty</exception>
    void ValidatePathNotEmpty(string path, string paramName = "path");

    /// <summary>
    /// Returns true iff <paramref name="candidate"/>, resolved to a full path, is <paramref name="root"/>
    /// itself or lives underneath it. Pure string logic — does NOT touch the disk. Use this to confine an
    /// untrusted path segment (a manifest field, an IPC-supplied name) before combining it with a trusted
    /// root, defeating <c>..</c> traversal and rooted-segment escapes (a rooted second arg makes
    /// <see cref="Path.Combine(string,string)"/> discard the root). Comparison is case-insensitive
    /// (Windows). A null/empty/unresolvable input returns false.
    /// </summary>
    bool IsPathWithin(string root, string candidate);
}

/// <summary>
/// Implementation of IPathValidationService.
/// </summary>
public class PathValidator : IPathValidator
{
    /// <inheritdoc />
    public void ValidateFileExists(string filePath)
    {
        ValidatePathNotEmpty(filePath, nameof(filePath));

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}", filePath);
        }
    }

    /// <inheritdoc />
    public void ValidateDirectoryExists(string directoryPath)
    {
        ValidatePathNotEmpty(directoryPath, nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }
    }

    /// <inheritdoc />
    public void ValidatePathNotEmpty(string path, string paramName = "path")
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"Path cannot be null or empty", paramName);
        }
    }

    /// <inheritdoc />
    public bool IsPathWithin(string root, string candidate)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string fullRoot;
        string fullCandidate;
        try
        {
            fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            fullCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            // Malformed path (invalid chars, too long, etc.) — treat as not-contained rather than throwing.
            return false;
        }

        if (string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = fullRoot + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
