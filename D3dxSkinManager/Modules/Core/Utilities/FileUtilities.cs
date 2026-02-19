using System.IO;

namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Shared utilities for file operations and formatting
/// </summary>
public static class FileUtilities
{
    /// <summary>
    /// Formats a byte count as a human-readable string (B, KB, MB, GB, TB)
    /// </summary>
    /// <param name="bytes">Number of bytes to format</param>
    /// <returns>Formatted string like "1.5 GB"</returns>
    public static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Calculates the total size of a directory including all subdirectories
    /// </summary>
    /// <param name="path">Directory path to calculate size for</param>
    /// <returns>Total size in bytes</returns>
    public static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        long totalSize = 0;

        try
        {
            // Add file sizes in current directory
            var fileInfos = new DirectoryInfo(path).GetFiles();
            foreach (var fileInfo in fileInfos)
            {
                totalSize += fileInfo.Length;
            }

            // Recursively add subdirectory sizes
            var directories = Directory.GetDirectories(path);
            foreach (var directory in directories)
            {
                totalSize += GetDirectorySize(directory);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we don't have access to
        }
        catch (DirectoryNotFoundException)
        {
            // Directory was deleted during traversal
        }

        return totalSize;
    }
}
