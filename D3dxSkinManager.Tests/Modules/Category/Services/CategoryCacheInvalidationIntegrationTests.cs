using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Category;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Mod;
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
/// Integration tests that verify the complete flow:
/// MOD.CATEGORY_UPDATED â†?CategoryEventHandler â†?InvalidateCache â†?CATEGORY.CATEGORY_TREE_UPDATED
/// </summary>
public class CategoryCacheInvalidationIntegrationTests : IDisposable
{
    private readonly Mock<ICategoryRepository> _mockCategoryRepository;
    private readonly Mock<IModRepository> _mockModRepository;
    private readonly Mock<IPathHelper> _mockPathHelper;
    private readonly Mock<IHashHelper> _mockHashHelper;
    private readonly Mock<IImageHelper> _mockImageHelper;
    private readonly Mock<IProfilePathService> _mockProfilePathService;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly Mock<IProfileContext> _mockProfileContext;
    private readonly Mock<ILogHelper> _mockLogger;

    private readonly CategoryService _categoryService;
    private readonly CategoryEventHandler _eventHandler;

    private Func<EventMessage, Task>? _registeredCategoryUpdateHandler;

    public CategoryCacheInvalidationIntegrationTests()
    {
        _mockCategoryRepository = new Mock<ICategoryRepository>();
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

        // Capture the event handler registered by CategoryEventHandler
        _mockEventBus
            .Setup(x => x.Subscribe(
                ModuleNames.MOD,
                ModEvents.CATEGORY_UPDATED,
                It.IsAny<Func<EventMessage, Task>>()))
            .Returns("handler-id")
            .Callback<string, string, Func<EventMessage, Task>>((module, type, handler) =>
            {
                _registeredCategoryUpdateHandler = handler;
            });

        // Create the service and event handler
        _categoryService = new CategoryService(
            _mockCategoryRepository.Object,
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

        _eventHandler = new CategoryEventHandler(
            _categoryService,
            _mockEventBus.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task CompleteFlow_WhenModCategoryUpdated_ShouldInvalidateCacheAndEmitEvent()
    {
        // Arrange - Verify handler was registered
        _registeredCategoryUpdateHandler.Should().NotBeNull(
            "CategoryEventHandler should register a handler for MOD.CATEGORY_UPDATED in constructor");

        var modCategoryUpdatedEvent = new EventMessage
        {
            Module = ModuleNames.MOD,
            Type = ModEvents.CATEGORY_UPDATED,
            Payload = new { id = "test-mod-id", category = "new-category-id" }
        };

        // Act - Simulate the backend emitting MOD.CATEGORY_UPDATED
        await _registeredCategoryUpdateHandler!(modCategoryUpdatedEvent);

        // Give Task.Run time to complete async event emission
        await Task.Delay(100);

        // Assert - Verify the complete flow
        // 1. Cache should be invalidated
        _mockCache.Verify(
            x => x.Remove("CategoryTree_test-profile-id"),
            Times.Once,
            "Step 1: Cache should be invalidated when mod category is updated"
        );

        // 2. CATEGORY.CATEGORY_TREE_UPDATED event should be emitted
        _mockEventBus.Verify(
            x => x.EmitAsync(
                ModuleNames.CATEGORY,
                CategoryEvents.CATEGORY_TREE_UPDATED),
            Times.Once,
            "Step 2: CATEGORY_TREE_UPDATED event should be emitted after cache invalidation"
        );
    }

    [Fact]
    public async Task CompleteFlow_ShouldHandleMultipleModUpdates()
    {
        // Arrange
        _registeredCategoryUpdateHandler.Should().NotBeNull();

        // Act - Simulate multiple mods being moved to different categories
        await _registeredCategoryUpdateHandler!(new EventMessage
        {
            Module = ModuleNames.MOD,
            Type = ModEvents.CATEGORY_UPDATED,
            Payload = new { id = "mod1", category = "cat1" }
        });

        await _registeredCategoryUpdateHandler!(new EventMessage
        {
            Module = ModuleNames.MOD,
            Type = ModEvents.CATEGORY_UPDATED,
            Payload = new { id = "mod2", category = "cat2" }
        });

        await _registeredCategoryUpdateHandler!(new EventMessage
        {
            Module = ModuleNames.MOD,
            Type = ModEvents.CATEGORY_UPDATED,
            Payload = new { id = "mod3", category = "cat1" }
        });

        // Give time for all async operations
        await Task.Delay(200);

        // Assert - Each update should invalidate cache and emit event
        // InvalidateTreeCache removes 2 keys (tree cache and category map cache)
        // So 3 updates = 6 Remove operations
        _mockCache.Verify(
            x => x.Remove(It.IsAny<string>()),
            Times.Exactly(6),
            "Cache should be invalidated for each mod category update (3 updates Ã— 2 keys = 6)"
        );

        _mockEventBus.Verify(
            x => x.EmitAsync(
                ModuleNames.CATEGORY,
                CategoryEvents.CATEGORY_TREE_UPDATED),
            Times.Exactly(3),
            "CATEGORY_TREE_UPDATED should be emitted for each update"
        );
    }

    [Fact]
    public void EventHandlerRegistration_ShouldUseCorrectModuleAndEventType()
    {
        // Assert
        _mockEventBus.Verify(
            x => x.Subscribe(
                ModuleNames.MOD,
                ModEvents.CATEGORY_UPDATED,
                It.IsAny<Func<EventMessage, Task>>()),
            Times.Once,
            "Should register handler for MOD.CATEGORY_UPDATED event specifically"
        );
    }

    public void Dispose()
    {
        _eventHandler?.Dispose();
    }
}
