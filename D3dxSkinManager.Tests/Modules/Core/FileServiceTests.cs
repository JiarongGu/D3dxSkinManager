using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Core;

/// <summary>
/// Unit tests for FileService
/// Tests file hashing, archive extraction, and directory operations
/// </summary>
public class FileServiceTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly FileService _service;
    private readonly Mock<ILogHelper> _mockLogger = new();

    public FileServiceTests()
    {
        _testDataPath = Path.Combine(Path.GetTempPath(), $"FileServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataPath);
        _service = new FileService(_mockLogger.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, recursive: true);
        }
    }

    [Fact]
    public async Task CalculateSha256Async_WithValidFile_ShouldReturnCorrectHash()
    {
        // Arrange
        var testFile = Path.Combine(_testDataPath, "test.txt");
        var content = "Hello, World!";
        await File.WriteAllTextAsync(testFile, content);

        // Expected SHA256 hash for "Hello, World!"
        var expectedHash = "dffd6021bb2bd5b0af676290809ec3a53191dd81c7f70a4b28688a362182986f";

        // Act
        var result = await _service.CalculateSha256Async(testFile);

        // Assert
        result.Should().Be(expectedHash);
    }

    [Fact]
    public async Task CalculateSha256Async_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDataPath, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.CalculateSha256Async(nonExistentFile));
    }

    [Fact]
    public async Task CalculateSha256Async_WithEmptyFile_ShouldReturnEmptyFileHash()
    {
        // Arrange
        var testFile = Path.Combine(_testDataPath, "empty.txt");
        await File.WriteAllTextAsync(testFile, string.Empty);

        // Expected SHA256 hash for empty file
        var expectedHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        // Act
        var result = await _service.CalculateSha256Async(testFile);

        // Assert
        result.Should().Be(expectedHash);
    }

    [Fact]
    public async Task CalculateSha256Async_WithLargeFile_ShouldCalculateCorrectly()
    {
        // Arrange - Create a 1MB file
        var testFile = Path.Combine(_testDataPath, "large.txt");
        var largeContent = new string('A', 1024 * 1024); // 1MB of 'A' characters
        await File.WriteAllTextAsync(testFile, largeContent);

        // Act
        var result = await _service.CalculateSha256Async(testFile);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Length.Should().Be(64); // SHA256 is 64 hex characters
    }

    [Fact]
    public async Task CalculateSha256Async_SameContent_ShouldReturnSameHash()
    {
        // Arrange
        var file1 = Path.Combine(_testDataPath, "file1.txt");
        var file2 = Path.Combine(_testDataPath, "file2.txt");
        var content = "Identical content";

        await File.WriteAllTextAsync(file1, content);
        await File.WriteAllTextAsync(file2, content);

        // Act
        var hash1 = await _service.CalculateSha256Async(file1);
        var hash2 = await _service.CalculateSha256Async(file2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public async Task CopyDirectoryAsync_WithValidSource_ShouldCopyAllFiles()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDataPath, "source");
        var targetDir = Path.Combine(_testDataPath, "target");

        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "file1.txt"), "Content 1");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "file2.txt"), "Content 2");

        var subDir = Path.Combine(sourceDir, "subfolder");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "file3.txt"), "Content 3");

        // Act
        var result = await _service.CopyDirectoryAsync(sourceDir, targetDir);

        // Assert
        result.Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "file2.txt")).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "subfolder", "file3.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task CopyDirectoryAsync_WithNonExistentSource_ShouldThrowDirectoryNotFoundException()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDataPath, "nonexistent");
        var targetDir = Path.Combine(_testDataPath, "target");

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _service.CopyDirectoryAsync(sourceDir, targetDir));
    }

    [Fact]
    public async Task CopyDirectoryAsync_WithOverwriteTrue_ShouldOverwriteExistingFiles()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDataPath, "source");
        var targetDir = Path.Combine(_testDataPath, "target");

        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(targetDir);

        await File.WriteAllTextAsync(Path.Combine(sourceDir, "file.txt"), "New content");
        await File.WriteAllTextAsync(Path.Combine(targetDir, "file.txt"), "Old content");

        // Act
        var result = await _service.CopyDirectoryAsync(sourceDir, targetDir, overwrite: true);

        // Assert
        result.Should().BeTrue();
        var content = await File.ReadAllTextAsync(Path.Combine(targetDir, "file.txt"));
        content.Should().Be("New content");
    }

    [Fact]
    public async Task CopyDirectoryAsync_WithEmptySource_ShouldCreateTargetDirectory()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDataPath, "empty_source");
        var targetDir = Path.Combine(_testDataPath, "target");

        Directory.CreateDirectory(sourceDir);

        // Act
        var result = await _service.CopyDirectoryAsync(sourceDir, targetDir);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(targetDir).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDirectoryAsync_WithExistingDirectory_ShouldDeleteRecursively()
    {
        // Arrange
        var testDir = Path.Combine(_testDataPath, "to_delete");
        Directory.CreateDirectory(testDir);

        var subDir = Path.Combine(testDir, "subfolder");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "file.txt"), "Content");

        // Act
        var result = await _service.DeleteDirectoryAsync(testDir);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(testDir).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDirectoryAsync_WithNonExistentDirectory_ShouldReturnTrue()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDataPath, "nonexistent");

        // Act
        var result = await _service.DeleteDirectoryAsync(nonExistentDir);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDirectoryAsync_WithNestedStructure_ShouldDeleteAll()
    {
        // Arrange - Create deep nested structure
        var testDir = Path.Combine(_testDataPath, "deep");
        var level1 = Path.Combine(testDir, "level1");
        var level2 = Path.Combine(level1, "level2");
        var level3 = Path.Combine(level2, "level3");

        Directory.CreateDirectory(level3);
        await File.WriteAllTextAsync(Path.Combine(level3, "file.txt"), "Deep file");

        // Act
        var result = await _service.DeleteDirectoryAsync(testDir);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(testDir).Should().BeFalse();
    }

    [Fact]
    public async Task CopyDirectoryAsync_WithComplexStructure_ShouldPreserveStructure()
    {
        // Arrange
        var sourceDir = Path.Combine(_testDataPath, "complex_source");
        var targetDir = Path.Combine(_testDataPath, "complex_target");

        Directory.CreateDirectory(Path.Combine(sourceDir, "folder1", "subfolder1"));
        Directory.CreateDirectory(Path.Combine(sourceDir, "folder2"));

        await File.WriteAllTextAsync(Path.Combine(sourceDir, "root.txt"), "Root");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "folder1", "file1.txt"), "File1");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "folder1", "subfolder1", "file2.txt"), "File2");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "folder2", "file3.txt"), "File3");

        // Act
        var result = await _service.CopyDirectoryAsync(sourceDir, targetDir);

        // Assert
        result.Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "root.txt")).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "folder1", "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "folder1", "subfolder1", "file2.txt")).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "folder2", "file3.txt")).Should().BeTrue();
    }
}
