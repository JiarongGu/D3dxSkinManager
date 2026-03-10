using Dapper;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Entities;
using Microsoft.Data.Sqlite;

namespace D3dxSkinManager.Modules.Workflow.Repositories;

/// <summary>
/// SQLite implementation of WorkflowRepository
/// Stores workflows in profile-scoped database
/// </summary>
public class WorkflowRepository : IWorkflowRepository
{
    private readonly string _connectionString;

    public WorkflowRepository(IProfilePathService profilePaths)
    {
        // Check if ProfileDatabasePath is already a full connection string (used in tests)
        // or just a file path (used in production)
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
        // Table creation now handled by Fluent migrations (Migration_202603080004_CreateWorkflowsTable)
    }

    public async Task<WorkflowInfo> AddAsync(WorkflowInfo workflow)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entity = workflow.ToEntity();

        await connection.ExecuteAsync(
            @"INSERT INTO Workflows (Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt)
              VALUES (@Id, @Type, @Status, @Context, @ErrorMessage, @CreatedAt, @CompletedAt)",
            new
            {
                entity.Id,
                entity.Type,
                Status = (int)entity.Status,
                entity.Context,
                entity.ErrorMessage,
                CreatedAt = entity.CreatedAt.ToString("O"),
                CompletedAt = entity.CompletedAt?.ToString("O")
            }
        );
        return workflow;
    }

    public async Task<WorkflowInfo?> GetByIdAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entity = await connection.QuerySingleOrDefaultAsync<WorkflowEntity>(
            @"SELECT Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt
              FROM Workflows
              WHERE Id = @Id",
            new { Id = id }
        );

        return entity?.ToDomain();
    }

    public async Task<List<WorkflowInfo>> GetByTypeAsync(string type)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entities = await connection.QueryAsync<WorkflowEntity>(
            @"SELECT Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt
              FROM Workflows
              WHERE Type = @Type
              ORDER BY CreatedAt ASC",
            new { Type = type }
        );

        return entities.ToDomainList();
    }

    public async Task<List<WorkflowInfo>> GetActiveByTypeAsync(string type)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entities = await connection.QueryAsync<WorkflowEntity>(
            @"SELECT Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt
              FROM Workflows
              WHERE Type = @Type AND Status IN (0, 1, 2)
              ORDER BY CreatedAt ASC",
            new { Type = type }
        );

        return entities.ToDomainList();
    }

    public async Task UpdateAsync(WorkflowInfo workflow)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var entity = workflow.ToEntity();

        await connection.ExecuteAsync(
            @"UPDATE Workflows
              SET Type = @Type,
                  Status = @Status,
                  Context = @Context,
                  ErrorMessage = @ErrorMessage,
                  CreatedAt = @CreatedAt,
                  CompletedAt = @CompletedAt
              WHERE Id = @Id",
            new
            {
                entity.Id,
                entity.Type,
                Status = (int)entity.Status,
                entity.Context,
                entity.ErrorMessage,
                CreatedAt = entity.CreatedAt.ToString("O"),
                CompletedAt = entity.CompletedAt?.ToString("O")
            }
        );
    }

    /// <summary>
    /// Update only the Context field of a workflow
    /// This is much more efficient than UpdateAsync for progress updates
    /// </summary>
    public async Task UpdateContextAsync(string workflowId, string context)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            @"UPDATE Workflows
              SET Context = @Context
              WHERE Id = @Id",
            new { Id = workflowId, Context = context }
        );
    }

    public async Task DeleteAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "DELETE FROM Workflows WHERE Id = @Id",
            new { Id = id }
        );
    }

    public async Task<int> DeleteBatchAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return 0;

        await using var connection = new SqliteConnection(_connectionString);

        // Build parameterized query with IN clause
        var parameters = idList.Select((id, index) => $"@id{index}").ToList();
        var inClause = string.Join(",", parameters);

        var dynamicParams = new DynamicParameters();
        for (int i = 0; i < idList.Count; i++)
        {
            dynamicParams.Add($"@id{i}", idList[i]);
        }

        var rowsAffected = await connection.ExecuteAsync(
            $"DELETE FROM Workflows WHERE Id IN ({inClause})",
            dynamicParams
        );

        return rowsAffected;
    }

    public async Task<List<WorkflowInfo>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return new List<WorkflowInfo>();

        await using var connection = new SqliteConnection(_connectionString);

        // Build parameterized query with IN clause
        var parameters = idList.Select((id, index) => $"@id{index}").ToList();
        var inClause = string.Join(",", parameters);

        var dynamicParams = new DynamicParameters();
        for (int i = 0; i < idList.Count; i++)
        {
            dynamicParams.Add($"@id{i}", idList[i]);
        }

        var entities = await connection.QueryAsync<WorkflowEntity>(
            $@"SELECT Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt
               FROM Workflows
               WHERE Id IN ({inClause})
               ORDER BY CreatedAt ASC",
            dynamicParams
        );

        return entities.ToDomainList();
    }
}
