using System.Text.Json;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Tool.Models;
using D3dxSkinManager.Modules.Tool.Services;
using ModEntity = D3dxSkinManager.Modules.Mod.Entities.ModEntity;
using ModInfo = D3dxSkinManager.Modules.Mod.Models.ModInfo;

namespace D3dxSkinManager.Tests.Modules.Tool.Services;

public class ModAnalysisServiceTests : IDisposable
{
    private readonly Mock<IProfilePathService> _mockPathService;
    private readonly Mock<IModRepository> _mockModRepository;
    private readonly Mock<IModEnrichmentService> _mockEnrichmentService;
    private readonly Mock<IModArchiveService> _mockArchiveService;
    private readonly Mock<IModAnalysisRepository> _mockAnalysisRepository;
    private readonly Mock<ICategoryService> _mockCategoryService;
    private readonly Mock<IProfileEventBus> _mockEventBus;
    private readonly Mock<ILogHelper> _mockLogger;
    private readonly ModAnalysisService _service;
    private readonly string _tempDir;

    public ModAnalysisServiceTests()
    {
        _mockPathService = new Mock<IProfilePathService>();
        _mockModRepository = new Mock<IModRepository>();
        _mockEnrichmentService = new Mock<IModEnrichmentService>();
        _mockArchiveService = new Mock<IModArchiveService>();
        _mockAnalysisRepository = new Mock<IModAnalysisRepository>();
        _mockCategoryService = new Mock<ICategoryService>();
        _mockEventBus = new Mock<IProfileEventBus>();
        _mockLogger = new Mock<ILogHelper>();

        _tempDir = Path.Combine(Path.GetTempPath(), $"d3dx_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _mockPathService.Setup(p => p.CacheModsDirectory).Returns(Path.Combine(_tempDir, "mods"));
        _mockPathService.Setup(p => p.TempDirectory).Returns(Path.Combine(_tempDir, "temp"));
        _mockPathService.Setup(p => p.TdMigotoDirectory).Returns(Path.Combine(_tempDir, "3dmigoto"));
        _mockPathService.Setup(p => p.PreviewsDirectory).Returns(Path.Combine(_tempDir, "previews"));

        _mockEventBus
            .Setup(e => e.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _service = new ModAnalysisService(
            _mockPathService.Object,
            _mockModRepository.Object,
            _mockEnrichmentService.Object,
            _mockArchiveService.Object,
            _mockAnalysisRepository.Object,
            _mockCategoryService.Object,
            _mockEventBus.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ===== Pause/Resume State Machine =====

    [Fact]
    public void PauseAnalysis_WhenNotRunning_ShouldNotThrow()
    {
        // Act — should not throw even if no scan is running
        _service.PauseAnalysis();
        _service.IsPaused.Should().BeFalse("pause request is ignored when not running");
    }

    [Fact]
    public void ResumeAnalysis_WhenNotRunning_ShouldNotThrow()
    {
        // Act — should not throw even if no scan is running
        _service.ResumeAnalysis();
        _service.IsPaused.Should().BeFalse();
    }

    // ===== Session Management =====

    [Fact]
    public async Task GetSessionHistoryAsync_ShouldReturnSessions()
    {
        // Arrange
        var sessions = new List<AnalysisSessionEntity>
        {
            new() { Id = "s1", Status = "completed", TotalMods = 10, StartedAt = "2026-04-13T10:00:00Z" },
            new() { Id = "s2", Status = "cancelled", TotalMods = 5, StartedAt = "2026-04-13T11:00:00Z" },
        };
        _mockAnalysisRepository.Setup(r => r.GetAllSessionsAsync()).ReturnsAsync(sessions);

        // Act
        var result = await _service.GetSessionHistoryAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("s1");
        result[0].Status.Should().Be("completed");
        result[1].Id.Should().Be("s2");
        result[1].Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task GetSessionHistoryAsync_StaleRunningSession_ShouldShowAsPaused()
    {
        // Arrange — a "running" session with no active task should be shown as "paused"
        var sessions = new List<AnalysisSessionEntity>
        {
            new() { Id = "stale-1", Status = "running", TotalMods = 10, StartedAt = "2026-04-13T10:00:00Z" },
        };
        _mockAnalysisRepository.Setup(r => r.GetAllSessionsAsync()).ReturnsAsync(sessions);

        // Act
        var result = await _service.GetSessionHistoryAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("paused", "stale 'running' sessions with no active task should appear as 'paused'");
    }

    [Fact]
    public async Task DeleteSessionAsync_ShouldCallRepository()
    {
        // Act
        await _service.DeleteSessionAsync("session-abc");

        // Assert
        _mockAnalysisRepository.Verify(r => r.DeleteSessionAsync("session-abc"), Times.Once);
    }

    [Fact]
    public async Task ClearAllSessionsAsync_ShouldCallRepository()
    {
        // Act
        await _service.ClearAllSessionsAsync();

        // Assert
        _mockAnalysisRepository.Verify(r => r.ClearAllSessionsAsync(), Times.Once);
    }

    // ===== Report Building (Duplicate Grouping + Conflict Detection) =====

    [Fact]
    public async Task GetSessionReportAsync_ShouldGroupIdenticalDuplicates()
    {
        // Arrange — two mods with same buffer hash → identical duplicates
        var session = new AnalysisSessionEntity
        {
            Id = "dup-session", Status = "completed", TotalMods = 2, StartedAt = "2026-04-13T10:00:00Z"
        };
        var findings = new List<AnalysisFindingEntity>
        {
            new()
            {
                SessionId = "dup-session", ModId = "mod-a",
                BufferHash = "AABB", TextureHash = "CCDD",
                TargetHashes = "[\"hash1\"]", HealthStatus = "healthy",
                HealthIssues = "[]", PluginDependencies = "[]",
            },
            new()
            {
                SessionId = "dup-session", ModId = "mod-b",
                BufferHash = "AABB", TextureHash = "CCDD",
                TargetHashes = "[\"hash1\"]", HealthStatus = "healthy",
                HealthIssues = "[]", PluginDependencies = "[]",
            },
        };

        _mockAnalysisRepository.Setup(r => r.GetSessionAsync("dup-session")).ReturnsAsync(session);
        _mockAnalysisRepository.Setup(r => r.GetFindingsBySessionAsync("dup-session")).ReturnsAsync(findings);
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModEntity>());
        _mockEnrichmentService.Setup(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>())).ReturnsAsync(new List<ModInfo>());

        // Act
        var report = await _service.GetSessionReportAsync("dup-session");

        // Assert
        report.DuplicateGroups.Should().HaveCount(1);
        report.DuplicateGroups[0].Type.Should().Be(DuplicateType.Identical);
        report.DuplicateGroups[0].Mods.Should().HaveCount(2);
        report.IdenticalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetSessionReportAsync_ShouldGroupTextureVariants()
    {
        // Arrange — two mods with same buffer hash but different texture hash → texture variant
        var session = new AnalysisSessionEntity
        {
            Id = "var-session", Status = "completed", TotalMods = 2, StartedAt = "2026-04-13T10:00:00Z"
        };
        var findings = new List<AnalysisFindingEntity>
        {
            new()
            {
                SessionId = "var-session", ModId = "mod-a",
                BufferHash = "AABB", TextureHash = "TEX1",
                TargetHashes = "[\"hash1\"]", HealthStatus = "healthy",
                HealthIssues = "[]", PluginDependencies = "[]",
            },
            new()
            {
                SessionId = "var-session", ModId = "mod-b",
                BufferHash = "AABB", TextureHash = "TEX2",
                TargetHashes = "[\"hash1\"]", HealthStatus = "healthy",
                HealthIssues = "[]", PluginDependencies = "[]",
            },
        };

        _mockAnalysisRepository.Setup(r => r.GetSessionAsync("var-session")).ReturnsAsync(session);
        _mockAnalysisRepository.Setup(r => r.GetFindingsBySessionAsync("var-session")).ReturnsAsync(findings);
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModEntity>());
        _mockEnrichmentService.Setup(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>())).ReturnsAsync(new List<ModInfo>());

        // Act
        var report = await _service.GetSessionReportAsync("var-session");

        // Assert
        report.DuplicateGroups.Should().HaveCount(1);
        report.DuplicateGroups[0].Type.Should().Be(DuplicateType.TextureVariant);
        report.TextureVariantCount.Should().Be(1);
    }

    [Fact]
    public async Task GetSessionReportAsync_ShouldDetectConflicts()
    {
        // Arrange — two loaded mods sharing the same target hash → conflict
        var session = new AnalysisSessionEntity
        {
            Id = "conflict-session", Status = "completed", TotalMods = 2, StartedAt = "2026-04-13T10:00:00Z"
        };
        var findings = new List<AnalysisFindingEntity>
        {
            new()
            {
                SessionId = "conflict-session", ModId = "mod-a",
                BufferHash = "BUF1", TextureHash = "TEX1",
                TargetHashes = "[\"shared-hash\"]", HealthStatus = "healthy",
                HealthIssues = "[]", PluginDependencies = "[]",
            },
            new()
            {
                SessionId = "conflict-session", ModId = "mod-b",
                BufferHash = "BUF2", TextureHash = "TEX2",
                TargetHashes = "[\"shared-hash\"]", HealthStatus = "healthy",
                HealthIssues = "[]", PluginDependencies = "[]",
            },
        };

        // Both mods are loaded
        var modInfos = new List<ModInfo>
        {
            new() { Id = "mod-a", Name = "ModA", IsLoaded = true },
            new() { Id = "mod-b", Name = "ModB", IsLoaded = true },
        };

        _mockAnalysisRepository.Setup(r => r.GetSessionAsync("conflict-session")).ReturnsAsync(session);
        _mockAnalysisRepository.Setup(r => r.GetFindingsBySessionAsync("conflict-session")).ReturnsAsync(findings);
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModEntity>());
        _mockEnrichmentService.Setup(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>())).ReturnsAsync(modInfos);

        // Act
        var report = await _service.GetSessionReportAsync("conflict-session");

        // Assert
        report.Conflicts.Should().HaveCount(1);
        report.Conflicts[0].Hash.Should().Be("shared-hash");
        report.Conflicts[0].Mods.Should().HaveCount(2);
        report.ConflictCount.Should().Be(1);
    }

    [Fact]
    public async Task GetSessionReportAsync_ShouldCountHealthStatuses()
    {
        // Arrange
        var session = new AnalysisSessionEntity
        {
            Id = "count-session", Status = "completed", TotalMods = 3, StartedAt = "2026-04-13T10:00:00Z"
        };
        var findings = new List<AnalysisFindingEntity>
        {
            new()
            {
                SessionId = "count-session", ModId = "mod-1",
                HealthStatus = "healthy", TargetHashes = "[]",
                HealthIssues = "[]", PluginDependencies = "[]",
            },
            new()
            {
                SessionId = "count-session", ModId = "mod-2",
                HealthStatus = "warning", TargetHashes = "[]",
                HealthIssues = JsonSerializer.Serialize(new[] { new { Type = "StaleHash", Severity = "Warning", Message = "Stale" } }),
                PluginDependencies = "[]",
            },
            new()
            {
                SessionId = "count-session", ModId = "mod-3",
                HealthStatus = "error", TargetHashes = "[]",
                HealthIssues = JsonSerializer.Serialize(new[] { new { Type = "NoIniFile", Severity = "Error", Message = "No ini" } }),
                PluginDependencies = "[]",
            },
        };

        _mockAnalysisRepository.Setup(r => r.GetSessionAsync("count-session")).ReturnsAsync(session);
        _mockAnalysisRepository.Setup(r => r.GetFindingsBySessionAsync("count-session")).ReturnsAsync(findings);
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModEntity>());
        _mockEnrichmentService.Setup(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>())).ReturnsAsync(new List<ModInfo>());

        // Act
        var report = await _service.GetSessionReportAsync("count-session");

        // Assert
        report.HealthyCount.Should().Be(1);
        report.WarningCount.Should().Be(1);
        report.ErrorCount.Should().Be(1);
        report.AnalyzedCount.Should().Be(3);
    }

    // ===== Cancel =====

    [Fact]
    public async Task CancelAnalysisAsync_WhenNotRunning_ShouldCancelStaleSessionsInDb()
    {
        // Arrange — a stale "running" session in DB, no active task
        var staleSessions = new List<AnalysisSessionEntity>
        {
            new() { Id = "stale-1", Status = "running", TotalMods = 10, StartedAt = "2026-04-13T10:00:00Z" },
        };
        _mockAnalysisRepository.Setup(r => r.GetAllSessionsAsync()).ReturnsAsync(staleSessions);
        _mockAnalysisRepository.Setup(r => r.GetSessionAsync("stale-1")).ReturnsAsync(staleSessions[0]);
        _mockAnalysisRepository.Setup(r => r.GetFindingsBySessionAsync("stale-1")).ReturnsAsync(new List<AnalysisFindingEntity>());
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModEntity>());
        _mockEnrichmentService.Setup(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>())).ReturnsAsync(new List<ModInfo>());

        // Act
        var report = await _service.CancelAnalysisAsync();

        // Assert
        report.Should().NotBeNull();
        _mockAnalysisRepository.Verify(r => r.UpdateSessionAsync(It.Is<AnalysisSessionEntity>(s => s.Status == "cancelled")), Times.Once);
    }

    // ===== Category Descendant Scanning =====

    [Fact]
    public async Task StartAnalysisAsync_WithCategoryId_ShouldIncludeDescendantCategories()
    {
        // Arrange — parent category "cat-parent" has child "cat-child"
        // Mods in both categories should be included in the scan
        var parentId = "cat-parent";
        var childId = "cat-child";

        _mockCategoryService
            .Setup(c => c.GetAllDescendantIdsAsync(parentId))
            .ReturnsAsync(new List<string> { parentId, childId });
        _mockCategoryService
            .Setup(c => c.GetCategoryNameAsync(parentId))
            .ReturnsAsync("Parent Category");

        var modEntities = new List<ModEntity>
        {
            new() { Id = "mod-in-parent", Name = "ModParent", Category = parentId },
            new() { Id = "mod-in-child", Name = "ModChild", Category = childId },
            new() { Id = "mod-other", Name = "ModOther", Category = "cat-unrelated" },
        };
        var enrichedMods = modEntities.Select(e => new ModInfo
        {
            Id = e.Id, Name = e.Name, Category = e.Category,
            HasCache = false, IsAvailable = false
        }).ToList();

        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(modEntities);
        _mockEnrichmentService
            .Setup(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>()))
            .ReturnsAsync(enrichedMods);

        // Capture the created session and wire up report-building mocks
        AnalysisSessionEntity? capturedSession = null;
        _mockAnalysisRepository
            .Setup(r => r.CreateSessionAsync(It.IsAny<AnalysisSessionEntity>()))
            .Callback<AnalysisSessionEntity>(s => capturedSession = s)
            .ReturnsAsync((AnalysisSessionEntity s) => s.Id);
        _mockAnalysisRepository
            .Setup(r => r.GetSessionAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => capturedSession?.Id == id ? capturedSession : null);
        _mockAnalysisRepository
            .Setup(r => r.GetFindingsBySessionAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<AnalysisFindingEntity>());

        // Act
        var report = await _service.StartAnalysisAsync(parentId);

        // Assert — session should include mods from both parent and child categories (2 mods, not 1)
        _mockAnalysisRepository.Verify(r => r.CreateSessionAsync(
            It.Is<AnalysisSessionEntity>(s =>
                s.CategoryId == parentId &&
                s.CategoryName == "Parent Category" &&
                s.TotalMods == 2)), Times.Once);
    }

    [Fact]
    public async Task StartAnalysisAsync_WithoutCategoryId_ShouldIncludeAllMods()
    {
        // Arrange — no category filter, should include all mods
        var modEntities = new List<ModEntity>
        {
            new() { Id = "mod-1", Name = "Mod1", Category = "cat-a" },
            new() { Id = "mod-2", Name = "Mod2", Category = "cat-b" },
        };
        var enrichedMods = modEntities.Select(e => new ModInfo
        {
            Id = e.Id, Name = e.Name, Category = e.Category,
            HasCache = false, IsAvailable = false
        }).ToList();

        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(modEntities);
        _mockEnrichmentService
            .Setup(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>()))
            .ReturnsAsync(enrichedMods);

        // Capture the created session and wire up report-building mocks
        AnalysisSessionEntity? capturedSession = null;
        _mockAnalysisRepository
            .Setup(r => r.CreateSessionAsync(It.IsAny<AnalysisSessionEntity>()))
            .Callback<AnalysisSessionEntity>(s => capturedSession = s)
            .ReturnsAsync((AnalysisSessionEntity s) => s.Id);
        _mockAnalysisRepository
            .Setup(r => r.GetSessionAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => capturedSession?.Id == id ? capturedSession : null);
        _mockAnalysisRepository
            .Setup(r => r.GetFindingsBySessionAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<AnalysisFindingEntity>());

        // Act
        var report = await _service.StartAnalysisAsync(null);

        // Assert — should include all mods, no category filtering
        _mockAnalysisRepository.Verify(r => r.CreateSessionAsync(
            It.Is<AnalysisSessionEntity>(s =>
                s.CategoryId == null &&
                s.TotalMods == 2)), Times.Once);

        // Should NOT call GetAllDescendantIdsAsync when no category filter
        _mockCategoryService.Verify(c => c.GetAllDescendantIdsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StartAnalysisAsync_WithCategoryId_ShouldDetectDuplicatesAcrossChildCategories()
    {
        // Arrange — two mods in different child categories with same buffer hash
        // Should be detected as duplicates when scanning parent category
        var parentId = "cat-parent";
        var child1Id = "cat-child1";
        var child2Id = "cat-child2";

        _mockCategoryService
            .Setup(c => c.GetAllDescendantIdsAsync(parentId))
            .ReturnsAsync(new List<string> { parentId, child1Id, child2Id });
        _mockCategoryService
            .Setup(c => c.GetCategoryNameAsync(parentId))
            .ReturnsAsync("Parent");

        // Session and findings for report building
        var session = new AnalysisSessionEntity
        {
            Id = "cross-cat-session", CategoryId = parentId, Status = "completed",
            TotalMods = 2, StartedAt = "2026-04-13T10:00:00Z"
        };
        var findings = new List<AnalysisFindingEntity>
        {
            new()
            {
                SessionId = "cross-cat-session", ModId = "mod-child1",
                BufferHash = "SAME_HASH", TextureHash = "SAME_TEX",
                TargetHashes = "[\"hash1\"]", HealthStatus = "healthy",
                HealthIssues = "[]", PluginDependencies = "[]",
            },
            new()
            {
                SessionId = "cross-cat-session", ModId = "mod-child2",
                BufferHash = "SAME_HASH", TextureHash = "SAME_TEX",
                TargetHashes = "[\"hash1\"]", HealthStatus = "healthy",
                HealthIssues = "[]", PluginDependencies = "[]",
            },
        };

        var enrichedMods = new List<ModInfo>
        {
            new() { Id = "mod-child1", Name = "Child1Mod", Category = child1Id, CategoryName = "Child 1" },
            new() { Id = "mod-child2", Name = "Child2Mod", Category = child2Id, CategoryName = "Child 2" },
        };

        _mockAnalysisRepository.Setup(r => r.GetSessionAsync("cross-cat-session")).ReturnsAsync(session);
        _mockAnalysisRepository.Setup(r => r.GetFindingsBySessionAsync("cross-cat-session")).ReturnsAsync(findings);
        _mockModRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModEntity>());
        _mockEnrichmentService.Setup(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>())).ReturnsAsync(enrichedMods);

        // Act
        var report = await _service.GetSessionReportAsync("cross-cat-session");

        // Assert — duplicates across child categories should be detected
        report.DuplicateGroups.Should().HaveCount(1);
        report.DuplicateGroups[0].Type.Should().Be(DuplicateType.Identical);
        report.DuplicateGroups[0].Mods.Should().HaveCount(2);
        report.IdenticalCount.Should().Be(1);
    }
}
