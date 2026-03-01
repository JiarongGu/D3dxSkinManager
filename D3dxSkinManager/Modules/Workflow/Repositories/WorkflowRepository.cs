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
    private readonly Lazy<Task> _init;

    public WorkflowRepository(IProfilePathService profilePaths)
    {
        _connectionString = $"Data Source={profilePaths.ProfileDatabasePath}";
        _init = new Lazy<Task>(InitializeDatabaseAsync, isThreadSafe: true);
    }

    private async Task EnsureInitializedAsync()
    {
        await _init.Value;
    }

    private async Task InitializeDatabaseAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createTableCmd = connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Workflows (
                Id TEXT PRIMARY KEY,
                Type TEXT NOT NULL,
                Status INTEGER NOT NULL,
                Context TEXT NOT NULL DEFAULT '{}',
                ErrorMessage TEXT,
                CreatedAt TEXT NOT NULL,
                CompletedAt TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_workflows_type ON Workflows(Type);
            CREATE INDEX IF NOT EXISTS idx_workflows_status ON Workflows(Status);
        ";
        await createTableCmd.ExecuteNonQueryAsync();
    }

    public async Task<WorkflowInfo> AddAsync(WorkflowInfo workflow)
    {
        await EnsureInitializedAsync();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Workflows (Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt)
            VALUES (@Id, @Type, @Status, @Context, @ErrorMessage, @CreatedAt, @CompletedAt)
        ";
        cmd.Parameters.AddWithValue("@Id", workflow.Id);
        cmd.Parameters.AddWithValue("@Type", workflow.Type);
        cmd.Parameters.AddWithValue("@Status", (int)workflow.Status);
        cmd.Parameters.AddWithValue("@Context", workflow.Context);
        cmd.Parameters.AddWithValue("@ErrorMessage", workflow.ErrorMessage ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", workflow.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CompletedAt", workflow.CompletedAt?.ToString("O") ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
        return workflow;
    }

    public async Task<WorkflowInfo?> GetByIdAsync(string id)
    {
        await EnsureInitializedAsync();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt
            FROM Workflows
            WHERE Id = @Id
        ";
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapToWorkflowInfo(reader);
        }

        return null;
    }

    public async Task<List<WorkflowInfo>> GetByTypeAsync(string type)
    {
        await EnsureInitializedAsync();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt
            FROM Workflows
            WHERE Type = @Type
            ORDER BY CreatedAt DESC
        ";
        cmd.Parameters.AddWithValue("@Type", type);

        var workflows = new List<WorkflowInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            workflows.Add(MapToWorkflowInfo(reader));
        }

        return workflows;
    }

    public async Task<List<WorkflowInfo>> GetActiveByTypeAsync(string type)
    {
        await EnsureInitializedAsync();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var activeStatuses = new[] {
            (int)WorkflowStatus.Pending,
            (int)WorkflowStatus.Processing,
            (int)WorkflowStatus.WaitingForInput
        };

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt
            FROM Workflows
            WHERE Type = @Type AND Status IN (0, 1, 2)
            ORDER BY CreatedAt DESC
        ";
        cmd.Parameters.AddWithValue("@Type", type);

        var workflows = new List<WorkflowInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            workflows.Add(MapToWorkflowInfo(reader));
        }

        return workflows;
    }

    public async Task UpdateAsync(WorkflowInfo workflow)
    {
        await EnsureInitializedAsync();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Workflows
            SET Type = @Type,
                Status = @Status,
                Context = @Context,
                ErrorMessage = @ErrorMessage,
                CreatedAt = @CreatedAt,
                CompletedAt = @CompletedAt
            WHERE Id = @Id
        ";
        cmd.Parameters.AddWithValue("@Id", workflow.Id);
        cmd.Parameters.AddWithValue("@Type", workflow.Type);
        cmd.Parameters.AddWithValue("@Status", (int)workflow.Status);
        cmd.Parameters.AddWithValue("@Context", workflow.Context);
        cmd.Parameters.AddWithValue("@ErrorMessage", workflow.ErrorMessage ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", workflow.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CompletedAt", workflow.CompletedAt?.ToString("O") ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string id)
    {
        await EnsureInitializedAsync();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Workflows WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteBatchAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return 0;

        await EnsureInitializedAsync();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Build parameterized query with IN clause
        var parameters = idList.Select((id, index) => $"@id{index}").ToList();
        var inClause = string.Join(",", parameters);

        var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM Workflows WHERE Id IN ({inClause})";

        for (int i = 0; i < idList.Count; i++)
        {
            cmd.Parameters.AddWithValue($"@id{i}", idList[i]);
        }

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected;
    }

    public async Task<List<WorkflowInfo>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return new List<WorkflowInfo>();

        await EnsureInitializedAsync();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Build parameterized query with IN clause
        var parameters = idList.Select((id, index) => $"@id{index}").ToList();
        var inClause = string.Join(",", parameters);

        var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT Id, Type, Status, Context, ErrorMessage, CreatedAt, CompletedAt
            FROM Workflows
            WHERE Id IN ({inClause})
            ORDER BY CreatedAt DESC
        ";

        for (int i = 0; i < idList.Count; i++)
        {
            cmd.Parameters.AddWithValue($"@id{i}", idList[i]);
        }

        var workflows = new List<WorkflowInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            workflows.Add(MapToWorkflowInfo(reader));
        }

        return workflows;
    }

    private static WorkflowInfo MapToWorkflowInfo(SqliteDataReader reader)
    {
        return new WorkflowInfo
        {
            Id = reader.GetString(0),
            Type = reader.GetString(1),
            Status = (WorkflowStatus)reader.GetInt32(2),
            Context = reader.GetString(3),
            ErrorMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt = DateTime.Parse(reader.GetString(5)),
            CompletedAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6))
        };
    }
}
