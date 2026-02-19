using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Settings.Services;
using D3dxSkinManager.Modules.Settings.Models;
using Moq;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Settings;

/// <summary>
/// Unit tests for GlobalSettingsService
/// Tests settings persistence, caching, and field updates
/// </summary>
public class GlobalSettingsServiceTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly GlobalSettingsService _service;
    private readonly Mock<IPathHelper> _mockPathHelper;

    public GlobalSettingsServiceTests()
    {
        // Arrange - Create temp directory for each test
        _testDataPath = Path.Combine(Path.GetTempPath(), $"GlobalSettingsServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataPath);

        // Setup mock PathHelper
        _mockPathHelper = new Mock<IPathHelper>();
        _mockPathHelper.Setup(x => x.BaseDataPath).Returns(_testDataPath);

        _service = new GlobalSettingsService(_mockPathHelper.Object);
    }

    public void Dispose()
    {
        // Cleanup - Delete temp directory after test
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, recursive: true);
        }
    }

    [Fact]
    public async Task GetSettingsAsync_WhenFileDoesNotExist_ShouldCreateDefaultSettings()
    {
        // Act
        var settings = await _service.GetSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.Theme.Should().Be("light");
        settings.LogLevel.Should().Be("info");
        settings.AnnotationLevel.Should().Be("all");
        settings.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetSettingsAsync_WhenFileDoesNotExist_ShouldPersistDefaultSettings()
    {
        // Act
        var settings = await _service.GetSettingsAsync();

        // Assert - File should now exist in settings subfolder
        var filePath = Path.Combine(_testDataPath, "settings", "global.json");
        File.Exists(filePath).Should().BeTrue();

        var fileContent = await File.ReadAllTextAsync(filePath);
        fileContent.Should().Contain("\"theme\"");
        fileContent.Should().Contain("\"logLevel\"");
        fileContent.Should().Contain("\"annotationLevel\"");
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldPersistChanges()
    {
        // Arrange
        var newSettings = new GlobalSettings
        {
            Theme = "dark",
            LogLevel = "DEBUG",
            AnnotationLevel = "more"
        };

        // Act
        await _service.UpdateSettingsAsync(newSettings);

        // Create new service instance to ensure reading from file
        var newService = new GlobalSettingsService(_mockPathHelper.Object);
        var retrieved = await newService.GetSettingsAsync();

        // Assert
        retrieved.Theme.Should().Be("dark");
        retrieved.LogLevel.Should().Be("DEBUG");
        retrieved.AnnotationLevel.Should().Be("more");
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateLastUpdatedTimestamp()
    {
        // Arrange
        var initialSettings = await _service.GetSettingsAsync();
        var initialTimestamp = initialSettings.LastUpdated;

        // Wait a bit to ensure timestamp difference
        await Task.Delay(100);

        var newSettings = new GlobalSettings
        {
            Theme = "dark",
            LogLevel = "DEBUG",
            AnnotationLevel = "more"
        };

        // Act
        await _service.UpdateSettingsAsync(newSettings);
        var retrieved = await _service.GetSettingsAsync();

        // Assert
        retrieved.LastUpdated.Should().BeAfter(initialTimestamp);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenCalledTwice_ShouldReturnCachedValue()
    {
        // Act
        var first = await _service.GetSettingsAsync();
        var second = await _service.GetSettingsAsync();

        // Assert - Same object reference = cached
        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task GetSettingsAsync_AfterUpdate_ShouldReturnUpdatedValue()
    {
        // Arrange
        var initial = await _service.GetSettingsAsync();
        initial.Theme.Should().Be("light");

        var newSettings = new GlobalSettings
        {
            Theme = "dark",
            LogLevel = "INFO",
            AnnotationLevel = "all"
        };

        // Act
        await _service.UpdateSettingsAsync(newSettings);
        var retrieved = await _service.GetSettingsAsync();

        // Assert
        retrieved.Theme.Should().Be("dark");
    }

    [Theory]
    [InlineData("theme", "dark")]
    [InlineData("theme", "light")]
    [InlineData("theme", "auto")]
    [InlineData("logLevel", "DEBUG")]
    [InlineData("logLevel", "INFO")]
    [InlineData("logLevel", "ERROR")]
    [InlineData("annotationLevel", "all")]
    [InlineData("annotationLevel", "more")]
    [InlineData("annotationLevel", "less")]
    [InlineData("annotationLevel", "off")]
    public async Task UpdateSettingAsync_WithValidKeyValue_ShouldUpdateField(string key, string value)
    {
        // Act
        await _service.UpdateSettingAsync(key, value);
        var settings = await _service.GetSettingsAsync();

        // Assert
        var property = typeof(GlobalSettings).GetProperty(
            key,
            System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );
        property.Should().NotBeNull();
        var actualValue = property!.GetValue(settings) as string;
        actualValue.Should().Be(value);
    }

    [Fact]
    public async Task UpdateSettingAsync_WithInvalidKey_ShouldThrowArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateSettingAsync("invalidKey", "value"));
    }

    [Fact]
    public async Task UpdateSettingAsync_ShouldPersistToFile()
    {
        // Act
        await _service.UpdateSettingAsync("theme", "dark");

        // Create new service instance to ensure reading from file
        var newService = new GlobalSettingsService(_mockPathHelper.Object);
        var settings = await newService.GetSettingsAsync();

        // Assert
        settings.Theme.Should().Be("dark");
    }

    [Fact]
    public async Task ResetSettingsAsync_ShouldRestoreDefaults()
    {
        // Arrange - Change settings
        await _service.UpdateSettingAsync("theme", "dark");
        await _service.UpdateSettingAsync("logLevel", "DEBUG");
        await _service.UpdateSettingAsync("annotationLevel", "none");

        // Act
        await _service.ResetSettingsAsync();
        var settings = await _service.GetSettingsAsync();

        // Assert
        settings.Theme.Should().Be("light");
        settings.LogLevel.Should().Be("info");
        settings.AnnotationLevel.Should().Be("all");
    }

    [Fact]
    public async Task ResetSettingsAsync_ShouldPersistToFile()
    {
        // Arrange
        await _service.UpdateSettingAsync("theme", "dark");

        // Act
        await _service.ResetSettingsAsync();

        // Create new service instance
        var newService = new GlobalSettingsService(_mockPathHelper.Object);
        var settings = await newService.GetSettingsAsync();

        // Assert
        settings.Theme.Should().Be("light");
    }

    [Fact]
    public async Task GetSettingsAsync_WithCorruptedFile_ShouldHandleGracefully()
    {
        // Arrange - Create corrupted JSON file
        var filePath = Path.Combine(_testDataPath, "global.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json }");

        // Act - This should either throw or create new default settings
        var act = async () => await _service.GetSettingsAsync();

        // Assert - Should either succeed with defaults or throw meaningful exception
        // (Behavior depends on implementation - adjust based on actual behavior)
        var result = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new Task[10];

        // Act - Multiple concurrent updates
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks[i] = Task.Run(async () =>
            {
                await _service.UpdateSettingAsync("theme", index % 2 == 0 ? "dark" : "light");
            });
        }

        await Task.WhenAll(tasks);

        // Assert - Should complete without exceptions
        var settings = await _service.GetSettingsAsync();
        settings.Should().NotBeNull();
        settings.Theme.Should().BeOneOf("dark", "light");
    }

    [Fact]
    public async Task UpdateSettingAsync_WithCaseInsensitiveKey_ShouldWork()
    {
        // Act
        await _service.UpdateSettingAsync("THEME", "dark");
        var settings = await _service.GetSettingsAsync();

        // Assert
        settings.Theme.Should().Be("dark");
    }
}
