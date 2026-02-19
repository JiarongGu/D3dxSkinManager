namespace D3dxSkinManager.Modules.Core.Services;

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
}

/// <summary>
/// Implementation of IPathValidator.
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
}
