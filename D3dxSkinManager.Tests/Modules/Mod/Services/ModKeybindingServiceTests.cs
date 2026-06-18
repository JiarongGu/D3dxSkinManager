using System;
using System.IO;
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
/// Tests for the keybinding write-back (#3 mod .ini editor, easy phase): rebind a key in [Key*]
/// sections and recompress. Uses a temp cache + mocked archive — no real mod data touched.
/// </summary>
public class ModKeybindingServiceTests : IDisposable
{
    private readonly Mock<IProfilePathService> _paths = new();
    private readonly Mock<IModArchiveService> _archive = new();
    private readonly IModOperationQueue _queue = new ModOperationQueue(Mock.Of<ILogHelper>());
    private readonly string _cacheRoot;
    private readonly ModKeybindingService _service;

    public ModKeybindingServiceTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), "d3dx-kb-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheRoot);
        _paths.Setup(p => p.CacheModsDirectory).Returns(_cacheRoot);
        _archive.Setup(a => a.CompressCacheToArchiveAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        _service = new ModKeybindingService(_paths.Object, _archive.Object, _queue);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_cacheRoot)) Directory.Delete(_cacheRoot, true); } catch { }
    }

    private string WriteModIni(string modId, string content)
    {
        var dir = Path.Combine(_cacheRoot, modId);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "mod.ini");
        File.WriteAllText(file, content);
        return file;
    }

    private const string Sample = @"[Constants]
global persist $swapkey0 = 1

[KeySwap0]
condition = $active0 == 1
key = 0
type = cycle
$swapkey0 = 1,0

[TextureOverride_VB]
hash = 64a6b06d
";

    [Fact]
    public async Task UpdateKeybinding_RewritesKeyLine_AndRecompresses()
    {
        var file = WriteModIni("MODA", Sample);

        var changed = await _service.UpdateKeybindingAsync("MODA", "0", "5");

        changed.Should().Be(1);
        var text = await File.ReadAllTextAsync(file);
        text.Should().Contain("key = 5");
        text.Should().NotContain("key = 0");
        // Untouched: the hash override line must remain.
        text.Should().Contain("hash = 64a6b06d");
        _archive.Verify(a => a.CompressCacheToArchiveAsync("MODA", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateKeybinding_OnlyTouchesKeySections()
    {
        // A 'key =' line outside a [Key*] section must NOT be rewritten.
        var file = WriteModIni("MODB", "[Other]\nkey = 0\n\n[KeyX]\nkey = 0\n");

        var changed = await _service.UpdateKeybindingAsync("MODB", "0", "j");

        changed.Should().Be(1); // only the [KeyX] one
        var lines = await File.ReadAllLinesAsync(file);
        lines.Should().Contain("key = 0");  // [Other] preserved
        lines.Should().Contain("key = j");  // [KeyX] rebound
    }

    [Fact]
    public async Task UpdateKeybinding_Throws_WhenNotExtracted()
    {
        var act = () => _service.UpdateKeybindingAsync("GHOST", "0", "5");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("MOD_NOT_EXTRACTED");
    }

    [Fact]
    public async Task UpdateKeybinding_Throws_WhenKeyNotFound()
    {
        WriteModIni("MODC", Sample);
        var act = () => _service.UpdateKeybindingAsync("MODC", "Z", "5");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("KEYBINDING_NOT_FOUND");
    }
}
