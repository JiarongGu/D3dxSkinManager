using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Helpers;

/// <summary>
/// Mock implementation of IHashHelper for testing purposes
/// Provides predictable hash values and file existence tracking
/// </summary>
public class MockHashHelper
{
    private readonly Mock<IHashHelper> _mock;
    private readonly Dictionary<string, string> _fileHashes = new();
    private readonly HashSet<string> _existingFiles = new();

    public Mock<IHashHelper> Mock => _mock;
    public IHashHelper Object => _mock.Object;

    public MockHashHelper()
    {
        _mock = new Mock<IHashHelper>();
        SetupMocks();
    }

    private void SetupMocks()
    {
        // Setup CalculateFileSHA256Async
        _mock.Setup(x => x.CalculateFileSHA256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((filePath, ct) =>
            {
                if (!_existingFiles.Contains(filePath))
                    throw new System.IO.FileNotFoundException($"File not found: {filePath}");

                // Return a predictable hash based on the file path or a stored hash
                if (_fileHashes.TryGetValue(filePath, out var hash))
                    return Task.FromResult(hash);

                // Generate a predictable hash from the file path
                return Task.FromResult(GeneratePredictableHash(filePath));
            });

        // Setup CalculateSHA256 for byte arrays
        _mock.Setup(x => x.CalculateSHA256(It.IsAny<byte[]>()))
            .Returns<byte[]>(data =>
            {
                // Generate a predictable hash based on array length and first byte
                if (data == null || data.Length == 0)
                    return "EMPTY";

                return $"HASH_{data.Length}_{data[0]:X2}";
            });

        // Setup CalculateSHA256 for strings
        _mock.Setup(x => x.CalculateSHA256(It.IsAny<string>()))
            .Returns<string>(text =>
            {
                if (string.IsNullOrEmpty(text))
                    return "EMPTY";

                // Generate a predictable hash from the text
                return GeneratePredictableHash(text);
            });
    }

    /// <summary>
    /// Add a file to the mock file system with an optional hash
    /// </summary>
    public void AddFile(string filePath, string? hash = null)
    {
        _existingFiles.Add(filePath);
        if (hash != null)
        {
            _fileHashes[filePath] = hash;
        }
    }

    /// <summary>
    /// Set a specific hash value for a file
    /// </summary>
    public void SetFileHash(string filePath, string hash)
    {
        _fileHashes[filePath] = hash;
        _existingFiles.Add(filePath);
    }

    /// <summary>
    /// Remove a file from the mock file system
    /// </summary>
    public void RemoveFile(string filePath)
    {
        _existingFiles.Remove(filePath);
        _fileHashes.Remove(filePath);
    }

    /// <summary>
    /// Clear all files and hashes
    /// </summary>
    public void Clear()
    {
        _existingFiles.Clear();
        _fileHashes.Clear();
    }

    /// <summary>
    /// Check if a file exists in the mock
    /// </summary>
    public bool FileExists(string filePath)
    {
        return _existingFiles.Contains(filePath);
    }

    /// <summary>
    /// Get the hash for a file
    /// </summary>
    public string? GetFileHash(string filePath)
    {
        return _fileHashes.TryGetValue(filePath, out var hash) ? hash : null;
    }

    /// <summary>
    /// Generate a predictable hash from a string
    /// </summary>
    private static string GeneratePredictableHash(string input)
    {
        // Create a predictable "hash" based on the input
        // This is not a real hash but a deterministic value for testing
        var hashCode = input.GetHashCode();
        var bytes = BitConverter.GetBytes(hashCode);

        // Convert to hex string to mimic SHA256 format
        var hexString = BitConverter.ToString(bytes).Replace("-", "");

        // Pad to make it look like a SHA256 hash (64 characters)
        return hexString.PadRight(64, '0').ToUpperInvariant();
    }

    /// <summary>
    /// Setup to return a specific hash for any file matching a pattern
    /// </summary>
    public void SetupPattern(string pattern, string hash)
    {
        _mock.Setup(x => x.CalculateFileSHA256Async(
                It.Is<string>(path => path.Contains(pattern)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(hash));
    }

    /// <summary>
    /// Setup to return sequential hashes for testing
    /// </summary>
    public void SetupSequentialHashes(string prefix = "HASH")
    {
        int counter = 0;
        _mock.Setup(x => x.CalculateFileSHA256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                counter++;
                return Task.FromResult($"{prefix}_{counter:D4}".PadRight(64, '0'));
            });
    }
}