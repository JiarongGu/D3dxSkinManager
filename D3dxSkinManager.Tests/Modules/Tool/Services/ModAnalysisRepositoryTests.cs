using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Tool.Services;
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Tool.Services;

/// <summary>
/// Tests for ModAnalysisRepository.GetLatestFindingPerModAsync — the window-function query that drives
/// the mod-list "last scan" health badge. Must return exactly one row per mod, taken from the session
/// with the newest StartedAt.
/// </summary>
public class ModAnalysisRepositoryTests : InMemoryDatabaseTestBase
{
    private readonly ModAnalysisRepository _repo;

    public ModAnalysisRepositoryTests()
    {
        // The shared base creates Mods/Categories/etc. but not the analysis tables — add them here
        // (columns match the migration / what the repo reads).
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE AnalysisSessions (
                Id TEXT PRIMARY KEY NOT NULL, CategoryId TEXT, CategoryName TEXT, Status TEXT,
                TotalMods INTEGER, AnalyzedCount INTEGER, HealthyCount INTEGER, WarningCount INTEGER,
                ErrorCount INTEGER, IdenticalCount INTEGER, TextureVariantCount INTEGER, ConflictCount INTEGER,
                StartedAt TEXT, CompletedAt TEXT
            );
            CREATE TABLE AnalysisFindings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, SessionId TEXT, ModId TEXT, TargetHashes TEXT,
                BufferHash TEXT, TextureHash TEXT, HealthStatus TEXT, HealthIssues TEXT, PluginDependencies TEXT,
                IniFileCount INTEGER, ResourceFileCount INTEGER, TextureOverrideCount INTEGER,
                BufferSizeBytes INTEGER, TextureSizeBytes INTEGER, BufferFileHashes TEXT, TextureFileHashes TEXT
            );";
        cmd.ExecuteNonQuery();

        _repo = new ModAnalysisRepository(MockProfilePathService.Object);
    }

    private async Task SeedSessionAsync(string id, string startedAt) =>
        await _repo.CreateSessionAsync(new AnalysisSessionEntity { Id = id, Status = "completed", StartedAt = startedAt });

    private async Task SeedFindingAsync(string sessionId, string modId, string health) =>
        await _repo.InsertFindingAsync(new AnalysisFindingEntity { SessionId = sessionId, ModId = modId, HealthStatus = health });

    [Fact]
    public async Task GetLatestFindingPerModAsync_ReturnsNewestSessionsFindingPerMod()
    {
        // Two sessions; S2 is newer. Mod A scanned in both (error→warning); Mod B only in the older S1.
        await SeedSessionAsync("S1", "2026-06-18T10:00:00");
        await SeedSessionAsync("S2", "2026-06-19T10:00:00");
        await SeedFindingAsync("S1", "A", "error");
        await SeedFindingAsync("S2", "A", "warning");
        await SeedFindingAsync("S1", "B", "error");

        var latest = await _repo.GetLatestFindingPerModAsync();

        // One row per mod, taken from the newest session that analyzed it.
        latest.Should().HaveCount(2);
        latest.Single(f => f.ModId == "A").HealthStatus.Should().Be("warning"); // from S2 (newer)
        latest.Single(f => f.ModId == "B").HealthStatus.Should().Be("error");   // only in S1
    }

    [Fact]
    public async Task GetLatestFindingPerModAsync_EmptyWhenNoFindings()
    {
        var latest = await _repo.GetLatestFindingPerModAsync();
        latest.Should().BeEmpty();
    }
}
