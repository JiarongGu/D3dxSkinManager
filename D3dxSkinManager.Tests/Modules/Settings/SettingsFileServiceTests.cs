using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Settings.Services;

namespace D3dxSkinManager.Tests.Modules.Settings;

/// <summary>
/// Unit tests for SettingsFileService
/// Tests generic JSON file storage with security and validation
/// </summary>
public class SettingsFileServiceTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly SettingsFileService _service;
    private readonly string _settingsDirectory;

    public SettingsFileServiceTests()
    {
        // Arrange - Create temp directory for each test
        _testDataPath = Path.Combine(Path.GetTempPath(), $"SettingsFileServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataPath);
        _service = new SettingsFileService(_testDataPath);
        _settingsDirectory = Path.Combine(_testDataPath, "settings");
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
    public async Task SaveSettingsFileAsync_WithValidJson_ShouldCreateFile()
    {
        // Arrange
        var filename = "test_config";
        var content = "{\"key\":\"value\",\"number\":42}";

        // Act
        await _service.SaveSettingsFileAsync(filename, content);

        // Assert
        var filePath = Path.Combine(_settingsDirectory, $"{filename}.json");
        File.Exists(filePath).Should().BeTrue();

        var savedContent = await File.ReadAllTextAsync(filePath);
        savedContent.Should().Be(content);
    }

    [Fact]
    public async Task SaveSettingsFileAsync_WithInvalidJson_ShouldThrowArgumentException()
    {
        // Arrange
        var filename = "invalid_config";
        var invalidJson = "{ invalid json }";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveSettingsFileAsync(filename, invalidJson));
    }

    [Fact]
    public async Task SaveSettingsFileAsync_ShouldOverwriteExistingFile()
    {
        // Arrange
        var filename = "test_config";
        var originalContent = "{\"version\":1}";
        var newContent = "{\"version\":2}";

        // Act
        await _service.SaveSettingsFileAsync(filename, originalContent);
        await _service.SaveSettingsFileAsync(filename, newContent);

        // Assert
        var filePath = Path.Combine(_settingsDirectory, $"{filename}.json");
        var savedContent = await File.ReadAllTextAsync(filePath);
        savedContent.Should().Be(newContent);
    }

    [Fact]
    public async Task GetSettingsFileAsync_WhenFileExists_ShouldReturnContent()
    {
        // Arrange
        var filename = "test_config";
        var content = "{\"key\":\"value\"}";
        await _service.SaveSettingsFileAsync(filename, content);

        // Act
        var retrieved = await _service.GetSettingsFileAsync(filename);

        // Assert
        retrieved.Should().Be(content);
    }

    [Fact]
    public async Task GetSettingsFileAsync_WhenFileDoesNotExist_ShouldReturnNull()
    {
        // Act
        var retrieved = await _service.GetSettingsFileAsync("nonexistent");

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetSettingsFileAsync_WithCorruptedJson_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var filename = "corrupted";
        var filePath = Path.Combine(_settingsDirectory, $"{filename}.json");
        Directory.CreateDirectory(_settingsDirectory);
        await File.WriteAllTextAsync(filePath, "{ invalid json }");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetSettingsFileAsync(filename));
    }

    [Fact]
    public async Task DeleteSettingsFileAsync_WhenFileExists_ShouldDeleteFile()
    {
        // Arrange
        var filename = "test_config";
        await _service.SaveSettingsFileAsync(filename, "{\"key\":\"value\"}");

        // Act
        await _service.DeleteSettingsFileAsync(filename);

        // Assert
        var filePath = Path.Combine(_settingsDirectory, $"{filename}.json");
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSettingsFileAsync_WhenFileDoesNotExist_ShouldNotThrow()
    {
        // Act & Assert
        await _service.DeleteSettingsFileAsync("nonexistent");
        // Should complete without exception
    }

    [Fact]
    public async Task SettingsFileExistsAsync_WhenFileExists_ShouldReturnTrue()
    {
        // Arrange
        var filename = "test_config";
        await _service.SaveSettingsFileAsync(filename, "{\"key\":\"value\"}");

        // Act
        var exists = await _service.SettingsFileExistsAsync(filename);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task SettingsFileExistsAsync_WhenFileDoesNotExist_ShouldReturnFalse()
    {
        // Act
        var exists = await _service.SettingsFileExistsAsync("nonexistent");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ListSettingsFilesAsync_WhenNoFiles_ShouldReturnEmptyArray()
    {
        // Act
        var files = await _service.ListSettingsFilesAsync();

        // Assert
        files.Should().BeEmpty();
    }

    [Fact]
    public async Task ListSettingsFilesAsync_WithMultipleFiles_ShouldReturnAllFilenames()
    {
        // Arrange
        await _service.SaveSettingsFileAsync("config1", "{\"a\":1}");
        await _service.SaveSettingsFileAsync("config2", "{\"b\":2}");
        await _service.SaveSettingsFileAsync("config3", "{\"c\":3}");

        // Act
        var files = await _service.ListSettingsFilesAsync();

        // Assert
        files.Should().HaveCount(3);
        files.Should().Contain("config1");
        files.Should().Contain("config2");
        files.Should().Contain("config3");
    }

    [Fact]
    public async Task ListSettingsFilesAsync_ShouldReturnFilenamesWithoutExtension()
    {
        // Arrange
        await _service.SaveSettingsFileAsync("myconfig", "{\"key\":\"value\"}");

        // Act
        var files = await _service.ListSettingsFilesAsync();

        // Assert
        files.Should().Contain("myconfig");
        files.Should().NotContain("myconfig.json");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task SaveSettingsFileAsync_WithInvalidFilename_ShouldThrowArgumentException(string? filename)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveSettingsFileAsync(filename!, "{\"key\":\"value\"}"));
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    [InlineData("test/../../../etc/passwd")]
    [InlineData("test\\..\\..\\windows")]
    public async Task SaveSettingsFileAsync_WithPathTraversal_ShouldThrowArgumentException(string filename)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveSettingsFileAsync(filename, "{\"key\":\"value\"}"));
    }

    [Fact]
    public async Task SaveSettingsFileAsync_WithGlobalSettingsFilename_ShouldThrowArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveSettingsFileAsync("global_settings", "{\"key\":\"value\"}"));
    }

    [Theory]
    [InlineData("config/subdir")]
    [InlineData("config\\subdir")]
    public async Task SaveSettingsFileAsync_WithSlashesInFilename_ShouldThrowArgumentException(string filename)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveSettingsFileAsync(filename, "{\"key\":\"value\"}"));
    }

    [Theory]
    [InlineData("test<file")]
    [InlineData("test>file")]
    [InlineData("test|file")]
    [InlineData("test:file")]
    [InlineData("test*file")]
    [InlineData("test?file")]
    [InlineData("test\"file")]
    public async Task SaveSettingsFileAsync_WithInvalidCharacters_ShouldThrowArgumentException(string filename)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveSettingsFileAsync(filename, "{\"key\":\"value\"}"));
    }

    [Fact]
    public async Task SaveSettingsFileAsync_WithValidComplexFilename_ShouldWork()
    {
        // Arrange
        var filename = "my_config-v2.backup";
        var content = "{\"key\":\"value\"}";

        // Act
        await _service.SaveSettingsFileAsync(filename, content);

        // Assert
        var retrieved = await _service.GetSettingsFileAsync(filename);
        retrieved.Should().Be(content);
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new Task[10];

        // Act - Multiple concurrent saves to different files
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks[i] = Task.Run(async () =>
            {
                await _service.SaveSettingsFileAsync($"config{index}", $"{{\"id\":{index}}}");
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All files should exist
        var files = await _service.ListSettingsFilesAsync();
        files.Should().HaveCount(10);
    }

    [Fact]
    public async Task SaveSettingsFileAsync_WithPrettyPrintedJson_ShouldPreserveFormatting()
    {
        // Arrange
        var filename = "pretty_config";
        var prettyJson = @"{
  ""key"": ""value"",
  ""nested"": {
    ""array"": [1, 2, 3]
  }
}";

        // Act
        await _service.SaveSettingsFileAsync(filename, prettyJson);
        var retrieved = await _service.GetSettingsFileAsync(filename);

        // Assert
        retrieved.Should().Be(prettyJson);
    }

    [Fact]
    public async Task GetSettingsFileAsync_AfterSave_ShouldReturnExactSameContent()
    {
        // Arrange
        var filename = "test_config";
        var content = "{\"unicode\":\"✓\",\"emoji\":\"🎉\",\"special\":\"\\n\\t\\r\"}";

        // Act
        await _service.SaveSettingsFileAsync(filename, content);
        var retrieved = await _service.GetSettingsFileAsync(filename);

        // Assert
        retrieved.Should().Be(content);
    }
}
