using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace D3dxSkinManager.Tests.Modules.Category.Services;

/// <summary>
/// Unit tests for CategoryService
/// Tests CRUD operations, tree building, and cache management
/// </summary>
public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _mockRepository;
    private readonly Mock<IModRepository> _mockModRepository;
    private readonly Mock<IPathHelper> _mockPathHelper;
    private readonly Mock<IHashHelper> _mockHashHelper;
    private readonly Mock<IImageHelper> _mockImageHelper;
    private readonly Mock<IProfilePathService> _mockProfilePathService;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly Mock<IProfileContext> _mockProfileContext;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _mockRepository = new Mock<ICategoryRepository>();
        _mockModRepository = new Mock<IModRepository>();
        _mockPathHelper = new Mock<IPathHelper>();
        _mockHashHelper = new Mock<IHashHelper>();
        _mockImageHelper = new Mock<IImageHelper>();
        _mockProfilePathService = new Mock<IProfilePathService>();
        _mockCache = new Mock<IMemoryCache>();
        _mockEventBus = new Mock<IProfileEventBus>();
        _mockProfileContext = new Mock<IProfileContext>();
        _mockLogger = new Mock<ILogHelper>();

        // Setup profile context
        _mockProfileContext.Setup(x => x.ProfileId).Returns("test-profile-id");

        _service = new CategoryService(
            _mockRepository.Object,
            _mockModRepository.Object,
            _mockPathHelper.Object,
            _mockHashHelper.Object,
            _mockImageHelper.Object,
            _mockProfilePathService.Object,
            _mockCache.Object,
            _mockEventBus.Object,
            _mockProfileContext.Object,
            _mockLogger.Object
        );

        // Setup default mock behaviors
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModEntity>());
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateCategory()
    {
        // Arrange
        var categoryId = Guid.NewGuid().ToString();
        var name = "Test Category";
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CategoryInfo>());
        _mockRepository.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepository.Setup(r => r.InsertAsync(It.IsAny<CategoryInfo>())).ReturnsAsync((CategoryInfo c) => c);

        // Act
        var result = await _service.CreateAsync(categoryId, name);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(categoryId);
        result.Name.Should().Be(name);
        _mockRepository.Verify(r => r.InsertAsync(It.IsAny<CategoryInfo>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyCategoryId_ShouldGenerateGuid()
    {
        // Arrange
        var name = "Test Category";
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CategoryInfo>());
        _mockRepository.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepository.Setup(r => r.InsertAsync(It.IsAny<CategoryInfo>())).ReturnsAsync((CategoryInfo c) => c);

        // Act
        var result = await _service.CreateAsync("", name);

        // Assert
        result.Should().NotBeNull();
        Guid.TryParse(result!.Id, out _).Should().BeTrue("Service should generate a valid GUID");
        _mockRepository.Verify(r => r.InsertAsync(It.IsAny<CategoryInfo>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateName_ShouldReturnNull()
    {
        // Arrange
        var categoryId = Guid.NewGuid().ToString();
        var name = "Existing Category";
        var existingCategory = new CategoryInfo { Id = "existing-id", Name = name };
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CategoryInfo> { existingCategory });

        // Act
        var result = await _service.CreateAsync(categoryId, name);

        // Assert
        result.Should().BeNull("Category names must be globally unique");
        _mockRepository.Verify(r => r.InsertAsync(It.IsAny<CategoryInfo>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCategoryAsync_WithValidData_ShouldUpdate()
    {
        // Arrange
        var categoryId = Guid.NewGuid().ToString();
        var oldName = "Old Name";
        var newName = "New Name";
        var existingCategory = new CategoryInfo { Id = categoryId, Name = oldName };

        _mockRepository.Setup(r => r.GetByIdAsync(categoryId)).ReturnsAsync(existingCategory);
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CategoryInfo> { existingCategory });
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<CategoryInfo>())).ReturnsAsync(true);

        // Act
        var result = await _service.UpdateCategoryAsync(categoryId, newName);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<CategoryInfo>(c => c.Name == newName)), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_ShouldDeleteCategoryAndChildren()
    {
        // Arrange
        var parentId = Guid.NewGuid().ToString();
        var childId = Guid.NewGuid().ToString();
        var parent = new CategoryInfo { Id = parentId, Name = "Parent" };
        var child = new CategoryInfo { Id = childId, Name = "Child", ParentId = parentId };

        _mockRepository.Setup(r => r.GetByIdAsync(parentId)).ReturnsAsync(parent);
        _mockRepository.Setup(r => r.GetChildrenAsync(parentId)).ReturnsAsync(new List<CategoryInfo> { child });
        _mockRepository.Setup(r => r.GetChildrenAsync(childId)).ReturnsAsync(new List<CategoryInfo>());
        _mockRepository.Setup(r => r.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.DeleteAsync(parentId);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.DeleteAsync(parentId), Times.Once);
        _mockRepository.Verify(r => r.DeleteAsync(childId), Times.Once);
    }

    [Fact]
    public async Task UpdateParentAsync_ShouldMoveCategory()
    {
        // Arrange
        var categoryId = Guid.NewGuid().ToString();
        var newParentId = Guid.NewGuid().ToString();

        _mockRepository.Setup(r => r.MoveCategoryAsync(categoryId, newParentId)).ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetChildrenAsync(newParentId)).ReturnsAsync(new List<CategoryInfo>());

        // Act
        var result = await _service.UpdateParentAsync(categoryId, newParentId);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.MoveCategoryAsync(categoryId, newParentId), Times.Once);
    }

    [Fact]
    public async Task GetByNameAsync_WithExistingName_ShouldReturnCategory()
    {
        // Arrange
        var name = "Test Category";
        var expectedCategory = new CategoryInfo { Id = Guid.NewGuid().ToString(), Name = name };
        _mockRepository.Setup(r => r.GetByNameAsync(name)).ReturnsAsync(expectedCategory);

        // Act
        var result = await _service.GetByNameAsync(name);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(name);
    }

    [Fact]
    public async Task GetByNameAsync_WithNonExistingName_ShouldReturnNull()
    {
        // Arrange
        var name = "Non-Existing";
        _mockRepository.Setup(r => r.GetByNameAsync(name)).ReturnsAsync((CategoryInfo?)null);

        // Act
        var result = await _service.GetByNameAsync(name);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingId_ShouldReturnTrue()
    {
        // Arrange
        var categoryId = Guid.NewGuid().ToString();
        _mockRepository.Setup(r => r.ExistsAsync(categoryId)).ReturnsAsync(true);

        // Act
        var result = await _service.ExistsAsync(categoryId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateThumbnailAsync_WithValidPath_ShouldUpdate()
    {
        // Arrange
        var categoryId = Guid.NewGuid().ToString();
        var thumbnailPath = @"C:\test\thumbnail.png";
        var relativePath = "thumbnails/thumb.png";
        var category = new CategoryInfo { Id = categoryId, Name = "Test" };

        _mockRepository.Setup(r => r.GetByIdAsync(categoryId)).ReturnsAsync(category);
        _mockPathHelper.Setup(p => p.ToRelativePath(thumbnailPath)).Returns(relativePath);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<CategoryInfo>())).ReturnsAsync(true);

        // Act
        var result = await _service.UpdateThumbnailAsync(categoryId, thumbnailPath);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<CategoryInfo>(c => c.Thumbnail == relativePath)), Times.Once);
    }
}
