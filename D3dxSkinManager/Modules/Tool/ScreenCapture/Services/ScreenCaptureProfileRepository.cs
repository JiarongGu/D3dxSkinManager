using D3dxSkinManager.Modules.Tool.ScreenCapture.Models;
using D3dxSkinManager.Modules.Context.Services;
using Microsoft.Data.Sqlite;

namespace D3dxSkinManager.Modules.Tool.ScreenCapture.Services;

/// <summary>
/// Interface for screen capture profile data access
/// </summary>
public interface IScreenCaptureProfileRepository
{
    Task<List<ScreenCaptureProfile>> GetAllAsync();
    Task<string> InsertAsync(ScreenCaptureProfile profile);
    Task UpdateAsync(ScreenCaptureProfile profile);
    Task DeleteAsync(string id);
    Task<int> CountAsync();
}

/// <summary>
/// Repository for screen capture profile data access
/// SQLite-based storage for screen capture profiles in the profile database
/// </summary>
public class ScreenCaptureProfileRepository : IScreenCaptureProfileRepository
{
    private readonly string _connectionString;

    public ScreenCaptureProfileRepository(IProfilePathService profilePaths)
    {
        // Check if ProfileDatabasePath is already a full connection string (used in tests)
        // or just a file path (used in production)
        var path = profilePaths.ProfileDatabasePath;
        _connectionString = path.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"Data Source={path}";
    }

    public async Task<List<ScreenCaptureProfile>> GetAllAsync()
    {

        var profiles = new List<ScreenCaptureProfile>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM ScreenCaptureProfiles ORDER BY Name ASC";
        await using var command = new SqliteCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            profiles.Add(ReadProfile(reader));
        }

        return profiles;
    }

    public async Task<string> InsertAsync(ScreenCaptureProfile profile)
    {
        if (string.IsNullOrEmpty(profile.Id))
        {
            profile.Id = Guid.NewGuid().ToString();
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO ScreenCaptureProfiles
            (Id, Name, X, Y, Width, Height)
            VALUES
            (@Id, @Name, @X, @Y, @Width, @Height)
        ";

        await using var command = new SqliteCommand(sql, connection);
        AddProfileParameters(command, profile);
        await command.ExecuteNonQueryAsync();

        return profile.Id;
    }

    public async Task UpdateAsync(ScreenCaptureProfile profile)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            UPDATE ScreenCaptureProfiles
            SET Name = @Name, X = @X, Y = @Y, Width = @Width, Height = @Height
            WHERE Id = @Id
        ";

        await using var command = new SqliteCommand(sql, connection);
        AddProfileParameters(command, profile);
        var rowsAffected = await command.ExecuteNonQueryAsync();

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Profile with ID {profile.Id} not found");
        }

    }

    public async Task DeleteAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "DELETE FROM ScreenCaptureProfiles WHERE Id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Profile with ID {id} not found");
        }
    }

    public async Task<int> CountAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT COUNT(*) FROM ScreenCaptureProfiles";
        await using var command = new SqliteCommand(sql, connection);

        var result = await command.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

        private void AddProfileParameters(SqliteCommand command, ScreenCaptureProfile profile)
    {
        command.Parameters.AddWithValue("@Id", profile.Id);
        command.Parameters.AddWithValue("@Name", profile.Name);
        command.Parameters.AddWithValue("@X", profile.X);
        command.Parameters.AddWithValue("@Y", profile.Y);
        command.Parameters.AddWithValue("@Width", profile.Width);
        command.Parameters.AddWithValue("@Height", profile.Height);
    }

        private ScreenCaptureProfile ReadProfile(SqliteDataReader reader)
    {
        return new ScreenCaptureProfile
        {
            Id = reader.GetString(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            X = reader.GetInt32(reader.GetOrdinal("X")),
            Y = reader.GetInt32(reader.GetOrdinal("Y")),
            Width = reader.GetInt32(reader.GetOrdinal("Width")),
            Height = reader.GetInt32(reader.GetOrdinal("Height"))
        };
    }
}
