using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Category;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace D3dxSkinManager.Tests.Modules.Category.Services;

public class CategoryServiceCacheTests
{
    private readonly Mock<ICategoryRepository> _mockRepository;
    private readonly Mock<IModRepository> _mockModRepository;
    private readonly Mock<IPathHelper> _mockPathHelper;
    private readonly Mock<IHashHelper> _mockHashHelper;
    private readonly Mock<IImageHelper> _mockImageHelper;
    private readonly Mock<IFileTransferService> _mockFileTransferService;
    private readonly Mock<IProfilePathService> _mockProfilePathService;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly Mock<IProfileContext> _mockProfileContext;
    private readonly CategoryService _service;

    public CategoryServiceCacheTests()
    {
        _mockRepository = new Mock<ICategoryRepository>();
        _mockModRepository = new Mock<IModRepository>();
        _mockPathHelper = new Mock<IPathHelper>();
        _mockHashHelper = new Mock<IHashHelper>();
        _mockImageHelper = new Mock<IImageHelper>();
        _mockFileTransferService = new Mock<IFileTransferService>();
        _mockProfilePathService = new Mock<IProfilePathService>();
        _mockCache = new Mock<IMemoryCache>();
        _mockEventBus = new Mock<IProfileEventBus>();
        _mockProfileContext = new Mock<IProfileContext>();

        // Setup profile context
        _mockProfileContext.Setup(x => x.ProfileId).Returns("test-profile-id");

        _service = new CategoryService(
            _mockRepository.Object,
            _mockModRepository.Object,
            _mockPathHelper.Object,
            _mockHashHelper.Object,
            _mockImageHelper.Object,
            _mockFileTransferService.Object,
            _mockProfilePathService.Object,
            _mockCache.Object,
            _mockEventBus.Object,
            _mockProfileContext.Object
        );
    }

    [Fact]
    public void InvalidateTreeCache_ShouldRemoveCacheEntry()
    {
        // Arrange
        var expectedCacheKey = "CategoryTree_test-profile-id";

        // Act
        _service.InvalidateTreeCache();

        // Give Task.Run time to complete
        Task.Delay(100).Wait();

        // Assert
        _mockCache.Verify(
            x => x.Remove(expectedCacheKey),
            Times.Once,
            "Should remove cache entry with profile-specific key"
        );
    }

    [Fact]
    public void InvalidateTreeCache_ShouldEmitCategoryTreeUpdatedEvent()
    {
        // Act
        _service.InvalidateTreeCache();

        // Give Task.Run time to complete the async event emission
        Task.Delay(100).Wait();

        // Assert
        _mockEventBus.Verify(
            x => x.EmitAsync(
                ModuleNames.CATEGORY,
                CategoryEvents.CATEGORY_TREE_UPDATED),
            Times.Once,
            "Should emit CATEGORY.CATEGORY_TREE_UPDATED event after invalidating cache"
        );
    }

    [Fact]
    public void InvalidateTreeCache_ShouldNotBlock_DespiteAsyncEventEmission()
    {
        // Arrange
        var startTime = DateTime.UtcNow;

        // Act
        _service.InvalidateTreeCache();
        var duration = DateTime.UtcNow - startTime;

        // Assert
        duration.TotalMilliseconds.Should().BeLessThan(50,
            "InvalidateTreeCache should return quickly without blocking on async event emission");
    }

    [Fact]
    public void InvalidateTreeCache_CalledMultipleTimes_ShouldInvalidateAndEmitEachTime()
    {
        // Act
        _service.InvalidateTreeCache();
        _service.InvalidateTreeCache();
        _service.InvalidateTreeCache();

        // Give Task.Run time to complete
        Task.Delay(200).Wait();

        // Assert
        _mockCache.Verify(
            x => x.Remove(It.IsAny<string>()),
            Times.Exactly(3),
            "Should invalidate cache each time it's called"
        );

        _mockEventBus.Verify(
            x => x.EmitAsync(
                ModuleNames.CATEGORY,
                CategoryEvents.CATEGORY_TREE_UPDATED),
            Times.Exactly(3),
            "Should emit event each time cache is invalidated"
        );
    }
}
