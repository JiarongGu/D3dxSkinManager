using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Tests.Modules.Mods;

/// <summary>
/// Unit tests for ModFacade (replaces old ModControllerTests)
/// Tests message routing, mod operations, and event handling
/// </summary>
public class ModFacadeTests
{
    private readonly Mock<IModRepository> _mockRepository;
    private readonly Mock<IModFileService> _mockFileService;
    private readonly Mock<IModImportService> _mockImportService;
    private readonly Mock<IModQueryService> _mockQueryService;
    private readonly Mock<IClassificationService> _mockClassificationService;
    private readonly Mock<IPayloadHelper> _mockPayloadHelper;
    private readonly Mock<IEventEmitterHelper> _mockEventEmitter = new();
    private readonly Mock<ILogHelper> _mockLogger = new();
    private readonly Mock<IImageService> _mockImageService = new();
    private readonly Mock<IProfilePathService> _mockProfilePathService = new();
    private readonly Mock<IPathHelper> _mockPathHelper = new();
    private readonly ModFacade _facade;

    public ModFacadeTests()
    {
        _mockRepository = new Mock<IModRepository>();
        _mockFileService = new Mock<IModFileService>();
        _mockImportService = new Mock<IModImportService>();
        _mockQueryService = new Mock<IModQueryService>();
        _mockClassificationService = new Mock<IClassificationService>();
        _mockPayloadHelper = new Mock<IPayloadHelper>();

        _facade = new ModFacade(
            _mockRepository.Object,
            _mockFileService.Object,
            _mockImportService.Object,
            _mockQueryService.Object,
            _mockClassificationService.Object,
            _mockPayloadHelper.Object,
            _mockEventEmitter.Object,
            _mockImageService.Object,
            _mockProfilePathService.Object,
            _mockPathHelper.Object,
            _mockLogger.Object
        );
    }

    #region HandleMessageAsync Tests

    [Fact]
    public async Task HandleMessageAsync_WithGetAll_ShouldReturnAllMods()
    {
        // Arrange
        var mods = CreateSampleMods();
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(mods);

        var request = new MessageRequest
        {
            Id = Guid.NewGuid().ToString(),
            Module = "MOD",
            Type = "GET_ALL",
            Payload = null
        };

        // Act
        var response = await _facade.HandleMessageAsync(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_WithUnknownType_ShouldReturnError()
    {
        // Arrange
        var request = new MessageRequest
        {
            Id = Guid.NewGuid().ToString(),
            Module = "MOD",
            Type = "UNKNOWN_TYPE",
            Payload = null
        };

        // Act
        var response = await _facade.HandleMessageAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Unknown message type");
    }

    [Fact]
    public async Task HandleMessageAsync_WhenExceptionThrown_ShouldReturnError()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database error"));

        var request = new MessageRequest
        {
            Id = Guid.NewGuid().ToString(),
            Module = "MOD",
            Type = "GET_ALL",
            Payload = null
        };

        // Act
        var response = await _facade.HandleMessageAsync(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Database error");
    }

    #endregion

    #region GetAllModsAsync Tests

    [Fact]
    public async Task GetAllModsAsync_ShouldReturnAllMods()
    {
        // Arrange
        var mods = CreateSampleMods();
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(mods);

        // Act
        var result = await _facade.GetAllModsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(mods);
    }

    [Fact]
    public async Task GetAllModsAsync_WithEmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModInfo>());

        // Act
        var result = await _facade.GetAllModsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetModByIdAsync Tests

    [Fact]
    public async Task GetModByIdAsync_WithExistingMod_ShouldReturnMod()
    {
        // Arrange
        var expectedMod = CreateSampleMods()[0];
        _mockRepository.Setup(r => r.GetByIdAsync("sha123")).ReturnsAsync(expectedMod);

        // Act
        var result = await _facade.GetModByIdAsync("sha123");

        // Assert
        result.Should().NotBeNull();
        result!.SHA.Should().Be("sha123");
    }

    [Fact]
    public async Task GetModByIdAsync_WithNonExistentMod_ShouldReturnNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync("nonexistent")).ReturnsAsync((ModInfo?)null);

        // Act
        var result = await _facade.GetModByIdAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region LoadModAsync Tests

    [Fact]
    public async Task LoadModAsync_WithValidSha_ShouldLoadAndEmitEvent()
    {
        // Arrange
        _mockFileService.Setup(s => s.LoadAsync("sha123")).ReturnsAsync(true);

        // Act
        var result = await _facade.LoadModAsync("sha123");

        // Assert
        result.Should().BeTrue();
        _mockFileService.Verify(s => s.LoadAsync("sha123"), Times.Once);
        // Note: IsLoaded is determined dynamically from file system, not stored in database
    }

    [Fact]
    public async Task LoadModAsync_WhenArchiveLoadFails_ShouldReturnFalse()
    {
        // Arrange
        _mockFileService.Setup(s => s.LoadAsync("sha123")).ReturnsAsync(false);

        // Act
        var result = await _facade.LoadModAsync("sha123");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task LoadModAsync_WhenSuccessful_ShouldEmitEvent()
    {
        // Arrange
        _mockFileService.Setup(s => s.LoadAsync("sha123")).ReturnsAsync(true);

        // Act
        await _facade.LoadModAsync("sha123");

        // Assert
        _mockEventEmitter.Verify(e => e.EmitAsync(PluginEventType.ModLoaded, It.IsAny<string?>(), It.IsAny<object?>()), Times.Once);
    }

    #endregion

    #region UnloadModAsync Tests

    [Fact]
    public async Task UnloadModAsync_WithValidSha_ShouldUnloadAndEmitEvent()
    {
        // Arrange
        _mockFileService.Setup(s => s.UnloadAsync("sha123")).ReturnsAsync(true);

        // Act
        var result = await _facade.UnloadModAsync("sha123");

        // Assert
        result.Should().BeTrue();
        _mockFileService.Verify(s => s.UnloadAsync("sha123"), Times.Once);
        // Note: IsLoaded is determined dynamically from file system, not stored in database
    }

    [Fact]
    public async Task UnloadModAsync_WhenArchiveUnloadFails_ShouldReturnFalse()
    {
        // Arrange
        _mockFileService.Setup(s => s.UnloadAsync("sha123")).ReturnsAsync(false);

        // Act
        var result = await _facade.UnloadModAsync("sha123");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UnloadModAsync_WhenSuccessful_ShouldEmitEvent()
    {
        // Arrange
        _mockFileService.Setup(s => s.UnloadAsync("sha123")).ReturnsAsync(true);

        // Act
        await _facade.UnloadModAsync("sha123");

        // Assert
        _mockEventEmitter.Verify(e => e.EmitAsync(PluginEventType.ModUnloaded, It.IsAny<string?>(), It.IsAny<object?>()), Times.Once);
    }

    #endregion

    #region GetLoadedModIdsAsync Tests

    [Fact]
    public async Task GetLoadedModIdsAsync_ShouldReturnLoadedIds()
    {
        // Arrange
        var loadedIds = new List<string> { "sha123", "sha456" };
        _mockRepository.Setup(r => r.GetLoadedIdsAsync()).ReturnsAsync(loadedIds);

        // Act
        var result = await _facade.GetLoadedModIdsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("sha123");
        result.Should().Contain("sha456");
    }

    [Fact]
    public async Task GetLoadedModIdsAsync_WithNoLoadedMods_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetLoadedIdsAsync()).ReturnsAsync(new List<string>());

        // Act
        var result = await _facade.GetLoadedModIdsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ImportModAsync Tests

    [Fact]
    public async Task ImportModAsync_WithValidFile_ShouldReturnImportedMod()
    {
        // Arrange
        var importedMod = CreateSampleMods()[0];
        _mockImportService.Setup(s => s.ImportAsync("test.zip")).ReturnsAsync(importedMod);

        // Act
        var result = await _facade.ImportModAsync("test.zip");

        // Assert
        result.Should().NotBeNull();
        result!.SHA.Should().Be(importedMod.SHA);
        _mockImportService.Verify(s => s.ImportAsync("test.zip"), Times.Once);
    }

    [Fact]
    public async Task ImportModAsync_WhenImportFails_ShouldReturnNull()
    {
        // Arrange
        _mockImportService.Setup(s => s.ImportAsync("invalid.zip")).ReturnsAsync((ModInfo?)null);

        // Act
        var result = await _facade.ImportModAsync("invalid.zip");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ImportModAsync_WhenSuccessful_ShouldEmitEvent()
    {
        // Arrange
        var importedMod = CreateSampleMods()[0];
        _mockImportService.Setup(s => s.ImportAsync("test.zip")).ReturnsAsync(importedMod);

        // Act
        await _facade.ImportModAsync("test.zip");

        // Assert
        _mockEventEmitter.Verify(e => e.EmitAsync(PluginEventType.ModImported, It.IsAny<string?>(), It.IsAny<object?>()), Times.Once);
    }

    #endregion

    #region DeleteModAsync Tests

    [Fact]
    public async Task DeleteModAsync_WithExistingMod_ShouldDeleteSuccessfully()
    {
        // Arrange
        var mod = CreateSampleMods()[0];
        _mockRepository.Setup(r => r.GetByIdAsync("sha123")).ReturnsAsync(mod);
        _mockFileService.Setup(s => s.DeleteAsync("sha123", null))
            .ReturnsAsync(true);
        _mockRepository.Setup(r => r.DeleteAsync("sha123")).ReturnsAsync(true);

        // Act
        var result = await _facade.DeleteModAsync("sha123");

        // Assert
        result.Should().BeTrue();
        _mockFileService.Verify(s => s.DeleteAsync("sha123", null), Times.Once);
        _mockRepository.Verify(r => r.DeleteAsync("sha123"), Times.Once);
    }

    [Fact]
    public async Task DeleteModAsync_WithNonExistentMod_ShouldReturnFalse()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync("nonexistent")).ReturnsAsync((ModInfo?)null);

        // Act
        var result = await _facade.DeleteModAsync("nonexistent");

        // Assert
        result.Should().BeFalse();
        _mockFileService.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteModAsync_WhenSuccessful_ShouldEmitEvent()
    {
        // Arrange
        var mod = CreateSampleMods()[0];
        _mockRepository.Setup(r => r.GetByIdAsync("sha123")).ReturnsAsync(mod);
        _mockFileService.Setup(s => s.DeleteAsync("sha123", null))
            .ReturnsAsync(true);
        _mockRepository.Setup(r => r.DeleteAsync("sha123")).ReturnsAsync(true);

        // Act
        await _facade.DeleteModAsync("sha123");

        // Assert
        _mockEventEmitter.Verify(e => e.EmitAsync(PluginEventType.ModDeleted, It.IsAny<string?>(), It.IsAny<object?>()), Times.Once);
    }

    #endregion

    #region GetModsByObjectAsync Tests

    [Fact]
    public async Task GetModsByObjectAsync_WithExistingObject_ShouldReturnMods()
    {
        // Arrange
        var mods = CreateSampleMods().Take(2).ToList();
        _mockRepository.Setup(r => r.GetByCategoryAsync("Character1")).ReturnsAsync(mods);

        // Act
        var result = await _facade.GetModsByObjectAsync("Character1");

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetModsByObjectAsync_WithNonExistentObject_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByCategoryAsync("NonExistent")).ReturnsAsync(new List<ModInfo>());

        // Act
        var result = await _facade.GetModsByObjectAsync("NonExistent");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetObjectNamesAsync Tests

    [Fact]
    public async Task GetObjectNamesAsync_ShouldReturnDistinctCategories()
    {
        // Arrange
        var categories = new List<string> { "Character1", "Character2", "Weapon1" };
        _mockRepository.Setup(r => r.GetDistinctCategoriesAsync()).ReturnsAsync(categories);

        // Act
        var result = await _facade.GetObjectNamesAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("Character1");
        result.Should().Contain("Character2");
        result.Should().Contain("Weapon1");
    }

    #endregion

    #region GetAuthorsAsync Tests

    [Fact]
    public async Task GetAuthorsAsync_ShouldReturnDistinctAuthors()
    {
        // Arrange
        var authors = new List<string> { "Author1", "Author2", "Author3" };
        _mockRepository.Setup(r => r.GetDistinctAuthorsAsync()).ReturnsAsync(authors);

        // Act
        var result = await _facade.GetAuthorsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("Author1");
    }

    #endregion

    #region GetTagsAsync Tests

    [Fact]
    public async Task GetTagsAsync_ShouldReturnAllTags()
    {
        // Arrange
        var tags = new List<string> { "tag1", "tag2", "tag3" };
        _mockRepository.Setup(r => r.GetAllTagsAsync()).ReturnsAsync(tags);

        // Act
        var result = await _facade.GetTagsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("tag1");
    }

    #endregion

    #region SearchModsAsync Tests

    [Fact]
    public async Task SearchModsAsync_WithValidTerm_ShouldReturnMatchingMods()
    {
        // Arrange
        var mods = CreateSampleMods().Take(2).ToList();
        _mockQueryService.Setup(s => s.SearchAsync("Character")).ReturnsAsync(mods);

        // Act
        var result = await _facade.SearchModsAsync("Character");

        // Assert
        result.Should().HaveCount(2);
        _mockQueryService.Verify(s => s.SearchAsync("Character"), Times.Once);
    }

    [Fact]
    public async Task SearchModsAsync_WithNoMatches_ShouldReturnEmptyList()
    {
        // Arrange
        _mockQueryService.Setup(s => s.SearchAsync("NoMatch")).ReturnsAsync(new List<ModInfo>());

        // Act
        var result = await _facade.SearchModsAsync("NoMatch");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    // Helper method to create sample test data
    private List<ModInfo> CreateSampleMods()
    {
        return new List<ModInfo>
        {
            new()
            {
                SHA = "sha123",
                Name = "Test Mod 1",
                Author = "Author1",
                Category = "Character1",
                IsLoaded = false
            },
            new()
            {
                SHA = "sha456",
                Name = "Test Mod 2",
                Author = "Author2",
                Category = "Character2",
                IsLoaded = false
            },
            new()
            {
                SHA = "sha789",
                Name = "Test Mod 3",
                Author = "Author3",
                Category = "Weapon1",
                IsLoaded = true
            }
        };
    }
}
