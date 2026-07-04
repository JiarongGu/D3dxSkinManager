using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Tests for the general mod config (.ini) editor: parse + editability classification, namespace
/// parsing, and the single-line write-back (fast single-file archive patch + server-side read-only
/// guard). Uses a temp cache + mocked archive — no real mod data touched.
/// </summary>
public class ModIniServiceTests : IDisposable
{
    private readonly Mock<IModCacheService> _cache = new();
    private readonly Mock<IModArchiveService> _archive = new();
    private readonly IModOperationQueue _queue = new ModOperationQueue(Mock.Of<ILogHelper>());
    private readonly string _cacheRoot;
    private readonly ModIniService _service;

    public ModIniServiceTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), "d3dx-ini-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheRoot);
        // GetCachePath contract: active {id} dir, else DISABLED-{id}, else null — over the temp root.
        _cache.Setup(c => c.GetCachePath(It.IsAny<string>())).Returns((string id) =>
        {
            var active = Path.Combine(_cacheRoot, id);
            if (Directory.Exists(active)) return active;
            var disabled = Path.Combine(_cacheRoot, $"DISABLED-{id}");
            return Directory.Exists(disabled) ? disabled : null;
        });
        _archive.Setup(a => a.UpdateFileInArchiveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        _service = new ModIniService(_cache.Object, _archive.Object, _queue);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_cacheRoot)) Directory.Delete(_cacheRoot, true); } catch { }
    }

    private string WriteModIni(string modId, string fileName, string content)
    {
        var dir = Path.Combine(_cacheRoot, modId);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, fileName);
        File.WriteAllText(file, content);
        return file;
    }

    private const string Sample = @"namespace = Foo\Bar

[Constants]
global persist $swapkey0 = 1
global persist $hair = 0

[KeySwap0]
condition = $active0 == 1
key = 0
type = cycle
$swapkey0 = 1,0

[TextureOverrideBody]
hash = 64a6b06d
run = CommandListSkinTexture
";

    [Fact]
    public async Task GetIniFiles_ClassifiesSectionsAndParsesNamespace()
    {
        WriteModIni("MODA", "mod.ini", Sample);

        var files = await _service.GetIniFilesAsync("MODA");

        files.Should().HaveCount(1);
        var file = files[0];
        file.Namespace.Should().Be(@"Foo\Bar");
        file.RelativePath.Should().Be("mod.ini");

        var constants = file.Sections.Single(s => s.Name == "Constants");
        constants.Advanced.Should().BeFalse();
        constants.Entries.Should().OnlyContain(e => e.Editable);

        var keySection = file.Sections.Single(s => s.Name == "KeySwap0");
        keySection.Advanced.Should().BeFalse();
        keySection.Entries.Single(e => e.Key == "key").Editable.Should().BeTrue();
        keySection.Entries.Single(e => e.Key == "type").Value.Should().Be("cycle");

        var texOverride = file.Sections.Single(s => s.Name == "TextureOverrideBody");
        texOverride.Advanced.Should().BeTrue();
        texOverride.Entries.Should().OnlyContain(e => !e.Editable && e.LockReason == "advancedSection");
    }

    [Fact]
    public async Task GetIniFiles_ReturnsEmpty_WhenNotExtracted()
    {
        (await _service.GetIniFilesAsync("GHOST")).Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateEntry_RewritesValue_PreservesKeyAndComment_PatchesArchive()
    {
        // A constant default with an inline comment + indentation — both must be preserved.
        var file = WriteModIni("MODB", "mod.ini", "[Constants]\n  global persist $hair = 0 ; the hair toggle\n");

        // Line index 1 = the constant line.
        var line = await _service.UpdateEntryAsync("MODB", "mod.ini", 1, "2");

        line.Should().Be("  global persist $hair = 2 ; the hair toggle");
        var text = await File.ReadAllTextAsync(file);
        text.Should().Contain("  global persist $hair = 2 ; the hair toggle");
        _archive.Verify(a => a.UpdateFileInArchiveAsync("MODB", file, "mod.ini"), Times.Once);
        _archive.Verify(a => a.CompressCacheToArchiveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEntry_Throws_WhenEditingAdvancedHashLine()
    {
        WriteModIni("MODC", "mod.ini", Sample);
        // The hash line is inside [TextureOverrideBody] (advanced) — must be rejected.
        var hashLine = Array.FindIndex(await File.ReadAllLinesAsync(Path.Combine(_cacheRoot, "MODC", "mod.ini")),
            l => l.TrimStart().StartsWith("hash"));

        var act = () => _service.UpdateEntryAsync("MODC", "mod.ini", hashLine, "deadbeef");

        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("INI_ENTRY_READONLY");
        _archive.Verify(a => a.UpdateFileInArchiveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEntry_Throws_WhenNotExtracted()
    {
        var act = () => _service.UpdateEntryAsync("GHOST", "mod.ini", 0, "1");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("MOD_NOT_EXTRACTED");
    }

    [Fact]
    public async Task UpdateEntry_Throws_WhenFileMissing()
    {
        WriteModIni("MODD", "mod.ini", Sample);
        var act = () => _service.UpdateEntryAsync("MODD", "nope.ini", 0, "1");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("INI_FILE_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateEntry_Throws_WhenLineOutOfRange()
    {
        WriteModIni("MODE", "mod.ini", Sample);
        var act = () => _service.UpdateEntryAsync("MODE", "mod.ini", 9999, "1");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("INI_LINE_INVALID");
    }

    [Fact]
    public async Task UpdateEntry_RejectsPathTraversal()
    {
        WriteModIni("MODF", "mod.ini", Sample);
        var act = () => _service.UpdateEntryAsync("MODF", "../../escape.ini", 0, "1");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("INI_FILE_NOT_FOUND");
    }
}
