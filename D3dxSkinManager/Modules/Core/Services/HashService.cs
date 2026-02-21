using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for hash calculation service
/// </summary>
public interface IHashService
{
    /// <summary>
    /// Calculate SHA256 hash of a file
    /// </summary>
    /// <param name="filePath">Absolute path to the file</param>
    /// <returns>SHA256 hash as lowercase hexadecimal string (64 characters)</returns>
    Task<string> CalculateFileSHA256Async(string filePath);

    /// <summary>
    /// Calculate SHA256 hash of a byte array
    /// </summary>
    /// <param name="data">Data to hash</param>
    /// <returns>SHA256 hash as lowercase hexadecimal string (64 characters)</returns>
    string CalculateSHA256(byte[] data);

    /// <summary>
    /// Calculate SHA256 hash of a string (UTF-8 encoding)
    /// </summary>
    /// <param name="text">Text to hash</param>
    /// <returns>SHA256 hash as lowercase hexadecimal string (64 characters)</returns>
    string CalculateSHA256(string text);
}

/// <summary>
/// Service for calculating cryptographic hashes
/// Used for file deduplication, integrity checking, and unique identifiers
/// </summary>
public class HashService : IHashService
{
    /// <summary>
    /// Calculate SHA256 hash of a file
    /// </summary>
    /// <param name="filePath">Absolute path to the file</param>
    /// <returns>SHA256 hash as lowercase hexadecimal string (64 characters)</returns>
    /// <exception cref="FileNotFoundException">If file does not exist</exception>
    public async Task<string> CalculateFileSHA256Async(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        using var sha256 = SHA256.Create();
        using var fileStream = File.OpenRead(filePath);

        var hashBytes = await sha256.ComputeHashAsync(fileStream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Calculate SHA256 hash of a byte array
    /// </summary>
    /// <param name="data">Data to hash</param>
    /// <returns>SHA256 hash as lowercase hexadecimal string (64 characters)</returns>
    public string CalculateSHA256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Calculate SHA256 hash of a string (UTF-8 encoding)
    /// </summary>
    /// <param name="text">Text to hash</param>
    /// <returns>SHA256 hash as lowercase hexadecimal string (64 characters)</returns>
    public string CalculateSHA256(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hashBytes = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
