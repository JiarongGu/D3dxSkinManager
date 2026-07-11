using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Tool;
using D3dxSkinManager.Modules.Tool.ScreenCapture.Models;
using D3dxSkinManager.Modules.Tool.ScreenCapture.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace D3dxSkinManager.Tests.Modules.Tool.Services;

/// <summary>
/// Tests for ScreenCaptureProfileService — the CRUD + lifecycle-event logic extracted from ToolFacade
/// (B5b). Verifies the insert-vs-update branch, the returned id, and the emitted lifecycle events.
/// </summary>
public class ScreenCaptureProfileServiceTests
{
    private readonly Mock<IScreenCaptureProfileRepository> _repo = new();
    private readonly Mock<IProfileEventBus> _eventBus = new();

    private ScreenCaptureProfileService CreateService()
    {
        _eventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        return new ScreenCaptureProfileService(_repo.Object, _eventBus.Object, Mock.Of<ILogHelper>());
    }

    [Fact]
    public async Task SaveAsync_NoId_InsertsAndEmitsCreated()
    {
        _repo.Setup(r => r.InsertAsync(It.IsAny<ScreenCaptureProfile>())).ReturnsAsync("new-id");
        var service = CreateService();

        var id = await service.SaveAsync(new SaveScreenCaptureProfileRequest
        {
            Id = null, Name = "Cap", X = 1, Y = 2, Width = 3, Height = 4,
        });

        id.Should().Be("new-id");
        _repo.Verify(r => r.InsertAsync(It.Is<ScreenCaptureProfile>(p =>
            p.Name == "Cap" && p.X == 1 && p.Y == 2 && p.Width == 3 && p.Height == 4)), Times.Once);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<ScreenCaptureProfile>()), Times.Never);
        _eventBus.Verify(x => x.EmitAsync(ModuleNames.TOOL, ToolEvents.CAPTURE_PROFILE_CREATED, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_WithId_UpdatesAndEmitsUpdated()
    {
        var service = CreateService();

        var id = await service.SaveAsync(new SaveScreenCaptureProfileRequest
        {
            Id = "existing-id", Name = "Cap2", X = 5, Y = 6, Width = 7, Height = 8,
        });

        id.Should().Be("existing-id");
        _repo.Verify(r => r.UpdateAsync(It.Is<ScreenCaptureProfile>(p =>
            p.Id == "existing-id" && p.Name == "Cap2")), Times.Once);
        _repo.Verify(r => r.InsertAsync(It.IsAny<ScreenCaptureProfile>()), Times.Never);
        _eventBus.Verify(x => x.EmitAsync(ModuleNames.TOOL, ToolEvents.CAPTURE_PROFILE_UPDATED, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DeletesAndEmitsDeleted()
    {
        var service = CreateService();

        await service.DeleteAsync("del-id");

        _repo.Verify(r => r.DeleteAsync("del-id"), Times.Once);
        _eventBus.Verify(x => x.EmitAsync(ModuleNames.TOOL, ToolEvents.CAPTURE_PROFILE_DELETED, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_PassesThroughRepository()
    {
        var profiles = new List<ScreenCaptureProfile> { new() { Id = "a", Name = "A" } };
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(profiles);
        var service = CreateService();

        (await service.GetAllAsync()).Should().BeSameAs(profiles);
    }
}
