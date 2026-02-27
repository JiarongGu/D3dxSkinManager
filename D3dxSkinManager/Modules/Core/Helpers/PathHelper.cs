using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Core.Helpers;

public interface IPathHelper
{
    string? ToRelativePath(string? absolutePath);
    string? ToAbsolutePath(string? relativePath);
    bool IsRelativePath(string? path);
    bool IsUnderDataPath(string? absolutePath);
    string? NormalizePath(string? path);
}

/// <summary>
/// Converts between absolute and relative paths (relative to base data folder).
/// </summary>
public class PathHelper : IPathHelper
{
    private readonly IGlobalPathService _globalPathService;

    public PathHelper(IGlobalPathService globalPathService)
    {
        _globalPathService = globalPathService;
    }

    public string? ToRelativePath(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return null;

        try
        {
            var fullPath = Path.GetFullPath(absolutePath);
            var basePathWithSeparator = _globalPathService.BaseDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                        + Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(basePathWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(basePathWithSeparator.Length);
            }

            return absolutePath;
        }
        catch
        {
            return absolutePath;
        }
    }

    public string? ToAbsolutePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        try
        {
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            return Path.GetFullPath(Path.Combine(_globalPathService.BaseDataPath, relativePath));
        }
        catch
        {
            return relativePath;
        }
    }

    public bool IsRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return !Path.IsPathRooted(path);
    }

    public bool IsUnderDataPath(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(absolutePath);
            var basePathWithSeparator = _globalPathService.BaseDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                        + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(basePathWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

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
}
