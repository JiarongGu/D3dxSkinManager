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
    private readonly Mock<IModCacheService> _cache = new();
    private readonly Mock<IModArchiveService> _archive = new();
    private readonly Mock<IModRepository> _repo = new();
    private readonly IModOperationQueue _queue = new ModOperationQueue(Mock.Of<ILogHelper>());
    private readonly string _cacheRoot;
    private readonly ModKeybindingService _service;

    public ModKeybindingServiceTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), "d3dx-kb-test-" + Guid.NewGuid().ToString("N"));
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
        _service = new ModKeybindingService(_cache.Object, _archive.Object, _queue, _repo.Object);
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
    public async Task UpdateKeybinding_RewritesKeyLine_AndPatchesArchiveEntry()
    {
        var file = WriteModIni("MODA", Sample);

        var changed = await _service.UpdateKeybindingAsync("MODA", "0", "5");

        changed.Should().Be(1);
        var text = await File.ReadAllTextAsync(file);
        text.Should().Contain("key = 5");
        text.Should().NotContain("key = 0");
        // Untouched: the hash override line must remain.
        text.Should().Contain("hash = 64a6b06d");
        // Fast path: only the changed .ini is patched into the archive (forward-slash entry path), no full recompress.
        _archive.Verify(a => a.UpdateFileInArchiveAsync("MODA", file, "mod.ini"), Times.Once);
        _archive.Verify(a => a.CompressCacheToArchiveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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

    private const string TwoKeys = @"[KeySwap0]
key = 0
type = cycle

[KeySwap1]
key = 9
type = cycle
";

    [Fact]
    public async Task Reorder_SavesOrderToMetadata()
    {
        var entity = new D3dxSkinManager.Modules.Mod.Entities.ModEntity { Id = "MODR" };
        _repo.Setup(r => r.GetByIdAsync("MODR")).ReturnsAsync(entity);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<D3dxSkinManager.Modules.Mod.Entities.ModEntity>())).ReturnsAsync(true);

        await _service.ReorderKeybindingsAsync("MODR", new System.Collections.Generic.List<string> { "9", "0" });

        // Order persisted into the mod's Metadata JSON (not the .ini).
        entity.Metadata.Should().Contain("keybindingOrder").And.Contain("\"9\"").And.Contain("\"0\"");
        entity.Metadata!.IndexOf("\"9\"", StringComparison.Ordinal)
            .Should().BeLessThan(entity.Metadata!.IndexOf("\"0\"", StringComparison.Ordinal));
        _repo.Verify(r => r.UpdateAsync(entity), Times.Once);
        // .ini is NOT touched (works across files via metadata instead).
        _archive.Verify(a => a.UpdateFileInArchiveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Parse_AppliesSavedOrderFromMetadata()
    {
        WriteModIni("MODR2", TwoKeys); // file order: 0, 9
        _repo.Setup(r => r.GetByIdAsync("MODR2")).ReturnsAsync(new D3dxSkinManager.Modules.Mod.Entities.ModEntity
        {
            Id = "MODR2",
            Metadata = "{\"keybindingOrder\":[\"9\",\"0\"]}",
        });

        var result = await _service.ParseKeybindingsAsync("MODR2");

        // Saved order wins over file order → "9" first.
        result.Select(k => k.Key).Should().ContainInOrder("9", "0");
    }

    [Fact]
    public async Task Parse_KeepsEveryKeyLineOfASection()
    {
        // B4 regression (user report 2026-07-05): a [Key*] section may carry MULTIPLE `key =` lines
        // (keyboard + controller share state, per the 3DMigoto key doc) — later lines used to
        // overwrite the first, so all but one binding silently vanished from the editor.
        WriteModIni("MODMK", "[KeySwap]\nkey = no_ctrl alt j\nkey = XB_LEFT_SHOULDER\ntype = cycle\n$swap = 0,1\n");
        _repo.Setup(r => r.GetByIdAsync("MODMK")).ReturnsAsync((D3dxSkinManager.Modules.Mod.Entities.ModEntity?)null);

        var result = await _service.ParseKeybindingsAsync("MODMK");

        result.Should().ContainSingle();
        result[0].Key.Should().Be("no_ctrl alt j");
        result[0].KeyDisplay.Should().Be("ALT + J");
        result[0].AdditionalKeys.Should().ContainSingle().Which.Should().Be("XB_LEFT_SHOULDER");
        result[0].Type.Should().Be("cycle");
        result[0].Variable.Should().Be("$swap");
    }

    [Fact]
    public async Task Parse_SkipsFullwidthCommentLines()
    {
        // Real mods mix ASCII `;` and fullwidth `；` comments — a `；key = ...` line is dead config.
        WriteModIni("MODFW", "[KeySwap]\n；key = VK_F1\nkey = 9\ntype = cycle\n");
        _repo.Setup(r => r.GetByIdAsync("MODFW")).ReturnsAsync((D3dxSkinManager.Modules.Mod.Entities.ModEntity?)null);

        var result = await _service.ParseKeybindingsAsync("MODFW");

        result.Should().ContainSingle();
        result[0].Key.Should().Be("9");
        result[0].AdditionalKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateKeybinding_RebindsAnAdditionalKeyLine_LeavingTheFirstIntact()
    {
        // Each `key =` line of a multi-key section is independently rebindable.
        var file = WriteModIni("MODMK2", "[KeySwap]\nkey = j\nkey = XB_LEFT_SHOULDER\ntype = cycle\n");

        var changed = await _service.UpdateKeybindingAsync("MODMK2", "XB_LEFT_SHOULDER", "XB_RIGHT_SHOULDER");

        changed.Should().Be(1);
        var text = await File.ReadAllTextAsync(file);
        text.Should().Contain("key = j");
        text.Should().Contain("key = XB_RIGHT_SHOULDER");
        text.Should().NotContain("XB_LEFT_SHOULDER");
    }

    [Fact]
    public async Task Parse_StripsInlineComment_AndRebindPreservesIt()
    {
        // A `key = VK_F1 ; hair toggle` line: the parse shows the clean chord (IniParser strips inline
        // comments) and the rebind must match on the STRIPPED value + keep the comment on the line.
        var file = WriteModIni("MODIC", "[KeySwap]\nkey = VK_F1 ; hair toggle\ntype = cycle\n");
        _repo.Setup(r => r.GetByIdAsync("MODIC")).ReturnsAsync((D3dxSkinManager.Modules.Mod.Entities.ModEntity?)null);

        var parsed = await _service.ParseKeybindingsAsync("MODIC");
        parsed.Should().ContainSingle().Which.Key.Should().Be("VK_F1");

        var changed = await _service.UpdateKeybindingAsync("MODIC", "VK_F1", "VK_F2");

        changed.Should().Be(1);
        (await File.ReadAllTextAsync(file)).Should().Contain("key = VK_F2 ; hair toggle");
    }

    // ---- Add / remove alternate key line (keyboard + controller co-exist on one hotkey) -------------

    [Fact]
    public async Task AddKeyLine_AppendsAControllerAlternate_AfterTheKeyboardKey()
    {
        // A single-key hotkey gains a controller alternate: the [Key*] section keeps `key = j` AND gains
        // `key = XB_A`, so it fires from either. Inserted right after the last key line, indent matched.
        var file = WriteModIni("MODA", "[KeySwap]\nkey = j\ntype = cycle\n$swap = 0,1\n");

        var added = await _service.AddKeyLineAsync("MODA", "j", "XB_A");

        added.Should().Be(1);
        var lines = await File.ReadAllLinesAsync(file);
        lines.Should().ContainInOrder("key = j", "key = XB_A", "type = cycle");
        _archive.Verify(a => a.UpdateFileInArchiveAsync("MODA", file, "mod.ini"), Times.Once);
    }

    [Fact]
    public async Task AddKeyLine_Throws_WhenTargetKeyNotFound()
    {
        WriteModIni("MODB", "[KeySwap]\nkey = j\ntype = cycle\n");
        var act = () => _service.AddKeyLineAsync("MODB", "z", "XB_A");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("KEYBINDING_NOT_FOUND");
    }

    [Fact]
    public async Task AddKeyLine_Throws_WhenAdditionAlreadyPresent()
    {
        WriteModIni("MODC", "[KeySwap]\nkey = j\nkey = XB_A\ntype = cycle\n");
        var act = () => _service.AddKeyLineAsync("MODC", "j", "XB_A");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("KEYBINDING_ALREADY_BOUND");
    }

    [Fact]
    public async Task AddKeyLine_OnlyTouchesTheMatchingKeySection()
    {
        // A `key = j` outside a [Key*] section, and a different [Key*] section, must be left alone.
        var file = WriteModIni("MODD", "[Other]\nkey = j\n\n[KeyA]\nkey = j\n\n[KeyB]\nkey = 9\n");

        var added = await _service.AddKeyLineAsync("MODD", "j", "XB_B");

        added.Should().Be(1); // only [KeyA]
        var lines = await File.ReadAllLinesAsync(file);
        // [Other]'s key line untouched; the alternate lands in [KeyA] (after its key = j), not [KeyB].
        lines.Should().ContainInOrder("[Other]", "key = j", "[KeyA]", "key = j", "key = XB_B", "[KeyB]", "key = 9");
        var text = string.Join("\n", lines);
        text.IndexOf("XB_B", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("[KeyB]", StringComparison.Ordinal), "the alternate lands in [KeyA], not [KeyB]");
    }

    [Fact]
    public async Task RemoveKeyLine_RemovesTheAlternate_KeepingThePrimary()
    {
        var file = WriteModIni("MODE", "[KeySwap]\nkey = j\nkey = XB_A\ntype = cycle\n");

        var removed = await _service.RemoveKeyLineAsync("MODE", "XB_A");

        removed.Should().Be(1);
        var text = await File.ReadAllTextAsync(file);
        text.Should().Contain("key = j");
        text.Should().NotContain("XB_A");
        _archive.Verify(a => a.UpdateFileInArchiveAsync("MODE", file, "mod.ini"), Times.Once);
    }

    [Fact]
    public async Task RemoveKeyLine_Throws_WhenItWouldLeaveNoKey()
    {
        // The section's ONLY key line — removing it would leave a dead keybinding, so refuse.
        WriteModIni("MODF", "[KeySwap]\nkey = j\ntype = cycle\n");
        var act = () => _service.RemoveKeyLineAsync("MODF", "j");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("KEYBINDING_LAST_KEY");
    }

    [Fact]
    public async Task RemoveKeyLine_Throws_WhenValueAbsent()
    {
        WriteModIni("MODG", "[KeySwap]\nkey = j\nkey = XB_A\n");
        var act = () => _service.RemoveKeyLineAsync("MODG", "XB_Y");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("KEYBINDING_NOT_FOUND");
    }

    // ---- SetKeyLines: rewrite a binding's WHOLE key set (the row "edit mode" save) ------------------

    [Fact]
    public async Task SetKeyLines_ReplacesTheWholeKeySet_KeepingOtherLines()
    {
        // Edit mode: a one-key hotkey is saved with three keys (rebound primary + a keyboard alt + a
        // controller). type/$var lines stay; the key-line block is replaced in place.
        var file = WriteModIni("MODA", "[KeySwap]\nkey = j\ntype = cycle\n$swap = 0,1\n");

        var written = await _service.SetKeyLinesAsync("MODA", "j", new[] { "k", "no_ctrl alt l", "XB_A" });

        written.Should().Be(3);
        var lines = await File.ReadAllLinesAsync(file);
        lines.Should().ContainInOrder("[KeySwap]", "key = k", "key = no_ctrl alt l", "key = XB_A", "type = cycle", "$swap = 0,1");
        lines.Should().NotContain("key = j");
        _archive.Verify(a => a.UpdateFileInArchiveAsync("MODA", file, "mod.ini"), Times.Once);
    }

    [Fact]
    public async Task SetKeyLines_CanReduceToASingleKey()
    {
        var file = WriteModIni("MODB", "[KeySwap]\nkey = j\nkey = XB_A\ntype = cycle\n");

        var written = await _service.SetKeyLinesAsync("MODB", "j", new[] { "j" });

        written.Should().Be(1);
        var text = await File.ReadAllTextAsync(file);
        text.Should().Contain("key = j");
        text.Should().NotContain("XB_A");
    }

    [Fact]
    public async Task SetKeyLines_DropsBlankEntries_AndThrowsWhenAllBlank()
    {
        WriteModIni("MODC", "[KeySwap]\nkey = j\ntype = cycle\n");
        var act = () => _service.SetKeyLinesAsync("MODC", "j", new[] { "", "   " });
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("KEYBINDING_LAST_KEY");
    }

    [Fact]
    public async Task SetKeyLines_Throws_WhenAnchorNotFound()
    {
        WriteModIni("MODD", "[KeySwap]\nkey = j\n");
        var act = () => _service.SetKeyLinesAsync("MODD", "z", new[] { "k" });
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("KEYBINDING_NOT_FOUND");
    }

    [Fact]
    public async Task SetKeyLines_OnlyRewritesTheAnchoredSection()
    {
        var file = WriteModIni("MODE", "[KeyA]\nkey = j\n\n[KeyB]\nkey = 9\ntype = cycle\n");

        await _service.SetKeyLinesAsync("MODE", "j", new[] { "k", "XB_A" });

        var lines = await File.ReadAllLinesAsync(file);
        lines.Should().ContainInOrder("[KeyA]", "key = k", "key = XB_A", "[KeyB]", "key = 9", "type = cycle");
        // [KeyB] keeps exactly its one key line.
        lines.Count(l => l.Trim() == "key = 9").Should().Be(1);
    }

    [Fact]
    public async Task Parse_ConditionLine_DoesNotBecomeTheCycleVariable()
    {
        // `condition = $active == 1` used to be misread by a value-side regex as Variable=$active,
        // CycleValues="= 1" when the section had no real `$var =` assignment (IniParser migration guard).
        WriteModIni("MODCOND", "[KeySwap]\ncondition = $active == 1\nkey = 9\ntype = cycle\n");
        _repo.Setup(r => r.GetByIdAsync("MODCOND")).ReturnsAsync((D3dxSkinManager.Modules.Mod.Entities.ModEntity?)null);

        var result = await _service.ParseKeybindingsAsync("MODCOND");

        result.Should().ContainSingle();
        result[0].Variable.Should().BeNullOrEmpty();
        result[0].CycleValues.Should().BeNullOrEmpty();
    }
}
