using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Modules.Core.Helpers;

/// <summary>
/// Real-7z integration test for the single-file archive update (the fast keybinding/.ini write-back
/// path). Proves append-mode REPLACES the matching entry (not duplicates) and leaves others intact.
/// Requires libs/7z.dll (present in the test bin).
/// </summary>
public class ArchiveHelperUpdateTests : IDisposable
{
    private readonly string _root;
    private readonly ArchiveHelper _helper = new(Mock.Of<ILogHelper>());

    public ArchiveHelperUpdateTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "d3dx-arc-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public async Task UpdateFileInArchive_ReplacesEntry_NotDuplicate_OthersIntact()
    {
        // Arrange: a mod folder with a nested .ini + a sibling file, compressed to an archive.
        var src = Path.Combine(_root, "src");
        Directory.CreateDirectory(Path.Combine(src, "sub"));
        var iniRel = Path.Combine("sub", "mod.ini");
        await File.WriteAllTextAsync(Path.Combine(src, iniRel), "[KeyX]\nkey = 0\n");
        await File.WriteAllTextAsync(Path.Combine(src, "other.txt"), "keep-me");

        var archive = Path.Combine(_root, "mod.7z");
        await _helper.CompressFolderAsync(src, archive);

        // Act: patch just the .ini entry (forward-slash key, as the service passes it).
        var newIni = Path.Combine(_root, "new.ini");
        await File.WriteAllTextAsync(newIni, "[KeyX]\nkey = 5\n");
        await _helper.UpdateFileInArchiveAsync(archive, newIni, "sub/mod.ini");

        // Assert: extract fresh and verify the entry was replaced (not duplicated) + sibling intact.
        var outDir = Path.Combine(_root, "out");
        var res = _helper.ExtractArchive(archive, outDir);
        res.Success.Should().BeTrue();

        var extractedInis = Directory.GetFiles(outDir, "mod.ini", SearchOption.AllDirectories);
        extractedInis.Should().HaveCount(1, "append must replace the entry, not create a duplicate");
        (await File.ReadAllTextAsync(extractedInis[0])).Should().Contain("key = 5").And.NotContain("key = 0");
        File.Exists(Path.Combine(outDir, "other.txt")).Should().BeTrue();
        (await File.ReadAllTextAsync(Path.Combine(outDir, "other.txt"))).Should().Be("keep-me");
    }

    [Fact]
    public async Task ExtractArchive_PasswordProtected_ExtractsWithPassword_FailsWithout()
    {
        // Arrange: a source folder compressed WITH a password (remote sites ship passworded zips —
        // e.g. huihui's 解压密码). A dummy helper compress first initializes the native 7z.dll.
        var src = Path.Combine(_root, "psrc");
        Directory.CreateDirectory(src);
        await File.WriteAllTextAsync(Path.Combine(src, "mod.ini"), "[KeyX]\nkey = 0\n");
        await _helper.CompressFolderAsync(src, Path.Combine(_root, "init-dummy.7z"));

        var archive = Path.Combine(_root, "locked.7z");
        var compressor = new global::SharpSevenZip.SharpSevenZipCompressor
        {
            ArchiveFormat = global::SharpSevenZip.OutArchiveFormat.SevenZip,
        };
        compressor.CompressDirectory(src, archive, "hui-secret");

        // Correct password → extracts.
        var okDir = Path.Combine(_root, "pok");
        var result = _helper.ExtractArchive(archive, okDir, "hui-secret");
        result.Success.Should().BeTrue();
        (await File.ReadAllTextAsync(Path.Combine(okDir, "mod.ini"))).Should().Contain("key = 0");

        // No password → fails, and the failure is classified as a password error (the remote import
        // maps it to REMOTE_ARCHIVE_PASSWORD).
        var failDir = Path.Combine(_root, "pfail");
        var act = () => _helper.ExtractArchive(archive, failDir);
        var ex = act.Should().Throw<Exception>().Which;
        ArchiveHelper.IsPasswordError(ex).Should().BeTrue($"message was: {ex.Message}");
    }

    [Fact]
    public async Task ExtractArchiveRecursive_UnwrapsNestedArchives_ThroughDisguisedExtension()
    {
        // Build mod.ini -> inner.7z -> outer.zip, then RENAME outer.zip to "1.mp4" (huihui's disguise).
        // Recursive extract must detect by magic bytes and unwrap BOTH layers to the real mod.ini.
        var content = Path.Combine(_root, "content");
        Directory.CreateDirectory(content);
        await File.WriteAllTextAsync(Path.Combine(content, "mod.ini"), "[KeyX]\nkey = 1\n");
        var inner = Path.Combine(_root, "inner.7z");
        await _helper.CompressFolderAsync(content, inner);

        var innerHolder = Path.Combine(_root, "holder");
        Directory.CreateDirectory(innerHolder);
        File.Copy(inner, Path.Combine(innerHolder, "inner.7z"));
        var outerZip = Path.Combine(_root, "outer.zip");
        await _helper.CompressFolderAsync(innerHolder, outerZip, ArchiveFormat.Zip);

        var disguised = Path.Combine(_root, "1.mp4"); // the "safe keep" fake extension
        File.Move(outerZip, disguised);

        var outDir = Path.Combine(_root, "rout");
        var layers = await _helper.ExtractArchiveRecursiveAsync(disguised, outDir);

        layers.Should().BeGreaterThanOrEqualTo(2, "outer(.mp4/zip) + inner(.7z) are both unwrapped");
        var inis = Directory.GetFiles(outDir, "mod.ini", SearchOption.AllDirectories);
        inis.Should().ContainSingle("the real mod file is reached through both wrappers");
        (await File.ReadAllTextAsync(inis[0])).Should().Contain("key = 1");
        // No archive files survive in the output — the recompressed mod holds content, not wrappers.
        Directory.GetFiles(outDir, "*", SearchOption.AllDirectories).Any(_helper.IsArchiveFile)
            .Should().BeFalse();
    }

    [Fact]
    public void ExtractArchive_UnencryptedArchive_IgnoresSuppliedPassword()
    {
        // A supplied password must be harmless for unencrypted archives — the import pipeline
        // extracts plain-first, but a user-typed password may still be handed to a plain archive.
        var src = Path.Combine(_root, "usrc");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "plain");
        var archive = Path.Combine(_root, "plain.7z");
        _helper.CompressFolderAsync(src, archive).GetAwaiter().GetResult();

        var outDir = Path.Combine(_root, "uout");
        var result = _helper.ExtractArchive(archive, outDir, "wrong-password-does-not-matter");

        result.Success.Should().BeTrue();
        File.ReadAllText(Path.Combine(outDir, "a.txt")).Should().Be("plain");
    }
}
