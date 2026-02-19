using System;
using System.IO;
using Xunit;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Core;

/// <summary>
/// Unit tests for PathHelper - ensures path portability works correctly
/// </summary>
public class PathHelperTests
{
    private const string TestDataPath = @"C:\TestApp\data";

    [Fact]
    public void ToRelativePath_PathUnderDataFolder_ReturnsRelativePath()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var absolutePath = Path.Combine(TestDataPath, "thumbnails", "abc123.png");

        // Act
        var result = helper.ToRelativePath(absolutePath);

        // Assert
        Assert.NotNull(result);
        Assert.False(Path.IsPathRooted(result), "Result should be a relative path");
        Assert.Equal(Path.Combine("thumbnails", "abc123.png"), result);
    }

    [Fact]
    public void ToRelativePath_PathOutsideDataFolder_ReturnsAbsolutePath()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var externalPath = @"D:\Games\MyGame\mods";

        // Act
        var result = helper.ToRelativePath(externalPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(externalPath, result);
    }

    [Fact]
    public void ToRelativePath_NullPath_ReturnsNull()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);

        // Act
        var result = helper.ToRelativePath(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToRelativePath_EmptyPath_ReturnsNull()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);

        // Act
        var result = helper.ToRelativePath(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToRelativePath_NestedPath_ReturnsCorrectRelativePath()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var nestedPath = Path.Combine(TestDataPath, "profiles", "default", "mods", "archive.zip");

        // Act
        var result = helper.ToRelativePath(nestedPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Path.Combine("profiles", "default", "mods", "archive.zip"), result);
    }

    [Fact]
    public void ToAbsolutePath_RelativePath_ReturnsAbsolutePath()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var relativePath = Path.Combine("thumbnails", "abc123.png");

        // Act
        var result = helper.ToAbsolutePath(relativePath);

        // Assert
        Assert.NotNull(result);
        Assert.True(Path.IsPathRooted(result), "Result should be an absolute path");
        Assert.Equal(Path.Combine(TestDataPath, "thumbnails", "abc123.png"), result);
    }

    [Fact]
    public void ToAbsolutePath_AlreadyAbsolutePath_ReturnsUnchanged()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var absolutePath = @"D:\Games\MyGame\mods";

        // Act
        var result = helper.ToAbsolutePath(absolutePath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(absolutePath, result);
    }

    [Fact]
    public void ToAbsolutePath_NullPath_ReturnsNull()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);

        // Act
        var result = helper.ToAbsolutePath(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToAbsolutePath_EmptyPath_ReturnsNull()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);

        // Act
        var result = helper.ToAbsolutePath(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void IsRelativePath_RelativePath_ReturnsTrue()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var relativePath = Path.Combine("thumbnails", "abc123.png");

        // Act
        var result = helper.IsRelativePath(relativePath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRelativePath_AbsolutePath_ReturnsFalse()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var absolutePath = @"C:\TestApp\data\thumbnails\abc123.png";

        // Act
        var result = helper.IsRelativePath(absolutePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsUnderDataPath_PathUnderDataFolder_ReturnsTrue()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var pathUnderData = Path.Combine(TestDataPath, "thumbnails", "abc123.png");

        // Act
        var result = helper.IsUnderDataPath(pathUnderData);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUnderDataPath_PathOutsideDataFolder_ReturnsFalse()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var externalPath = @"D:\Games\MyGame\mods";

        // Act
        var result = helper.IsUnderDataPath(externalPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RoundTrip_RelativeToAbsoluteToRelative_PreservesPath()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var originalRelative = Path.Combine("thumbnails", "abc123.png");

        // Act
        var absolute = helper.ToAbsolutePath(originalRelative);
        var backToRelative = helper.ToRelativePath(absolute);

        // Assert
        Assert.Equal(originalRelative, backToRelative);
    }

    [Fact]
    public void RoundTrip_ExternalAbsolutePathRemains_Unchanged()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);
        var externalPath = @"D:\Games\MyGame\mods";

        // Act
        var relative = helper.ToRelativePath(externalPath);
        var absolute = helper.ToAbsolutePath(relative);

        // Assert
        Assert.Equal(externalPath, relative);
        Assert.Equal(externalPath, absolute);
    }

    [Fact]
    public void BaseDataPath_Property_ReturnsCorrectPath()
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);

        // Act
        var result = helper.BaseDataPath;

        // Assert
        Assert.Equal(Path.GetFullPath(TestDataPath), result);
    }

    [Theory]
    [InlineData(@"C:\TestApp\data\thumbnails\abc.png", "thumbnails\\abc.png")]
    [InlineData(@"C:\TestApp\data\profiles\default\mods\mod.zip", "profiles\\default\\mods\\mod.zip")]
    [InlineData(@"C:\TestApp\data\cache\temp\extract", "cache\\temp\\extract")]
    public void ToRelativePath_VariousPaths_ReturnsExpectedRelative(string absolutePath, string expectedRelative)
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);

        // Act
        var result = helper.ToRelativePath(absolutePath);

        // Assert
        Assert.Equal(expectedRelative, result);
    }

    [Theory]
    [InlineData("thumbnails\\abc.png", @"C:\TestApp\data\thumbnails\abc.png")]
    [InlineData("profiles\\default\\mods\\mod.zip", @"C:\TestApp\data\profiles\default\mods\mod.zip")]
    [InlineData("cache\\temp\\extract", @"C:\TestApp\data\cache\temp\extract")]
    public void ToAbsolutePath_VariousPaths_ReturnsExpectedAbsolute(string relativePath, string expectedAbsolute)
    {
        // Arrange
        var helper = new PathHelper(TestDataPath);

        // Act
        var result = helper.ToAbsolutePath(relativePath);

        // Assert
        Assert.Equal(expectedAbsolute, result);
    }
}
