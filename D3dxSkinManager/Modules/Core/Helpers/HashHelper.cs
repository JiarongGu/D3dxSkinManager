using System.Security.Cryptography;
using Encoding = System.Text.Encoding;

namespace D3dxSkinManager.Modules.Core.Helpers;

public interface IHashHelper
{
    Task<string> CalculateFileSHA256Async(string filePath, CancellationToken cancellationToken = default);
    string CalculateSHA256(byte[] data);
    string CalculateSHA256(string text);

    /// <summary>
    /// One SHA256 over the CONCATENATED contents of <paramref name="filePaths"/> (in order) —
    /// equals hashing the files' bytes joined into a single stream. Unreadable files are skipped
    /// whole (callers hash best-effort sets, e.g. the analyzer's buffer/texture groups).
    /// </summary>
    Task<string> CalculateCombinedSHA256Async(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
}

/// <summary>
/// SHA256 hash calculation for files, byte arrays, and strings.
/// </summary>
public class HashHelper : IHashHelper
{
    public async Task<string> CalculateFileSHA256Async(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        using var sha256 = SHA256.Create();
        using var fileStream = File.OpenRead(filePath);

        var hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken).ConfigureAwait(false);
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

    public async Task<string> CalculateCombinedSHA256Async(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        // Incremental hash — same digest as concatenating all bytes into one stream, without
        // buffering the whole set in memory. A file is read fully before appending so a mid-read
        // failure skips it entirely (never half-appends).
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                sha256.AppendData(bytes);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* unreadable file skipped — best-effort set */ }
        }
        return Convert.ToHexString(sha256.GetHashAndReset());
    }
}
