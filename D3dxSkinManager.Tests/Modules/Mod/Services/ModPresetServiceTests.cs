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
    private readonly ModPresetService _service;

    public ModPresetServiceTests()
    {
        _eventBus
            .Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _service = new ModPresetService(
            _presets.Object, _mods.Object, _lifecycle.Object,
            _eventBus.Object, _logger.Object, _registry.Object);
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
}
