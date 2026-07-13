using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Tests for the preset overwrite feature (user ask 2026-07-05: "update selected preset with
/// current setting") — Save/Update only created new presets or renamed; OverwriteAsync replaces
/// a preset's mod list with the currently loaded mods, keeping its name.
/// </summary>
public class ModPresetServiceTests
{
    private readonly Mock<IModPresetRepository> _presets = new();
    private readonly Mock<IModRepository> _mods = new();
    private readonly Mock<IModLifecycleService> _lifecycle = new();
    private readonly Mock<IProfileEventBus> _eventBus = new();
    private readonly Mock<ILogHelper> _logger = new();
    private readonly Mock<IProcessRegistry> _registry = new();
    private readonly Mock<ID3dmigotoUserConfigService> _userConfig = new();
    private readonly ModPresetService _service;

    public ModPresetServiceTests()
    {
        _eventBus
            .Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        _userConfig
            .Setup(x => x.CaptureVarLines(It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(Array.Empty<string>());
        // Default: every mod is MANAGED (has a DB row). Tests override per-id to simulate unmanaged mods.
        _mods.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        _service = new ModPresetService(
            _presets.Object, _mods.Object, _lifecycle.Object,
            _eventBus.Object, _logger.Object, _registry.Object, _userConfig.Object);
    }

    [Fact]
    public async Task OverwriteAsync_ReplacesModList_KeepsName_AndEmitsPresetSaved()
    {
        // Arrange: preset holds an old snapshot; two different mods are loaded now
        var entity = new ModPresetEntity
        {
            Id = "P1",
            Name = "My Preset",
            ModIds = JsonSerializer.Serialize(new List<string> { "OLD1" })
        };
        _presets.Setup(r => r.GetByIdAsync("P1")).ReturnsAsync(entity);
        _mods.Setup(r => r.GetLoadedIdsAsync()).ReturnsAsync(new List<string> { "NEW1", "NEW2" });

        ModPresetEntity? saved = null;
        _presets.Setup(r => r.UpdateAsync(It.IsAny<ModPresetEntity>()))
            .Callback<ModPresetEntity>(e => saved = e)
            .ReturnsAsync(true);

        // Act
        var info = await _service.OverwriteAsync("P1");

        // Assert: mod list replaced, name untouched, menu-refresh event emitted
        saved.Should().NotBeNull();
        JsonSerializer.Deserialize<List<string>>(saved!.ModIds).Should().BeEquivalentTo(new[] { "NEW1", "NEW2" });
        info.Name.Should().Be("My Preset");
        info.ModCount.Should().Be(2);
        _eventBus.Verify(x => x.EmitAsync(ModuleNames.MOD, ModEvents.PRESET_SAVED, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task OverwriteAsync_UnknownPreset_ThrowsPresetNotFound()
    {
        _presets.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((ModPresetEntity?)null);

        var act = () => _service.OverwriteAsync("missing");

        (await act.Should().ThrowAsync<OperationException>())
            .Which.Code.Should().Be("PRESET_NOT_FOUND");
    }

    [Fact]
    public async Task OverwriteAsync_NoLoadedMods_Throws_AndLeavesPresetUntouched()
    {
        var entity = new ModPresetEntity
        {
            Id = "P1",
            Name = "My Preset",
            ModIds = JsonSerializer.Serialize(new List<string> { "OLD1" })
        };
        _presets.Setup(r => r.GetByIdAsync("P1")).ReturnsAsync(entity);
        _mods.Setup(r => r.GetLoadedIdsAsync()).ReturnsAsync(new List<string>());

        var act = () => _service.OverwriteAsync("P1");

        (await act.Should().ThrowAsync<OperationException>())
            .Which.Code.Should().Be("PRESET_NO_ACTIVE_MODS");
        _presets.Verify(r => r.UpdateAsync(It.IsAny<ModPresetEntity>()), Times.Never);
    }

    [Fact]
    public async Task OverwriteAsync_StatePreset_RefreshesModStateFromCurrentConfig()
    {
        // User ask: "when you update a preset, a state preset always updates the state." A preset that
        // carries mod state must RE-CAPTURE it from the current d3dx_user.ini on overwrite.
        var entity = new ModPresetEntity
        {
            Id = "P1",
            Name = "My Preset",
            ModIds = JsonSerializer.Serialize(new List<string> { "OLD1" }),
            ModState = JsonSerializer.Serialize(new List<string> { "$\\mods\\old1\\a.ini\\x = 0" }),
        };
        _presets.Setup(r => r.GetByIdAsync("P1")).ReturnsAsync(entity);
        _mods.Setup(r => r.GetLoadedIdsAsync()).ReturnsAsync(new List<string> { "NEW1" });
        _userConfig.Setup(u => u.CaptureVarLines(It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(new[] { "$\\mods\\new1\\b.ini\\y = 1" });

        ModPresetEntity? saved = null;
        _presets.Setup(r => r.UpdateAsync(It.IsAny<ModPresetEntity>()))
            .Callback<ModPresetEntity>(e => saved = e).ReturnsAsync(true);

        await _service.OverwriteAsync("P1");

        _userConfig.Verify(u => u.CaptureVarLines(It.IsAny<IReadOnlyCollection<string>>()), Times.Once,
            "a state preset re-captures its mod state on overwrite");
        saved!.ModState.Should().NotBeNull();
        saved.ModState!.Should().Contain("new1").And.NotContain("old1", "state refreshed from the current config");
    }

    [Fact]
    public async Task SaveAsync_CaptureModState_ReferencesDb_ExcludesUnmanagedMods()
    {
        // GetLoadedIdsAsync scans the deploy folder (includes unmanaged mods). Capture must reference the
        // DB and snapshot state ONLY for MANAGED mods (user directive 2026-07-13).
        _presets.Setup(r => r.GetByNameAsync(It.IsAny<string>())).ReturnsAsync((ModPresetEntity?)null);
        _mods.Setup(r => r.GetLoadedIdsAsync()).ReturnsAsync(new List<string> { "MANAGED", "UNMANAGED" });
        _mods.Setup(r => r.ExistsAsync("MANAGED")).ReturnsAsync(true);
        _mods.Setup(r => r.ExistsAsync("UNMANAGED")).ReturnsAsync(false); // deployed but no DB row
        _presets.Setup(r => r.InsertAsync(It.IsAny<ModPresetEntity>())).ReturnsAsync((ModPresetEntity e) => e);

        IReadOnlyCollection<string>? capturedWith = null;
        _userConfig.Setup(u => u.CaptureVarLines(It.IsAny<IReadOnlyCollection<string>>()))
            .Callback<IReadOnlyCollection<string>>(ids => capturedWith = ids)
            .Returns(new[] { "$\\mods\\managed\\a.ini\\x = 1" });

        await _service.SaveAsync("P", captureModState: true);

        capturedWith.Should().NotBeNull();
        capturedWith!.Should().Contain("MANAGED").And.NotContain("UNMANAGED",
            "capture references the DB — an unmanaged (no DB row) mod's state is never snapshotted");
    }

    [Fact]
    public async Task OverwriteAsync_NonStatePreset_DoesNotCaptureModState()
    {
        var entity = new ModPresetEntity
        {
            Id = "P2",
            Name = "Plain",
            ModIds = JsonSerializer.Serialize(new List<string> { "OLD1" }),
            ModState = null,
        };
        _presets.Setup(r => r.GetByIdAsync("P2")).ReturnsAsync(entity);
        _mods.Setup(r => r.GetLoadedIdsAsync()).ReturnsAsync(new List<string> { "NEW1" });
        _presets.Setup(r => r.UpdateAsync(It.IsAny<ModPresetEntity>())).ReturnsAsync(true);

        await _service.OverwriteAsync("P2");

        _userConfig.Verify(u => u.CaptureVarLines(It.IsAny<IReadOnlyCollection<string>>()), Times.Never,
            "a non-state preset stays non-state on overwrite");
    }

    [Fact]
    public async Task ApplyAsync_SkipsStaleUnmanagedMembers_NotCountedAsFailed()
    {
        // A preset carrying a member with no DB row (deleted / legacy unmanaged) must SKIP it — not try
        // to load it and report "failed" on every apply (#36).
        var entity = new ModPresetEntity
        {
            Id = "P1",
            Name = "My Preset",
            ModIds = JsonSerializer.Serialize(new List<string> { "MANAGED", "STALE" }),
        };
        _presets.Setup(r => r.GetByIdAsync("P1")).ReturnsAsync(entity);
        _mods.Setup(r => r.GetLoadedIdsAsync()).ReturnsAsync(new List<string>());
        _mods.Setup(r => r.ExistsAsync("MANAGED")).ReturnsAsync(true);
        _mods.Setup(r => r.ExistsAsync("STALE")).ReturnsAsync(false);
        _lifecycle.Setup(l => l.LoadAsync(It.IsAny<string>()))
            .ReturnsAsync(new D3dxSkinManager.Modules.Mod.Models.ModLoadResult
            {
                Success = true,
                LoadedModId = "MANAGED",
                UnloadedModIds = new List<string>(),
            });

        var result = await _service.ApplyAsync("P1");

        result.LoadedCount.Should().Be(1);
        result.FailedCount.Should().Be(0, "a stale member is skipped, not failed");
        result.SkippedCount.Should().Be(1);
        _lifecycle.Verify(l => l.LoadAsync("MANAGED"), Times.Once);
        _lifecycle.Verify(l => l.LoadAsync("STALE"), Times.Never, "a non-managed member is never loaded");
    }

    [Fact]
    public async Task SaveAsync_StoresOnlyManagedMods()
    {
        // An unmanaged deployed mod can't be redeployed from a managed archive → don't store it (#36).
        _presets.Setup(r => r.GetByNameAsync(It.IsAny<string>())).ReturnsAsync((ModPresetEntity?)null);
        _mods.Setup(r => r.GetLoadedIdsAsync()).ReturnsAsync(new List<string> { "MANAGED", "UNMANAGED" });
        _mods.Setup(r => r.ExistsAsync("MANAGED")).ReturnsAsync(true);
        _mods.Setup(r => r.ExistsAsync("UNMANAGED")).ReturnsAsync(false);

        ModPresetEntity? saved = null;
        _presets.Setup(r => r.InsertAsync(It.IsAny<ModPresetEntity>()))
            .Callback<ModPresetEntity>(e => saved = e)
            .ReturnsAsync((ModPresetEntity e) => e);

        await _service.SaveAsync("P", captureModState: false);

        saved.Should().NotBeNull();
        JsonSerializer.Deserialize<List<string>>(saved!.ModIds).Should()
            .BeEquivalentTo(new[] { "MANAGED" }, "an unmanaged deployed mod can't be re-applied, so it's not stored");
    }
}
