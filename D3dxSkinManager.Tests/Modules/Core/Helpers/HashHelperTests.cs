using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Modules.Core.Helpers;

/// <summary>
/// HashHelper — the combined-hash contract matters most: ModAnalysisService's duplicate detection
/// compares these digests against rows persisted by OLDER scans, so the digest must stay byte-for-byte
/// "SHA256 of the files' concatenated contents, uppercase hex" forever.
/// </summary>
public class HashHelperTests : IDisposable
{
    private readonly string _dir;
    private readonly HashHelper _helper = new();

    public HashHelperTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hash-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private async Task<string> WriteFileAsync(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    [Fact]
    public async Task CombinedHash_EqualsHashOfConcatenatedBytes()
    {
        var a = new byte[] { 1, 2, 3 };
        var b = new byte[] { 4, 5 };
        var fileA = await WriteFileAsync("a.buf", a);
        var fileB = await WriteFileAsync("b.buf", b);

        var combined = await _helper.CalculateCombinedSHA256Async(new[] { fileA, fileB });

        var concat = new byte[] { 1, 2, 3, 4, 5 };
        combined.Should().Be(_helper.CalculateSHA256(concat), "digest = SHA256 over the files' bytes joined in order");
    }

    [Fact]
    public async Task CombinedHash_IsOrderSensitive()
    {
        var fileA = await WriteFileAsync("a.buf", new byte[] { 1 });
        var fileB = await WriteFileAsync("b.buf", new byte[] { 2 });

        var ab = await _helper.CalculateCombinedSHA256Async(new[] { fileA, fileB });
        var ba = await _helper.CalculateCombinedSHA256Async(new[] { fileB, fileA });

        ab.Should().NotBe(ba);
    }

    [Fact]
    public async Task CombinedHash_SkipsUnreadableFiles_Whole()
    {
        var fileA = await WriteFileAsync("a.buf", new byte[] { 1, 2, 3 });
        var missing = Path.Combine(_dir, "missing.buf");

        var withMissing = await _helper.CalculateCombinedSHA256Async(new[] { fileA, missing });
        var withoutMissing = await _helper.CalculateCombinedSHA256Async(new[] { fileA });

        withMissing.Should().Be(withoutMissing, "an unreadable file is skipped entirely, never half-appended");
    }

    [Fact]
    public async Task CombinedHash_UppercaseHex_MatchesFileHashFormat()
    {
        var file = await WriteFileAsync("a.buf", new byte[] { 42 });

        var combined = await _helper.CalculateCombinedSHA256Async(new[] { file });
        var single = await _helper.CalculateFileSHA256Async(file);

        combined.Should().Be(single, "one file combined == that file's own hash, same uppercase-hex format");
        combined.Should().MatchRegex("^[0-9A-F]{64}$");
    }
}
