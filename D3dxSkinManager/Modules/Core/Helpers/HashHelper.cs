using System.Security.Cryptography;
using Encoding = System.Text.Encoding;

namespace D3dxSkinManager.Modules.Core.Helpers;

public interface IHashHelper
{
    Task<string> CalculateFileSHA256Async(string filePath);
    string CalculateSHA256(byte[] data);
    string CalculateSHA256(string text);
}

/// <summary>
/// SHA256 hash calculation for files, byte arrays, and strings.
/// </summary>
public class HashHelper : IHashHelper
{
    public async Task<string> CalculateFileSHA256Async(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        using var sha256 = SHA256.Create();
        using var fileStream = File.OpenRead(filePath);

        var hashBytes = await sha256.ComputeHashAsync(fileStream).ConfigureAwait(false);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
    }

    public string CalculateSHA256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
    }

    public string CalculateSHA256(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hashBytes = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
    }
}
