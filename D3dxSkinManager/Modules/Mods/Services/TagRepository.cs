using Microsoft.Data.Sqlite;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Repository for tag-related database operations
/// Responsibility: Tag management within the mods module
/// Tags are stored as JSON arrays in the Mods.Tags column
/// </summary>
public class TagRepository : ITagRepository
{
    private readonly string _connectionString;
    private readonly IModRepository _modRepository;
    private readonly Lazy<Task> _init;

    public TagRepository(IProfilePathService profilePaths, IModRepository modRepository)
    {
        _connectionString = $"Data Source={profilePaths.ProfileDatabasePath}";
        _modRepository = modRepository;
        // No initialization needed - ModRepository creates the schema
        _init = new Lazy<Task>(() => Task.CompletedTask, isThreadSafe: true);
    }

    private Task EnsureInitializedAsync() => _init.Value;

    public async Task<List<string>> GetAllTagsAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var allTags = new HashSet<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Tags FROM Mods WHERE Tags != ''";

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var tagsJson = reader.GetString(0);
            if (!string.IsNullOrEmpty(tagsJson))
            {
                var tags = JsonHelper.Deserialize<List<string>>(tagsJson);
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        allTags.Add(tag);
                    }
                }
            }
        }

        return allTags.OrderBy(t => t).ToList();
    }

    public async Task<List<string>> SearchTagsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllTagsAsync().ConfigureAwait(false);
        }

        var allTags = await GetAllTagsAsync().ConfigureAwait(false);
        var lowerSearch = searchTerm.ToLowerInvariant();

        return allTags
            .Where(tag => tag.ToLowerInvariant().Contains(lowerSearch))
            .ToList();
    }

    public async Task<bool> AddTagToModAsync(string sha, string tag)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var mod = await _modRepository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null) return false;

        // Don't add if already exists
        if (mod.Tags.Contains(tag)) return true;

        mod.Tags.Add(tag);
        return await _modRepository.UpdateAsync(mod).ConfigureAwait(false);
    }

    public async Task<bool> RemoveTagFromModAsync(string sha, string tag)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var mod = await _modRepository.GetByIdAsync(sha).ConfigureAwait(false);
        if (mod == null) return false;

        if (!mod.Tags.Contains(tag)) return true;

        mod.Tags.Remove(tag);
        return await _modRepository.UpdateAsync(mod).ConfigureAwait(false);
    }

    public async Task<int> RenameTagGloballyAsync(string oldTag, string newTag)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var mods = await _modRepository.GetAllAsync().ConfigureAwait(false);
        int updatedCount = 0;

        foreach (var mod in mods)
        {
            if (mod.Tags.Contains(oldTag))
            {
                mod.Tags.Remove(oldTag);
                if (!mod.Tags.Contains(newTag))
                {
                    mod.Tags.Add(newTag);
                }
                if (await _modRepository.UpdateAsync(mod).ConfigureAwait(false))
                {
                    updatedCount++;
                }
            }
        }

        return updatedCount;
    }

    public async Task<int> DeleteTagGloballyAsync(string tag)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var mods = await _modRepository.GetAllAsync().ConfigureAwait(false);
        int updatedCount = 0;

        foreach (var mod in mods)
        {
            if (mod.Tags.Contains(tag))
            {
                mod.Tags.Remove(tag);
                if (await _modRepository.UpdateAsync(mod).ConfigureAwait(false))
                {
                    updatedCount++;
                }
            }
        }

        return updatedCount;
    }

    public async Task<List<string>> GetTagsForModAsync(string sha)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var mod = await _modRepository.GetByIdAsync(sha).ConfigureAwait(false);
        return mod?.Tags ?? new List<string>();
    }

    public async Task<int> GetTagUsageCountAsync(string tag)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        var mods = await _modRepository.GetAllAsync().ConfigureAwait(false);
        return mods.Count(mod => mod.Tags.Contains(tag));
    }
}
