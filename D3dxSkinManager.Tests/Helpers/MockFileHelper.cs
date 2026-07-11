using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Helpers;

/// <summary>
/// Mock implementation of IFileHelper for testing
/// Uses an in-memory fake file system to simulate file operations without actual I/O
/// </summary>
public class MockFileHelper
{
    private readonly Dictionary<string, string> _fakeFileSystem = new();
    private readonly Mock<IFileHelper> _mock;

    public Mock<IFileHelper> Mock => _mock;
    public IFileHelper Object => _mock.Object;

    public MockFileHelper()
    {
        _mock = new Mock<IFileHelper>();
        SetupMocks();
    }

    private void SetupMocks()
    {
        // DirectoryExists - always returns true unless we implement directory tracking
        _mock.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);

        // FileExists - check in fake file system
        _mock.Setup(x => x.FileExists(It.IsAny<string>()))
            .Returns<string>(path => _fakeFileSystem.ContainsKey(Path.GetFullPath(path)));

        // EnumerateFiles - return files from fake file system
        _mock.Setup(x => x.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns<string, string, SearchOption>((dir, pattern, opt) =>
            {
                var normalizedDir = Path.GetFullPath(dir);
                var dirWithSeparator = normalizedDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                return _fakeFileSystem.Keys
                    .Where(k => k.StartsWith(dirWithSeparator, StringComparison.OrdinalIgnoreCase))
                    .Where(k => opt == SearchOption.TopDirectoryOnly ? !k.Substring(dirWithSeparator.Length).Contains(Path.DirectorySeparatorChar) : true)
                    .Where(k => MatchesPattern(Path.GetFileName(k), pattern))
                    .OrderBy(k => k);
            });

        // GetFiles - return files from fake file system as array
        _mock.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns<string, string, SearchOption>((dir, pattern, opt) =>
            {
                var normalizedDir = Path.GetFullPath(dir);
                return _fakeFileSystem.Keys
                    .Where(k => k.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase))
                    .Where(k => MatchesPattern(Path.GetFileName(k), pattern))
                    .OrderBy(k => k)
                    .ToArray();
            });

        // DeleteFile - remove from fake file system
        _mock.Setup(x => x.DeleteFile(It.IsAny<string>()))
            .Callback<string>(path => _fakeFileSystem.Remove(Path.GetFullPath(path)));

        // MoveFile - move in fake file system
        _mock.Setup(x => x.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, string, bool>((src, dest, overwrite) =>
            {
                var srcPath = Path.GetFullPath(src);
                var destPath = Path.GetFullPath(dest);
                if (_fakeFileSystem.ContainsKey(srcPath))
                {
                    if (overwrite || !_fakeFileSystem.ContainsKey(destPath))
                    {
                        _fakeFileSystem[destPath] = _fakeFileSystem[srcPath];
                        _fakeFileSystem.Remove(srcPath);
                    }
                }
            });

        // CopyFileAsync - copy in fake file system
        _mock.Setup(x => x.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync<string, string, bool, IFileHelper, bool>((src, dest, overwrite) =>
            {
                var srcPath = Path.GetFullPath(src);
                var destPath = Path.GetFullPath(dest);
                if (_fakeFileSystem.ContainsKey(srcPath))
                {
                    if (overwrite || !_fakeFileSystem.ContainsKey(destPath))
                    {
                        _fakeFileSystem[destPath] = _fakeFileSystem[srcPath];
                        return true;
                    }
                }
                return false;
            });

        // DeleteFileAsync - async version of DeleteFile
        _mock.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
            .ReturnsAsync<string, IFileHelper, bool>(path =>
            {
                var fullPath = Path.GetFullPath(path);
                if (_fakeFileSystem.ContainsKey(fullPath))
                {
                    _fakeFileSystem.Remove(fullPath);
                    return true;
                }
                return true; // Return true even if file doesn't exist (matches behavior)
            });

        // DeleteDirectoryAsync - remove every fake file under the directory (recursive)
        _mock.Setup(x => x.DeleteDirectoryAsync(It.IsAny<string>()))
            .ReturnsAsync<string, IFileHelper, bool>(dir =>
            {
                var prefix = Path.GetFullPath(dir)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (var k in _fakeFileSystem.Keys
                    .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    _fakeFileSystem.Remove(k);
                }
                return true;
            });
    }

    /// <summary>
    /// Add a fake file to the file system
    /// </summary>
    public void AddFile(string filePath, string content = "fake-content")
    {
        _fakeFileSystem[Path.GetFullPath(filePath)] = content;
    }

    /// <summary>
    /// Check if a fake file exists
    /// </summary>
    public bool HasFile(string filePath)
    {
        return _fakeFileSystem.ContainsKey(Path.GetFullPath(filePath));
    }

    /// <summary>
    /// Get the content of a fake file
    /// </summary>
    public string? GetFileContent(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        return _fakeFileSystem.TryGetValue(fullPath, out var content) ? content : null;
    }

    /// <summary>
    /// Clear all files from the fake file system
    /// </summary>
    public void Clear()
    {
        _fakeFileSystem.Clear();
    }

    /// <summary>
    /// Get all file paths in the fake file system
    /// </summary>
    public IEnumerable<string> GetAllFiles()
    {
        return _fakeFileSystem.Keys;
    }

    /// <summary>
    /// Simple pattern matching for file names (supports * wildcard)
    /// Handles common patterns like "*.txt", "prefix*", "prefix*.*"
    /// </summary>
    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern == "*" || pattern == "*.*")
            return true;

        // Handle patterns with wildcards
        if (pattern.Contains("*"))
        {
            // Split pattern by asterisks to get the parts that must match
            var parts = pattern.Split('*');
            var currentIndex = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (string.IsNullOrEmpty(part))
                    continue;

                if (i == 0)
                {
                    // First part must match at the beginning
                    if (!fileName.StartsWith(part, StringComparison.OrdinalIgnoreCase))
                        return false;
                    currentIndex = part.Length;
                }
                else if (i == parts.Length - 1)
                {
                    // Last part must match at the end
                    if (!fileName.EndsWith(part, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else
                {
                    // Middle parts must appear in order
                    var index = fileName.IndexOf(part, currentIndex, StringComparison.OrdinalIgnoreCase);
                    if (index == -1)
                        return false;
                    currentIndex = index + part.Length;
                }
            }

            return true;
        }

        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
