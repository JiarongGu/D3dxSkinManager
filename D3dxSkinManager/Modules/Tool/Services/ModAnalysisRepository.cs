using Dapper;
using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Tool.Services;

// ===== Entities =====

public class AnalysisSessionEntity
{
    public string Id { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Status { get; set; } = "running";
    public int TotalMods { get; set; }
    public int AnalyzedCount { get; set; }
    public int HealthyCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public int IdenticalCount { get; set; }
    public int TextureVariantCount { get; set; }
    public int ConflictCount { get; set; }
    public string StartedAt { get; set; } = string.Empty;
    public string? CompletedAt { get; set; }
}

public class AnalysisFindingEntity
{
    public long Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string ModId { get; set; } = string.Empty;
    public string TargetHashes { get; set; } = "[]";
    public string BufferHash { get; set; } = string.Empty;
    public string TextureHash { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = "unknown";
    public string HealthIssues { get; set; } = "[]";
    public string PluginDependencies { get; set; } = "[]";
    public int IniFileCount { get; set; }
    public int ResourceFileCount { get; set; }
    public int TextureOverrideCount { get; set; }
    public long BufferSizeBytes { get; set; }
    public long TextureSizeBytes { get; set; }
    public string BufferFileHashes { get; set; } = "[]";
    public string TextureFileHashes { get; set; } = "[]";
}

// ===== Repository =====

public interface IModAnalysisRepository
{
    // Sessions
    Task<string> CreateSessionAsync(AnalysisSessionEntity session);
    Task UpdateSessionAsync(AnalysisSessionEntity session);
    Task<AnalysisSessionEntity?> GetSessionAsync(string sessionId);
    Task<List<AnalysisSessionEntity>> GetAllSessionsAsync();
    Task DeleteSessionAsync(string sessionId);
    Task ClearAllSessionsAsync();

    // Findings
    Task InsertFindingAsync(AnalysisFindingEntity finding);
    Task<List<AnalysisFindingEntity>> GetFindingsBySessionAsync(string sessionId);
    Task<int> GetFindingCountBySessionAsync(string sessionId);
    Task DeleteFindingsByModIdAsync(string modId);

    /// <summary>
    /// The most recent finding for every mod (across all sessions), so the mod list can show a
    /// "last scan" health badge. One row per ModId, taken from the newest session that analyzed it.
    /// </summary>
    Task<List<AnalysisFindingEntity>> GetLatestFindingPerModAsync();
}

public class ModAnalysisRepository : IModAnalysisRepository
{
    private readonly string _connectionString;

    public ModAnalysisRepository(IProfilePathService profilePaths)
    {
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
    }

    // ===== Sessions =====

    public async Task<string> CreateSessionAsync(AnalysisSessionEntity session)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(@"
            INSERT INTO AnalysisSessions (Id, CategoryId, CategoryName, Status, TotalMods, AnalyzedCount,
                HealthyCount, WarningCount, ErrorCount, IdenticalCount, TextureVariantCount, ConflictCount,
                StartedAt, CompletedAt)
            VALUES (@Id, @CategoryId, @CategoryName, @Status, @TotalMods, @AnalyzedCount,
                @HealthyCount, @WarningCount, @ErrorCount, @IdenticalCount, @TextureVariantCount, @ConflictCount,
                @StartedAt, @CompletedAt)", session);
        return session.Id;
    }

    public async Task UpdateSessionAsync(AnalysisSessionEntity session)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(@"
            UPDATE AnalysisSessions SET
                Status = @Status, TotalMods = @TotalMods, AnalyzedCount = @AnalyzedCount,
                HealthyCount = @HealthyCount, WarningCount = @WarningCount, ErrorCount = @ErrorCount,
                IdenticalCount = @IdenticalCount, TextureVariantCount = @TextureVariantCount,
                ConflictCount = @ConflictCount, CompletedAt = @CompletedAt
            WHERE Id = @Id", session);
    }

    public async Task<AnalysisSessionEntity?> GetSessionAsync(string sessionId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<AnalysisSessionEntity>(
            "SELECT * FROM AnalysisSessions WHERE Id = @sessionId", new { sessionId });
    }

    public async Task<List<AnalysisSessionEntity>> GetAllSessionsAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        var results = await conn.QueryAsync<AnalysisSessionEntity>(
            "SELECT * FROM AnalysisSessions ORDER BY StartedAt DESC");
        return results.ToList();
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM AnalysisFindings WHERE SessionId = @sessionId", new { sessionId });
        await conn.ExecuteAsync("DELETE FROM AnalysisSessions WHERE Id = @sessionId", new { sessionId });
    }

    public async Task ClearAllSessionsAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM AnalysisFindings");
        await conn.ExecuteAsync("DELETE FROM AnalysisSessions");
    }

    // ===== Findings =====

    public async Task InsertFindingAsync(AnalysisFindingEntity finding)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync(@"
            INSERT INTO AnalysisFindings (SessionId, ModId, TargetHashes, BufferHash, TextureHash,
                HealthStatus, HealthIssues, PluginDependencies, IniFileCount, ResourceFileCount,
                TextureOverrideCount, BufferSizeBytes, TextureSizeBytes, BufferFileHashes, TextureFileHashes)
            VALUES (@SessionId, @ModId, @TargetHashes, @BufferHash, @TextureHash,
                @HealthStatus, @HealthIssues, @PluginDependencies, @IniFileCount, @ResourceFileCount,
                @TextureOverrideCount, @BufferSizeBytes, @TextureSizeBytes, @BufferFileHashes, @TextureFileHashes)", finding);
    }

    public async Task<List<AnalysisFindingEntity>> GetFindingsBySessionAsync(string sessionId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        var results = await conn.QueryAsync<AnalysisFindingEntity>(
            "SELECT * FROM AnalysisFindings WHERE SessionId = @sessionId", new { sessionId });
        return results.ToList();
    }

    public async Task<int> GetFindingCountBySessionAsync(string sessionId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM AnalysisFindings WHERE SessionId = @sessionId", new { sessionId });
    }

    public async Task DeleteFindingsByModIdAsync(string modId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM AnalysisFindings WHERE ModId = @modId", new { modId });
    }

    public async Task<List<AnalysisFindingEntity>> GetLatestFindingPerModAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        // Pick, per ModId, the finding from the session with the newest StartedAt. ROW_NUMBER keeps one
        // row per mod (SQLite supports window functions). Cheap enough for a per-profile findings table.
        var results = await conn.QueryAsync<AnalysisFindingEntity>(@"
            SELECT f.* FROM (
                SELECT f.*, ROW_NUMBER() OVER (PARTITION BY f.ModId ORDER BY s.StartedAt DESC) AS rn
                FROM AnalysisFindings f
                JOIN AnalysisSessions s ON s.Id = f.SessionId
            ) f
            WHERE f.rn = 1");
        return results.ToList();
    }
}
