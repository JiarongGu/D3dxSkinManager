using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Tests.Modules.Context;

/// <summary>
/// Unit tests for ProfileEventBus
/// Tests profile-scoped event emission and subscription with automatic profileId injection
/// </summary>
public class ProfileEventBusTests
{
    private readonly Mock<IEventBus> _mockGlobalEventBus;
    private readonly Mock<IProfileContext> _mockProfileContext;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly ProfileEventBus _profileEventBus;
    private readonly string _profileId = "test-profile-123";

    public ProfileEventBusTests()
    {
        _mockGlobalEventBus = new Mock<IEventBus>();
        _mockProfileContext = new Mock<IProfileContext>();
        _mockLogger = new Mock<ILogHelper>();

        _mockProfileContext.Setup(x => x.ProfileId).Returns(_profileId);

        _profileEventBus = new ProfileEventBus(
            _mockGlobalEventBus.Object,
            _mockProfileContext.Object,
            _mockLogger.Object
        );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullEventBus_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ProfileEventBus(null!, _mockProfileContext.Object, _mockLogger.Object));

        exception.ParamName.Should().Be("globalEventBus");
    }

    [Fact]
    public void Constructor_WithNullProfileContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ProfileEventBus(_mockGlobalEventBus.Object, null!, _mockLogger.Object));

        exception.ParamName.Should().Be("profileContext");
    }

    #endregion

    #region EmitAsync Tests

    [Fact]
    public async Task EmitAsync_WithModuleAndType_ShouldInjectProfileId()
    {
        // Arrange
        var module = "MOD";
        var type = "LOADED";
        var payload = new { Sha = "abc123" };

        // Act
        await _profileEventBus.EmitAsync(module, type, payload);

        // Assert
        _mockGlobalEventBus.Verify(
            x => x.EmitAsync(module, type, payload, _profileId),
            Times.Once
        );
    }

    [Fact]
    public async Task EmitAsync_WithoutPayload_ShouldStillInjectProfileId()
    {
        // Arrange
        var module = "MOD";
        var type = "LOADED";

        // Act
        await _profileEventBus.EmitAsync(module, type);

        // Assert
        _mockGlobalEventBus.Verify(
            x => x.EmitAsync(module, type, null, _profileId),
            Times.Once
        );
    }

    [Fact]
    public async Task EmitAsync_WithEventMessage_ShouldInjectProfileIdIfNotSet()
    {
        // Arrange
        var message = new EventMessage
        {
            Module = "MOD",
            Type = "LOADED",
            Payload = new { Sha = "abc" }
        };

        // Act
        await _profileEventBus.EmitAsync(message);

        // Assert
        message.ProfileId.Should().Be(_profileId);
        _mockGlobalEventBus.Verify(
            x => x.EmitAsync(message),
            Times.Once
        );
    }

    [Fact]
    public async Task EmitAsync_WithEventMessageHavingProfileId_ShouldNotOverwrite()
    {
        // Arrange
        var existingProfileId = "existing-profile-456";
        var message = new EventMessage
        {
            Module = "MOD",
            Type = "LOADED",
            ProfileId = existingProfileId
        };

        // Act
        await _profileEventBus.EmitAsync(message);

        // Assert
        message.ProfileId.Should().Be(existingProfileId); // Should not be overwritten
        _mockGlobalEventBus.Verify(
            x => x.EmitAsync(message),
            Times.Once
        );
    }

    #endregion

    #region RegisterHandler Tests

    [Fact]
    public void RegisterHandler_ShouldRegisterWithProfileIdFilter()
    {
        // Arrange
        var module = "MOD";
        var type = "LOADED";
        var handler = new Func<EventMessage, Task>(_ => Task.CompletedTask);
        var expectedRegistrationId = "registration-123";

        _mockGlobalEventBus
            .Setup(x => x.RegisterHandler(module, type, _profileId, handler))
            .Returns(expectedRegistrationId);

        // Act
        var registrationId = _profileEventBus.RegisterHandler(module, type, handler);

        // Assert
        registrationId.Should().Be(expectedRegistrationId);
        _mockGlobalEventBus.Verify(
            x => x.RegisterHandler(module, type, _profileId, handler),
            Times.Once
        );
    }

    [Fact]
    public void RegisterHandler_MultipleHandlers_ShouldAllUseProfileId()
    {
        // Arrange
        var handler1 = new Func<EventMessage, Task>(_ => Task.CompletedTask);
        var handler2 = new Func<EventMessage, Task>(_ => Task.CompletedTask);

        _mockGlobalEventBus
            .Setup(x => x.RegisterHandler("MOD", "LOADED", _profileId, It.IsAny<Func<EventMessage, Task>>()))
            .Returns("reg-1");

        _mockGlobalEventBus
            .Setup(x => x.RegisterHandler("TASK_QUEUE", "COMPLETED", _profileId, It.IsAny<Func<EventMessage, Task>>()))
            .Returns("reg-2");

        // Act
        _profileEventBus.RegisterHandler("MOD", "LOADED", handler1);
        _profileEventBus.RegisterHandler("TASK_QUEUE", "COMPLETED", handler2);

        // Assert
        _mockGlobalEventBus.Verify(
            x => x.RegisterHandler("MOD", "LOADED", _profileId, handler1),
            Times.Once
        );
        _mockGlobalEventBus.Verify(
            x => x.RegisterHandler("TASK_QUEUE", "COMPLETED", _profileId, handler2),
            Times.Once
        );
    }

    #endregion

    #region UnregisterHandler Tests

    [Fact]
    public void UnregisterHandler_ShouldDelegateToGlobalEventBus()
    {
        // Arrange
        var registrationId = "registration-123";

        // Act
        _profileEventBus.UnregisterHandler(registrationId);

        // Assert
        _mockGlobalEventBus.Verify(
            x => x.UnregisterHandler(registrationId),
            Times.Once
        );
    }

    #endregion

    #region Integration Tests (with real EventBus)

    [Fact]
    public async Task IntegrationTest_EmitAndSubscribe_ShouldFilterByProfileId()
    {
        // Arrange
        var realEventBus = new EventBus(_mockLogger.Object);
        var profileId = "profile-123";
        var profileContext = Mock.Of<IProfileContext>(x => x.ProfileId == profileId);
        var profileEventBus = new ProfileEventBus(realEventBus, profileContext, _mockLogger.Object);

        var invoked = false;
        EventMessage? receivedMessage = null;

        // Subscribe via profile event bus (filters by profileId)
        profileEventBus.RegisterHandler("MOD", "LOADED", async (msg) =>
        {
            invoked = true;
            receivedMessage = msg;
            await Task.CompletedTask;
        });

        // Act - emit via profile event bus (auto-injects profileId)
        await profileEventBus.EmitAsync("MOD", "LOADED", new { Sha = "abc" });

        // Assert
        invoked.Should().BeTrue();
        receivedMessage.Should().NotBeNull();
        receivedMessage!.ProfileId.Should().Be(profileId);
        receivedMessage.Module.Should().Be("MOD");
        receivedMessage.Type.Should().Be("LOADED");
    }

    [Fact]
    public async Task IntegrationTest_EmitFromDifferentProfile_ShouldNotInvokeHandler()
    {
        // Arrange
        var realEventBus = new EventBus(_mockLogger.Object);

        var profile1Context = Mock.Of<IProfileContext>(x => x.ProfileId == "profile-1");
        var profile2Context = Mock.Of<IProfileContext>(x => x.ProfileId == "profile-2");

        var profile1Bus = new ProfileEventBus(realEventBus, profile1Context, _mockLogger.Object);
        var profile2Bus = new ProfileEventBus(realEventBus, profile2Context, _mockLogger.Object);

        var invoked = false;

        // Subscribe in profile-1
        profile1Bus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            invoked = true;
            await Task.CompletedTask;
        });

        // Act - emit from profile-2
        await profile2Bus.EmitAsync("MOD", "LOADED");

        // Assert - profile-1 handler should NOT be invoked
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task IntegrationTest_MultipleProfiles_ShouldIsolateEvents()
    {
        // Arrange
        var realEventBus = new EventBus(_mockLogger.Object);

        var profile1Context = Mock.Of<IProfileContext>(x => x.ProfileId == "profile-1");
        var profile2Context = Mock.Of<IProfileContext>(x => x.ProfileId == "profile-2");

        var profile1Bus = new ProfileEventBus(realEventBus, profile1Context, _mockLogger.Object);
        var profile2Bus = new ProfileEventBus(realEventBus, profile2Context, _mockLogger.Object);

        var profile1Count = 0;
        var profile2Count = 0;

        // Subscribe both profiles
        profile1Bus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            profile1Count++;
            await Task.CompletedTask;
        });

        profile2Bus.RegisterHandler("MOD", "LOADED", async (_) =>
        {
            profile2Count++;
            await Task.CompletedTask;
        });

        // Act
        await profile1Bus.EmitAsync("MOD", "LOADED"); // Should only trigger profile-1
        await profile2Bus.EmitAsync("MOD", "LOADED"); // Should only trigger profile-2
        await profile1Bus.EmitAsync("MOD", "LOADED"); // Should only trigger profile-1

        // Assert
        profile1Count.Should().Be(2);
        profile2Count.Should().Be(1);
    }

    #endregion
}
