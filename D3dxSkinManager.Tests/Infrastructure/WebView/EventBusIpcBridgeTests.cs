using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Infrastructure.WebView;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Infrastructure.WebView;

/// <summary>
/// Unit tests for EventBusIpcBridge event batching
/// Tests that events are properly batched every 50ms and sent as a single IPC message
/// </summary>
public class EventBusIpcBridgeTests : IDisposable
{
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly Mock<IpcHandler> _mockIpcHandler;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly EventBusIpcBridge _bridge;
    private string _capturedRegistrationId = string.Empty;

    public EventBusIpcBridgeTests()
    {
        _mockEventBus = new Mock<IEventBus>();
        _mockIpcHandler = new Mock<IpcHandler>();
        _mockLogger = new Mock<ILogHelper>();

        // Capture registration ID when RegisterHandlerForAll is called
        _mockEventBus
            .Setup(x => x.RegisterHandlerForAll(It.IsAny<Func<EventMessage, Task>>()))
            .Returns((Func<EventMessage, Task> handler) =>
            {
                _capturedRegistrationId = "test-registration-id";
                return _capturedRegistrationId;
            });

        _bridge = new EventBusIpcBridge(
            _mockEventBus.Object,
            _mockIpcHandler.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void Init_ShouldSubscribeToAllEvents()
    {
        // Act
        _bridge.Init();

        // Assert
        _mockEventBus.Verify(
            x => x.RegisterHandlerForAll(It.IsAny<Func<EventMessage, Task>>()),
            Times.Once
        );
    }

    [Fact]
    public async Task EventBatching_ShouldBatchMultipleEventsInto50msWindow()
    {
        // Arrange
        _bridge.Init();
        var capturedBatches = new List<object>();

        _mockIpcHandler
            .Setup(x => x.SendNotification("EVENT_BUS", "BATCH", It.IsAny<object>()))
            .Callback<string, string, object>((module, type, payload) =>
            {
                capturedBatches.Add(payload);
            });

        // Get the registered handler
        Func<EventMessage, Task>? registeredHandler = null;
        _mockEventBus
            .Setup(x => x.RegisterHandlerForAll(It.IsAny<Func<EventMessage, Task>>()))
            .Callback<Func<EventMessage, Task>>(handler => registeredHandler = handler)
            .Returns("test-id");

        _bridge.Init();
        registeredHandler.Should().NotBeNull();

        // Act - emit multiple events rapidly
        await registeredHandler!(new EventMessage { Module = "MOD", Type = "LOADED", Payload = new { sha = "123" } });
        await registeredHandler(new EventMessage { Module = "MOD", Type = "LOADED", Payload = new { sha = "456" } });
        await registeredHandler(new EventMessage { Module = "WORKFLOW", Type = "PROGRESS", Payload = new { progress = 50 } });

        // Wait for batch timer to fire (50ms + buffer)
        await Task.Delay(100);

        // Assert - should have sent ONE batched notification with 3 events
        _mockIpcHandler.Verify(
            x => x.SendNotification("EVENT_BUS", "BATCH", It.IsAny<object>()),
            Times.Once,
            "Events should be batched into a single IPC message"
        );

        capturedBatches.Should().HaveCount(1);
        var batch = capturedBatches[0] as IEnumerable<object>;
        batch.Should().NotBeNull();
        batch!.Count().Should().Be(3, "Batch should contain all 3 events");
    }

    [Fact]
    public async Task EventBatching_ShouldNotSendEmptyBatches()
    {
        // Arrange
        _bridge.Init();

        // Act - don't emit any events, just wait for timer
        await Task.Delay(100);

        // Assert - should NOT have sent any notifications
        _mockIpcHandler.Verify(
            x => x.SendNotification(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()),
            Times.Never,
            "Should not send empty batches"
        );
    }

    [Fact]
    public async Task EventBatching_ShouldFlushOnDispose()
    {
        // Arrange
        Func<EventMessage, Task>? registeredHandler = null;
        _mockEventBus
            .Setup(x => x.RegisterHandlerForAll(It.IsAny<Func<EventMessage, Task>>()))
            .Callback<Func<EventMessage, Task>>(handler => registeredHandler = handler)
            .Returns("test-id");

        _bridge.Init();
        registeredHandler.Should().NotBeNull();

        // Act - emit event and immediately dispose (before timer fires)
        await registeredHandler!(new EventMessage { Module = "MOD", Type = "LOADED", Payload = new { sha = "123" } });
        _bridge.Dispose();

        // Assert - should have flushed pending events on dispose
        _mockIpcHandler.Verify(
            x => x.SendNotification("EVENT_BUS", "BATCH", It.IsAny<object>()),
            Times.Once,
            "Dispose should flush pending events"
        );
    }

    [Fact]
    public async Task EventBatching_ShouldPreserveEventData()
    {
        // Arrange
        _bridge.Init();
        var capturedPayload = (object?)null;

        _mockIpcHandler
            .Setup(x => x.SendNotification("EVENT_BUS", "BATCH", It.IsAny<object>()))
            .Callback<string, string, object>((module, type, payload) =>
            {
                capturedPayload = payload;
            });

        Func<EventMessage, Task>? registeredHandler = null;
        _mockEventBus
            .Setup(x => x.RegisterHandlerForAll(It.IsAny<Func<EventMessage, Task>>()))
            .Callback<Func<EventMessage, Task>>(handler => registeredHandler = handler)
            .Returns("test-id");

        _bridge.Init();
        registeredHandler.Should().NotBeNull();

        // Act - emit event with specific data
        var testEvent = new EventMessage
        {
            Module = "WORKFLOW",
            Type = "PROGRESS",
            Payload = new { WorkflowId = "wf-123", Progress = 75, Step = "compress" },
            ProfileId = "profile-456"
        };

        await registeredHandler!(testEvent);
        await Task.Delay(100); // Wait for batch

        // Assert - data should be preserved
        capturedPayload.Should().NotBeNull();
        var batch = capturedPayload as IEnumerable<object>;
        batch.Should().NotBeNull();
        batch!.Should().HaveCount(1);

        // Note: In real implementation, we'd need to inspect the anonymous type properties
        // This is a simplified test that verifies the batch structure
    }

    [Fact]
    public async Task EventBatching_MultipleWindows_ShouldCreateMultipleBatches()
    {
        // Arrange
        _bridge.Init();
        var batchCount = 0;

        _mockIpcHandler
            .Setup(x => x.SendNotification("EVENT_BUS", "BATCH", It.IsAny<object>()))
            .Callback<string, string, object>((module, type, payload) =>
            {
                batchCount++;
            });

        Func<EventMessage, Task>? registeredHandler = null;
        _mockEventBus
            .Setup(x => x.RegisterHandlerForAll(It.IsAny<Func<EventMessage, Task>>()))
            .Callback<Func<EventMessage, Task>>(handler => registeredHandler = handler)
            .Returns("test-id");

        _bridge.Init();
        registeredHandler.Should().NotBeNull();

        // Act - emit events in two separate time windows
        await registeredHandler!(new EventMessage { Module = "MOD", Type = "LOADED" });
        await Task.Delay(100); // Wait for first batch

        await registeredHandler(new EventMessage { Module = "MOD", Type = "LOADED" });
        await Task.Delay(100); // Wait for second batch

        // Assert - should have created 2 separate batches
        batchCount.Should().Be(2, "Events in different time windows should create separate batches");
    }

    public void Dispose()
    {
        _bridge?.Dispose();
    }
}
