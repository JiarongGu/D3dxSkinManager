using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// The PER-PROFILE configured remote libraries ({profile}/remote-libraries.json) — the redesigned
/// replacement for the single binding (remote-library-redesign.md). A profile owns MANY libraries
/// (site + game + ordered tag→category import rules); the main screen switches between them; library
/// management adds/edits/removes them. A legacy remote-binding.json is auto-upgraded into the first
/// library on first read so existing setups survive.
/// </summary>
public interface IRemoteLibraryStore
{
    RemoteLibrariesState GetState();
    RemoteLibrary Add(string sourceId, string listId, string name, List<RemoteTagRule>? tagRules = null);
    RemoteLibrary Update(RemoteLibrary library);
    bool Remove(string libraryId);
    RemoteLibrariesState SetActive(string libraryId);
    RemoteLibrary? GetActive();

    /// <summary>The first library targeting this source+list (tag-rule lookup for imports).</summary>
    RemoteLibrary? FindBySourceList(string sourceId, string listId);
}

public class RemoteLibraryStore : IRemoteLibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly IProfilePathService _profilePaths;
    private readonly IRemoteSourceStore _sources;
    private readonly ILogHelper _logger;
    private readonly object _lock = new();

    public RemoteLibraryStore(IProfilePathService profilePaths, IRemoteSourceStore sources, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _sources = sources;
        _logger = logger;
    }

    private string FilePath => Path.Combine(_profilePaths.ProfilePath, "remote-libraries.json");
    private string LegacyBindingPath => Path.Combine(_profilePaths.ProfilePath, "remote-binding.json");

    public RemoteLibrariesState GetState()
    {
        lock (_lock)
        {
            var state = Load();
            if (state != null) return state;

            // First read: upgrade a legacy single binding into the first library, so an existing
            // setup keeps working with zero user action.
            state = new RemoteLibrariesState();
            var legacy = LoadLegacyBinding();
            if (legacy != null)
            {
                var library = new RemoteLibrary
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SourceId = legacy.SourceId,
                    ListId = legacy.ListId,
                    Name = ComposeName(legacy.SourceId, legacy.ListId),
                    AddedAtUtc = DateTime.UtcNow,
                };
                state.Libraries.Add(library);
                state.ActiveLibraryId = library.Id;
                _logger.Info($"[Remote] Upgraded legacy binding to library '{library.Name}'", "RemoteLibraryStore");
            }
            Save(state);
            return state;
        }
    }

    public RemoteLibrary Add(string sourceId, string listId, string name, List<RemoteTagRule>? tagRules = null)
    {
        _ = _sources.GetById(sourceId); // validate the source exists
        lock (_lock)
        {
            var state = GetState();
            var library = new RemoteLibrary
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceId = sourceId,
                ListId = listId,
                Name = string.IsNullOrWhiteSpace(name) ? ComposeName(sourceId, listId) : name.Trim(),
                TagRules = tagRules ?? new(),
                AddedAtUtc = DateTime.UtcNow,
            };
            state.Libraries.Add(library);
            state.ActiveLibraryId ??= library.Id; // the first library becomes active automatically
            Save(state);
            return library;
        }
    }

    public RemoteLibrary Update(RemoteLibrary library)
    {
        lock (_lock)
        {
            var state = GetState();
            var index = state.Libraries.FindIndex(l => l.Id == library.Id);
            if (index < 0) throw new OperationException("REMOTE_LIBRARY_NOT_FOUND", "id", library.Id);
            // Identity (source+list) is fixed after creation — only name/rules are editable.
            library.SourceId = state.Libraries[index].SourceId;
            library.ListId = state.Libraries[index].ListId;
            library.AddedAtUtc = state.Libraries[index].AddedAtUtc;
            state.Libraries[index] = library;
            Save(state);
            return library;
        }
    }

    public bool Remove(string libraryId)
    {
        lock (_lock)
        {
            var state = GetState();
            var removed = state.Libraries.RemoveAll(l => l.Id == libraryId) > 0;
            if (removed && state.ActiveLibraryId == libraryId)
                state.ActiveLibraryId = state.Libraries.FirstOrDefault()?.Id;
            if (removed) Save(state);
            return removed;
        }
    }

    public RemoteLibrariesState SetActive(string libraryId)
    {
        lock (_lock)
        {
            var state = GetState();
            if (state.Libraries.All(l => l.Id != libraryId))
                throw new OperationException("REMOTE_LIBRARY_NOT_FOUND", "id", libraryId);
            state.ActiveLibraryId = libraryId;
            Save(state);
            return state;
        }
    }

    public RemoteLibrary? GetActive()
    {
        var state = GetState();
        return state.Libraries.FirstOrDefault(l => l.Id == state.ActiveLibraryId);
    }

    public RemoteLibrary? FindBySourceList(string sourceId, string listId) =>
        GetState().Libraries.FirstOrDefault(l =>
            string.Equals(l.SourceId, sourceId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(l.ListId, listId, StringComparison.OrdinalIgnoreCase));

    // ---- plumbing --------------------------------------------------------------------------

    private RemoteLibrariesState? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonSerializer.Deserialize<RemoteLibrariesState>(File.ReadAllText(FilePath), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] Corrupt remote-libraries.json: {ex.Message}", "RemoteLibraryStore");
            return null;
        }
    }

    private void Save(RemoteLibrariesState state) =>
        File.WriteAllText(FilePath, JsonSerializer.Serialize(state, JsonOptions));

    private RemoteBinding? LoadLegacyBinding()
    {
        try
        {
            if (!File.Exists(LegacyBindingPath)) return null;
            var binding = JsonSerializer.Deserialize<RemoteBinding>(File.ReadAllText(LegacyBindingPath), JsonOptions);
            return string.IsNullOrWhiteSpace(binding?.SourceId) ? null : binding;
        }
        catch { return null; }
    }

    /// <summary>"SiteName · GameName" from the source config (falls back to raw ids).</summary>
    private string ComposeName(string sourceId, string listId)
    {
        try
        {
            var source = _sources.GetById(sourceId);
            var listName = source.Lists.FirstOrDefault(l => l.Id == listId)?.Name ?? listId;
            return $"{source.Name} · {listName}";
        }
        catch { return $"{sourceId} · {listId}"; }
    }
}
