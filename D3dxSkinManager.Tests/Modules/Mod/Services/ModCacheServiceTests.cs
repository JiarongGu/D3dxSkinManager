using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Tests for ModCacheService file-operation serialization.
/// Regression guard for the 2026-06-17 fix: CleanCacheAsync must submit deletes to the
/// FileOperationPlanner (serialized) instead of calling raw Directory.Delete (races the planner).
/// See .claude/rules/filesystem-operation-serialization.md
/// </summary>
public class ModCacheServiceTests : IDisposable
{
    private readonly Mock<IProfilePathService> _mockProfilePaths;
    private readonly Mock<IFileOperationPlanner> _mockPlanner;
    private readonly Mock<IModRepository> _mockRepository;
    private readonly Mock<IProfileService> _mockProfileService;
    private readonly Mock<IProfileContext> _mockProfileContext;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly ModCacheService _service;
    private readonly string _cacheDir;

    public ModCacheServiceTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), "d3dx-cache-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheDir);

        _mockProfilePaths = new Mock<IProfilePathService>();
        _mockProfilePaths.Setup(x => x.CacheModsDirectory).Returns(_cacheDir);

        _mockPlanner = new Mock<IFileOperationPlanner>();
        _mockRepository = new Mock<IModRepository>();
        _mockProfileService = new Mock<IProfileService>();
        _mockProfileContext = new Mock<IProfileContext>();
        _mockLogger = new Mock<ILogHelper>();
        _mockEventBus = new Mock<IProfileEventBus>();

        _mockEventBus
            .Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _service = new ModCacheService(
            _mockProfilePaths.Object,
            _mockPlanner.Object,
            _mockRepository.Object,
            _mockProfileService.Object,
            _mockProfileContext.Object,
            _mockLogger.Object,
            _mockEventBus.Object,
            Mock.Of<D3dxSkinManager.Modules.Core.Services.IProcessRegistry>());
    }

    [Fact]
    public async Task CleanCacheAsync_DeletesViaPlanner_NotRawFileSystem()
    {
        // Arrange: one orphaned disabled cache (id not present in DB => CacheCategory.Invalid)
        var orphanDir = Path.Combine(_cacheDir, "DISABLED-orphan1");
        Directory.CreateDirectory(orphanDir);

        _mockRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<ModEntity>());
        _mockRepository.Setup(x => x.GetLoadedIdsAsync()).ReturnsAsync(new List<string>());

        FileSystemOperation? submitted = null;
        _mockPlanner
            .Setup(x => x.SubmitOperationAsync(It.IsAny<FileSystemOperation>()))
            .Callback<FileSystemOperation>(op => submitted = op)
            .ReturnsAsync(FileSystemOperationResult.Ok());

        // Act
        var deleted = await _service.CleanCacheAsync(CacheCategory.Invalid);

        // Assert: routed through the planner, not a raw delete
        deleted.Should().Be(1);
        submitted.Should().NotBeNull();
        submitted!.OperationType.Should().Be(FileSystemOperationType.DeleteDirectory);
        submitted.SourcePath.Should().Be(orphanDir);

        // Planner is mocked, so the real folder must still exist — proves no raw Directory.Delete
        Directory.Exists(orphanDir).Should().BeTrue("CleanCacheAsync must not delete the folder directly");

        // Refresh event emitted so the UI updates
        _mockEventBus.Verify(
            x => x.EmitAsync(ModuleNames.MOD, ModEvents.CACHE_CHANGED, It.IsAny<object>()),
            Times.Once);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_cacheDir))
                Directory.Delete(_cacheDir, recursive: true);
        }
        catch { /* best effort cleanup */ }
    }
}
