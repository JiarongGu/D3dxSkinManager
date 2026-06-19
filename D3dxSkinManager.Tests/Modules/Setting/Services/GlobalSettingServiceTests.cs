using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Setting.Models;
using D3dxSkinManager.Modules.Setting.Services;

namespace D3dxSkinManager.Tests.Modules.Setting.Services;

/// <summary>
/// Tests for GlobalSettingService — persistence to global.json, single-field updates (lowercased),
/// cache invalidation (re-read reflects writes), AppEnvironment log-level wiring, and change events.
/// Uses a real MemoryCache + a temp settings file; deps are mocked.
/// </summary>
public class GlobalSettingServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private readonly Mock<IAppEnvironment> _appEnv = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly GlobalSettingService _service;

    public GlobalSettingServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-gss-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "global.json");

        _appEnv.SetupAllProperties();
        _eventBus
            .Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.GlobalSettingsFilePath).Returns(_path);
        paths.Setup(p => p.EnsureDirectoriesExist());

        _service = new GlobalSettingService(paths.Object, _appEnv.Object, _cache, _eventBus.Object);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNoFile_ReturnsDefaultsAndPersistsThem()
    {
        File.Exists(_path).Should().BeFalse();

        var settings = await _service.GetSettingsAsync();

        settings.Should().NotBeNull();
        settings.Theme.Should().Be("dark"); // GlobalSettings default
        File.Exists(_path).Should().BeTrue(); // default written to disk
    }

    [Fact]
    public async Task UpdateSettingAsync_Theme_LowercasesAndPersists()
    {
        await _service.UpdateSettingAsync("theme", "LIGHT");

        // Re-read goes through cache invalidation → reflects the new value from disk.
        (await _service.GetSettingsAsync()).Theme.Should().Be("light");
    }

    [Fact]
    public async Task UpdateSettingAsync_LogLevel_UpdatesAppEnvironment()
    {
        await _service.UpdateSettingAsync("loglevel", "Debug");

        _appEnv.Object.MinimumLogLevel.Should().Be(LogLevel.Debug);
    }

    [Fact]
    public async Task GetSettingsAsync_AutoUpdateCheck_DefaultsToFalse()
    {
        (await _service.GetSettingsAsync()).AutoUpdateCheck.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSettingAsync_AutoUpdateCheck_PersistsBoolean()
    {
        await _service.UpdateSettingAsync("autoUpdateCheck", "true");
        (await _service.GetSettingsAsync()).AutoUpdateCheck.Should().BeTrue();

        await _service.UpdateSettingAsync("autoUpdateCheck", "false");
        (await _service.GetSettingsAsync()).AutoUpdateCheck.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSettingAsync_UnknownKey_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateSettingAsync("bogus", "x"));
    }

    [Fact]
    public async Task UpdateSettingAsync_EmitsChangedEvent()
    {
        await _service.UpdateSettingAsync("theme", "light");

        _eventBus.Verify(
            x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ResetSettingsAsync_RestoresDefaults()
    {
        await _service.UpdateSettingAsync("theme", "light");

        await _service.ResetSettingsAsync();

        (await _service.GetSettingsAsync()).Theme.Should().Be("dark");
    }

    public void Dispose()
    {
        _cache.Dispose();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }
}
