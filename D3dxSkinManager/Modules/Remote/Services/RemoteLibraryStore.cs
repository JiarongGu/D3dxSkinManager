using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// The PER-PROFILE configured remote libraries (remote-library-redesign.md). A profile owns MANY
/// libraries (site + game + ordered tag→category rules); the main screen switches between them; library
/// management adds/edits/removes them.
///
/// Storage is now the profile SQLite DB (RemoteLibraries table via <see cref="IRemoteLibraryRepository"/>)
/// — moved off {profile}/remote-libraries.json so library data is native to SQL. On first access the
/// legacy JSON (or an even older remote-binding.json) is migrated into the table once, then removed.
/// </summary>
public interface IRemoteLibraryStore
{
    RemoteLibrariesState GetState();
    RemoteLibrary Add(string sourceId, string listId, string name, List<RemoteTagRule>? tagRules = null, Dictionary<string, string>? paramValues = null);
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
    };

    private readonly IRemoteLibraryRepository _repository;
    private readonly IProfilePathService _profilePaths;
    private readonly IRemoteSourceStore _sources;
    private readonly ILogHelper _logger;
    private readonly object _lock = new();
    private bool _migrationChecked;

    public RemoteLibraryStore(
        IRemoteLibraryRepository repository,
        IProfilePathService profilePaths,
        IRemoteSourceStore sources,
        ILogHelper logger)
    {
        _repository = repository;
        _profilePaths = profilePaths;
        _sources = sources;
        _logger = logger;
    }

    private string LegacyLibrariesPath => Path.Combine(_profilePaths.ProfilePath, "remote-libraries.json");
    private string LegacyBindingPath => Path.Combine(_profilePaths.ProfilePath, "remote-binding.json");

    public RemoteLibrariesState GetState()
    {
        lock (_lock)
        {
            EnsureMigrated();
            return new RemoteLibrariesState
            {
                Libraries = _repository.GetAll(),
                ActiveLibraryId = _repository.GetActiveId(),
            };
        }
    }

    public RemoteLibrary Add(string sourceId, string listId, string name, List<RemoteTagRule>? tagRules = null, Dictionary<string, string>? paramValues = null)
    {
        _ = _sources.GetById(sourceId); // validate the source exists
        lock (_lock)
        {
            EnsureMigrated();
            var library = new RemoteLibrary
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceId = sourceId,
                ListId = listId,
                Name = string.IsNullOrWhiteSpace(name) ? ComposeName(sourceId, listId) : name.Trim(),
                TagRules = tagRules ?? new(),
                ParamValues = paramValues ?? new(),
                AddedAtUtc = DateTime.UtcNow,
            };
            // The first library added becomes active automatically.
            var active = _repository.Count() == 0;
            _repository.Insert(library, _repository.NextSortOrder(), active);
            return library;
        }
    }

    public RemoteLibrary Update(RemoteLibrary library)
    {
        lock (_lock)
        {
            EnsureMigrated();
            var existing = _repository.GetAll().FirstOrDefault(l => l.Id == library.Id)
                ?? throw new OperationException("REMOTE_LIBRARY_NOT_FOUND", "id", library.Id);
            // The library may SWITCH the source/list it references (keeping its id + mod FKs + its own
            // param overrides); validate the (possibly new) source exists. Only creation time stays fixed.
            _ = _sources.GetById(library.SourceId);
            library.AddedAtUtc = existing.AddedAtUtc;
            _repository.Update(library);
            return library;
        }
    }

    public bool Remove(string libraryId)
    {
        lock (_lock)
        {
            EnsureMigrated();
            var wasActive = _repository.GetActiveId() == libraryId;
            var removed = _repository.Delete(libraryId);
            if (removed && wasActive)
            {
                // Promote the first remaining library to active (null when none remain).
                _repository.SetActive(_repository.GetAll().FirstOrDefault()?.Id);
            }
            return removed;
        }
    }

    public RemoteLibrariesState SetActive(string libraryId)
    {
        lock (_lock)
        {
            EnsureMigrated();
            if (_repository.GetAll().All(l => l.Id != libraryId))
                throw new OperationException("REMOTE_LIBRARY_NOT_FOUND", "id", libraryId);
            _repository.SetActive(libraryId);
            return new RemoteLibrariesState
            {
                Libraries = _repository.GetAll(),
                ActiveLibraryId = libraryId,
            };
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

    // ---- one-time JSON → SQLite migration --------------------------------------------------

    /// <summary>Migrate the legacy JSON store into the table ONCE per profile session (only when the
    /// table is still empty). Preserves order + the active selection, then removes the JSON so it can't
    /// re-seed. Falls back to the even-older single remote-binding.json.</summary>
    private void EnsureMigrated()
    {
        if (_migrationChecked) return;
        _migrationChecked = true;

        try
        {
            if (_repository.Count() > 0) return; // already has data — nothing to migrate

            if (TryMigrateLibrariesJson()) return;
            TryUpgradeLegacyBinding();
        }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] Library JSON→SQLite migration skipped: {ex.Message}", "RemoteLibraryStore");
        }
    }

    private bool TryMigrateLibrariesJson()
    {
        if (!File.Exists(LegacyLibrariesPath)) return false;

        RemoteLibrariesState? state;
        try { state = JsonSerializer.Deserialize<RemoteLibrariesState>(File.ReadAllText(LegacyLibrariesPath), JsonOptions); }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] Corrupt remote-libraries.json, not migrated: {ex.Message}", "RemoteLibraryStore");
            TryDelete(LegacyLibrariesPath); // don't retry a corrupt file forever
            return false;
        }

        if (state?.Libraries != null)
        {
            for (var i = 0; i < state.Libraries.Count; i++)
            {
                var lib = state.Libraries[i];
                if (string.IsNullOrWhiteSpace(lib.Id)) lib.Id = Guid.NewGuid().ToString("N");
                _repository.Insert(lib, i, active: lib.Id == state.ActiveLibraryId);
            }
            // If nothing was flagged active but libraries exist, activate the first.
            if (_repository.GetActiveId() == null && state.Libraries.Count > 0)
                _repository.SetActive(state.Libraries[0].Id);
            _logger.Info($"[Remote] Migrated {state.Libraries.Count} librar(ies) from JSON into SQLite", "RemoteLibraryStore");
        }

        TryDelete(LegacyLibrariesPath);
        return true;
    }

    private void TryUpgradeLegacyBinding()
    {
        if (!File.Exists(LegacyBindingPath)) return;
        try
        {
            var binding = JsonSerializer.Deserialize<RemoteBinding>(File.ReadAllText(LegacyBindingPath), JsonOptions);
            if (!string.IsNullOrWhiteSpace(binding?.SourceId))
            {
                var library = new RemoteLibrary
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SourceId = binding!.SourceId,
                    ListId = binding.ListId,
                    Name = ComposeName(binding.SourceId, binding.ListId),
                    AddedAtUtc = DateTime.UtcNow,
                };
                _repository.Insert(library, 0, active: true);
                _logger.Info($"[Remote] Upgraded legacy binding to library '{library.Name}'", "RemoteLibraryStore");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] Legacy binding upgrade failed: {ex.Message}", "RemoteLibraryStore");
        }
        TryDelete(LegacyBindingPath);
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
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
