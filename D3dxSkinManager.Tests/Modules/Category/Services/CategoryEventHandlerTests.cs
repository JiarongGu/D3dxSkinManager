using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod;

namespace D3dxSkinManager.Tests.Modules.Category.Services;

public class CategoryEventHandlerTests : IDisposable
{
    private readonly Mock<ICategoryService> _mockCategoryService;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly CategoryEventHandler _handler;
    private readonly Dictionary<string, Func<EventMessage, Task>> _registeredHandlers = new();

    public CategoryEventHandlerTests()
    {
        _mockCategoryService = new Mock<ICategoryService>();
        _mockEventBus = new Mock<IProfileEventBus>();
        _mockLogger = new Mock<ILogHelper>();

        // Capture all registered handlers by event type
        _mockEventBus
            .Setup(x => x.Subscribe(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<EventMessage, Task>>()))
            .Returns("test-handler-id")
            .Callback<string, string, Func<EventMessage, Task>>((module, type, handler) =>
            {
                _registeredHandlers[type] = handler;
            });

        _handler = new CategoryEventHandler(
            _mockCategoryService.Object,
            _mockEventBus.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void Constructor_ShouldRegisterHandler_ForCategoryUpdatedEvent()
    {
        // Assert
        _mockEventBus.Verify(
            x => x.Subscribe(
                ModuleNames.MOD,
                ModEvents.CATEGORY_UPDATED,
                It.IsAny<Func<EventMessage, Task>>()),
            Times.Once,
            "Should register handler for MOD.CATEGORY_UPDATED event"
        );
    }

    [Fact]
    public void Constructor_ShouldLogInitialization()
    {
        // Assert
        _mockLogger.Verify(
            x => x.Info(
                It.Is<string>(msg => msg.Contains("Initializing")),
                "CategoryEventHandler"),
            Times.Once,
            "Should log initialization"
        );

        _mockLogger.Verify(
            x => x.Info(
                It.Is<string>(msg => msg.Contains("Successfully registered")),
                "CategoryEventHandler"),
            Times.Once,
            "Should log successful registration"
        );
    }

    [Fact]
    public async Task WhenCategoryUpdatedEventReceived_ShouldInvalidateCache()
    {
        // Arrange
        _registeredHandlers.Should().ContainKey(ModEvents.CATEGORY_UPDATED, "Handler should be registered in constructor");
        var eventMessage = new EventMessage
        {
            Module = ModuleNames.MOD,
            Type = ModEvents.CATEGORY_UPDATED,
            Payload = new { sha = "test-sha", category = "test-category" }
        };

        // Act
        await _registeredHandlers[ModEvents.CATEGORY_UPDATED](eventMessage);

        // Assert
        _mockCategoryService.Verify(
            x => x.InvalidateTreeCache(),
            Times.Once,
            "Should invalidate cache when CATEGORY_UPDATED event is received"
        );
    }

    [Fact]
    public async Task WhenCategoryUpdatedEventReceived_ShouldLogHandling()
    {
        // Arrange
        _registeredHandlers.Should().ContainKey(ModEvents.CATEGORY_UPDATED, "Handler should be registered in constructor");
        var eventMessage = new EventMessage
        {
            Module = ModuleNames.MOD,
            Type = ModEvents.CATEGORY_UPDATED,
            Payload = new { sha = "test-sha", category = "test-category" }
        };

        // Act
        await _registeredHandlers[ModEvents.CATEGORY_UPDATED](eventMessage);

        // Assert
        _mockLogger.Verify(
            x => x.Info(
                It.Is<string>(msg => msg.Contains("Received MOD.CATEGORY_UPDATED")),
                "CategoryEventHandler"),
            Times.Once,
            "Should log when event is received"
        );

        _mockLogger.Verify(
            x => x.Info(
                It.Is<string>(msg => msg.Contains("Cache invalidated")),
                "CategoryEventHandler"),
            Times.Once,
            "Should log after cache invalidation"
        );
    }

    [Fact]
    public void Dispose_ShouldUnregisterHandler()
    {
        // Act
        _handler.Dispose();

        // Assert
        // The handler subscribes to 3 events (CATEGORY_UPDATED, IMPORTED, DELETED)
        // So it should unsubscribe 3 times
        _mockEventBus.Verify(
            x => x.Unsubscribe("test-handler-id"),
            Times.Exactly(3),
            "Should unregister all 3 handlers on dispose"
        );
    }

    public void Dispose()
    {
        _handler?.Dispose();
    }
}
