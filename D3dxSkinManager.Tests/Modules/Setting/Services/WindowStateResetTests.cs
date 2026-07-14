using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Setting;
using D3dxSkinManager.Modules.Setting.Models;
using D3dxSkinManager.Modules.Setting.Services;

namespace D3dxSkinManager.Tests.Modules.Setting.Services;

/// <summary>
/// Locks the facade-to-service move of the window-state reset: the service (not the facade) now owns
/// clearing the saved window fields, persisting, and emitting SETTING/WINDOW_STATE_RESET.
/// </summary>
public class WindowStateResetTests
{
    [Fact]
    public async Task ResetWindowStateAsync_ClearsFields_Persists_AndEmits()
    {
        var settings = new GlobalSettings();
        settings.Window.X = 100;
        settings.Window.Y = 50;
        settings.Window.Width = 1600;
        settings.Window.Height = 900;
        settings.Window.Maximized = true;

        var settingsService = new Mock<IGlobalSettingService>();
        settingsService.Setup(s => s.GetSettingsAsync()).ReturnsAsync(settings);
        GlobalSettings? saved = null;
        settingsService.Setup(s => s.UpdateSettingsAsync(It.IsAny<GlobalSettings>()))
            .Callback<GlobalSettings>(s => saved = s)
            .Returns(Task.CompletedTask);

        var eventBus = new Mock<IEventBus>();
        eventBus.Setup(e => e.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var service = new WindowStateService(settingsService.Object, eventBus.Object);

        await service.ResetWindowStateAsync();

        saved.Should().NotBeNull();
        saved!.Window.X.Should().BeNull();
        saved.Window.Y.Should().BeNull();
        saved.Window.Width.Should().BeNull();
        saved.Window.Height.Should().BeNull();
        saved.Window.Maximized.Should().BeFalse();

        eventBus.Verify(
            e => e.EmitAsync(ModuleNames.SETTING, SettingEvents.WINDOW_STATE_RESET, It.IsAny<object?>(), It.IsAny<string?>()),
            Times.Once);
    }
}
