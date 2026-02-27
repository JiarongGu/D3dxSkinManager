using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Modules.Core.Event;

/// <summary>
/// Unit tests for EventBus
/// Tests event registration, emission, filtering, and profileId handling
/// </summary>
public class EventBusTests
{
    private readonly EventBus _eventBus;
    private readonly Mock<ILogHelper> _mockLogger;

    public EventBusTests()
    {
        _mockLogger = new Mock<ILogHelper>();
        _eventBus = new EventBus(_mockLogger.Object);
    }

    #region Registration Tests

    [Fact]
    public void RegisterHandler_ShouldReturnRegistrationId()
    {
        // Arrange
        var handler = new Func<EventMessage, Task>(_ => Task.CompletedTask);

        // Act
        var registrationId = _eventBus.RegisterHandler("MOD", "LOADED", handler);

        // Assert
        registrationId.Should().NotBeNullOrEmpty();
        registrationId.Should().StartWith("MOD.LOADED");
    }

    [Fact]
    public void RegisterHandlerWithProfileId_ShouldIncludeProfileIdInRegistrationId()
    {
        // Arrange
        var handler = new Func<EventMessage, Task>(_ => Task.CompletedTask);
        var profileId = "profile-123";

        // Act
        var registrationId = _eventBus.RegisterHandler("MOD", "LOADED", profileId, handler);

        // Assert
        registrationId.Should().Contain(profileId);
    }

    [Fact]
    public void RegisterHandlerForModule_WithModuleOnly_ShouldRegisterWildcard()
    {
        // Arrange
        var handler = new Func<EventMessage, Task>(_ => Task.CompletedTask);

        // Act
        var registrationId = _eventBus.RegisterHandlerForModule("MOD", handler);

        // Assert
        registrationId.Should().StartWith("MOD.*");
    }

    [Fact]
    public void RegisterHandlerForModule_WithModuleAndProfileId_ShouldIncludeBoth()
    {
        // Arrange
        var handler = new Func<EventMessage, Task>(_ => Task.CompletedTask);
        var profileId = "profile-123";

        // Act
        var registrationId = _eventBus.RegisterHandlerForModule("MOD", profileId, handler);

        // Assert
        registrationId.Should().StartWith("MOD.*");
        registrationId.Should().Contain(profileId);
    }

    [Fact]
    public void RegisterHandlerForAll_ShouldRegisterDoubleWildcard()
    {
        // Arrange
        var handler = new Func<EventMessage, Task>(_ => Task.CompletedTask);

        // Act
        var registrationId = _eventBus.RegisterHandlerForAll(handler);

        // Assert
        registrationId.Should().StartWith("*.*");
    }

    #endregion

    #region Emission and Handler Invocation Tests

    [Fact]
    public async Task EmitAsync_WithMatchingHandler_ShouldInvokeHandler()
    {
        // Arrange
        var invoked = false;
        var receivedMessage = (EventMessage?)null;

        _eventBus.RegisterHandler("MOD", "LOADED", async (msg) =>
        {
            invoked = true;
            receivedMessage = msg;
            await Task.CompletedTask;
        });

        var payload = new { Sha = "abc123" };

        // Act
        await _eventBus.EmitAsync("MOD", "LOADED", payload);

        // Assert
        invoked.Should().BeTrue();
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Module.Should().Be("MOD");
        receivedMessage.Type.Should().Be("LOADED");
        receivedMessage.Payload.Should().Be(payload);
    }

    [Fact]
    public async Task EmitAsync_WithNonMatchingHandler_ShouldNotInvokeHandler()
    {
        // Arrange
        var invoked = false;

        _eventBus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            invoked = true;
            await Task.CompletedTask;
        });

        // Act - emit different event type
        await _eventBus.EmitAsync("MOD", "UNLOADED");

        // Assert
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task EmitAsync_WithWildcardTypeHandler_ShouldInvokeForAllTypes()
    {
        // Arrange
        var invocations = new List<EventMessage>();

        _eventBus.RegisterHandlerForModule("MOD", async (msg) =>
        {
            invocations.Add(msg);
            await Task.CompletedTask;
        });

        // Act
        await _eventBus.EmitAsync("MOD", "LOADED");
        await _eventBus.EmitAsync("MOD", "UNLOADED");
        await _eventBus.EmitAsync("MOD", "UPDATED");

        // Assert
        invocations.Should().HaveCount(3);
        invocations.Should().Contain(m => m.Type == "LOADED");
        invocations.Should().Contain(m => m.Type == "UNLOADED");
        invocations.Should().Contain(m => m.Type == "UPDATED");
    }

    [Fact]
    public async Task EmitAsync_WithFullWildcardHandler_ShouldInvokeForAllEvents()
    {
        // Arrange
        var invocations = new List<EventMessage>();

        _eventBus.RegisterHandlerForAll(async (msg) =>
        {
            invocations.Add(msg);
            await Task.CompletedTask;
        });

        // Act
        await _eventBus.EmitAsync("MOD", "LOADED");
        await _eventBus.EmitAsync("TASK_QUEUE", "COMPLETED");
        await _eventBus.EmitAsync("CATEGORY", "UPDATED");

        // Assert
        invocations.Should().HaveCount(3);
    }

    [Fact]
    public async Task EmitAsync_WithMultipleHandlers_ShouldInvokeAllMatching()
    {
        // Arrange
        var handler1Invoked = false;
        var handler2Invoked = false;
        var handler3Invoked = false;

        _eventBus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            handler1Invoked = true;
            await Task.CompletedTask;
        });

        _eventBus.RegisterHandlerForModule("MOD", async (_) =>
        {
            handler2Invoked = true;
            await Task.CompletedTask;
        });

        _eventBus.RegisterHandlerForAll(async (_) =>
        {
            handler3Invoked = true;
            await Task.CompletedTask;
        });

        // Act
        await _eventBus.EmitAsync("MOD", "LOADED");

        // Assert
        handler1Invoked.Should().BeTrue();
        handler2Invoked.Should().BeTrue();
        handler3Invoked.Should().BeTrue();
    }

    #endregion

    #region ProfileId Filtering Tests

    [Fact]
    public async Task EmitAsync_WithProfileId_ShouldMatchSpecificProfileHandler()
    {
        // Arrange
        var profileId = "profile-123";
        var invoked = false;

        _eventBus.RegisterHandler("MOD", "LOADED", profileId, async (_) =>
        {
            invoked = true;
            await Task.CompletedTask;
        });

        // Act
        await _eventBus.EmitAsync("MOD", "LOADED", payload: null, profileId: profileId);

        // Assert
        invoked.Should().BeTrue();
    }

    [Fact]
    public async Task EmitAsync_WithDifferentProfileId_ShouldNotInvokeHandler()
    {
        // Arrange
        var invoked = false;

        _eventBus.RegisterHandler("MOD", "LOADED", "profile-123", async (_) =>
        {
            invoked = true;
            await Task.CompletedTask;
        });

        // Act - emit with different profileId
        await _eventBus.EmitAsync("MOD", "LOADED", payload: null, profileId: "profile-456");

        // Assert
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task EmitAsync_GlobalEvent_ShouldInvokeAllHandlersRegardlessOfProfileFilter()
    {
        // Arrange
        var handlerAllProfiles = false;
        var handlerSpecificProfile = false;

        _eventBus.RegisterHandler("CORE", "APPLICATION_STARTED", async (_) =>
        {
            handlerAllProfiles = true;
            await Task.CompletedTask;
        });

        _eventBus.RegisterHandler("CORE", "APPLICATION_STARTED", "profile-123", async (_) =>
        {
            handlerSpecificProfile = true;
            await Task.CompletedTask;
        });

        // Act - emit global event (no profileId)
        await _eventBus.EmitAsync("CORE", "APPLICATION_STARTED");

        // Assert - both handlers should be invoked for global events
        handlerAllProfiles.Should().BeTrue();
        handlerSpecificProfile.Should().BeTrue();
    }

    [Fact]
    public async Task EmitAsync_WithProfileId_ShouldIncludeProfileIdInMessage()
    {
        // Arrange
        var profileId = "profile-123";
        EventMessage? receivedMessage = null;

        _eventBus.RegisterHandlerForAll(async (msg) =>
        {
            receivedMessage = msg;
            await Task.CompletedTask;
        });

        // Act
        await _eventBus.EmitAsync("MOD", "LOADED", payload: null, profileId: profileId);

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.ProfileId.Should().Be(profileId);
    }

    #endregion

    #region Unregister Tests

    [Fact]
    public async Task UnregisterHandler_ShouldPreventHandlerInvocation()
    {
        // Arrange
        var invoked = false;

        var registrationId = _eventBus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            invoked = true;
            await Task.CompletedTask;
        });

        // Act
        _eventBus.UnregisterHandler(registrationId);
        await _eventBus.EmitAsync("MOD", "LOADED");

        // Assert
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task UnregisterHandler_MultipleHandlers_ShouldOnlyRemoveSpecificOne()
    {
        // Arrange
        var handler1Invoked = false;
        var handler2Invoked = false;

        var reg1 = _eventBus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            handler1Invoked = true;
            await Task.CompletedTask;
        });

        _eventBus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            handler2Invoked = true;
            await Task.CompletedTask;
        });

        // Act
        _eventBus.UnregisterHandler(reg1);
        await _eventBus.EmitAsync("MOD", "LOADED");

        // Assert
        handler1Invoked.Should().BeFalse();
        handler2Invoked.Should().BeTrue();
    }

    #endregion

    #region Event Caching Tests

    [Fact]
    public async Task EmitAsync_SameEventTwice_ShouldUseCachedEvaluation()
    {
        // Arrange
        var invocationCount = 0;

        _eventBus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            invocationCount++;
            await Task.CompletedTask;
        });

        // Act - emit same event twice
        await _eventBus.EmitAsync("MOD", "LOADED");
        await _eventBus.EmitAsync("MOD", "LOADED");

        // Assert - handler should be invoked both times (cache is for matching, not execution)
        invocationCount.Should().Be(2);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task EmitAsync_HandlerThrowsException_ShouldNotStopOtherHandlers()
    {
        // Arrange
        var handler2Invoked = false;

        _eventBus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            await Task.CompletedTask;
            throw new Exception("Test exception");
        });

        _eventBus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            handler2Invoked = true;
            await Task.CompletedTask;
        });

        // Act
        await _eventBus.EmitAsync("MOD", "LOADED");

        // Assert - second handler should still be invoked
        handler2Invoked.Should().BeTrue();
    }

    #endregion

    #region EventMessage Tests

    [Fact]
    public async Task EmitAsync_WithEventMessage_ShouldUseMessageProperties()
    {
        // Arrange
        EventMessage? receivedMessage = null;

        _eventBus.RegisterHandlerForAll(async (msg) =>
        {
            receivedMessage = msg;
            await Task.CompletedTask;
        });

        var eventMessage = new EventMessage
        {
            Id = "custom-id",
            Module = "MOD",
            Type = "LOADED",
            ProfileId = "profile-123",
            Payload = new { Sha = "abc" }
        };

        // Act
        await _eventBus.EmitAsync(eventMessage);

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Id.Should().Be("custom-id");
        receivedMessage.Module.Should().Be("MOD");
        receivedMessage.Type.Should().Be("LOADED");
        receivedMessage.ProfileId.Should().Be("profile-123");
    }

    #endregion
}
