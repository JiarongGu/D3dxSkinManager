using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// The PER-PROFILE remote binding: which source + game list this profile targets
/// ({profile}/remote-binding.json). Site adapters and index caches stay GLOBAL (a site serves many
/// games; profiles targeting the same game share one synced index) — the binding is the only
/// per-profile piece, because a profile IS one game.
/// </summary>
public interface IRemoteBindingStore
{
    RemoteBinding? Get();
    RemoteBinding Set(string sourceId, string listId);
    /// <summary>Update just the default import category on the current binding (no re-bind). No-op if unbound.</summary>
    RemoteBinding? SetDefaultCategory(string? categoryId);
    void Clear();
}

public class RemoteBindingStore : IRemoteBindingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;

    public RemoteBindingStore(IProfilePathService profilePaths, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _logger = logger;
    }

    private string FilePath => Path.Combine(_profilePaths.ProfilePath, "remote-binding.json");

    public RemoteBinding? Get()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var binding = JsonSerializer.Deserialize<RemoteBinding>(File.ReadAllText(FilePath), JsonOptions);
            return string.IsNullOrWhiteSpace(binding?.SourceId) ? null : binding;
        }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] Corrupt remote-binding.json: {ex.Message}", "RemoteBindingStore");
            return null;
        }
    }

    public RemoteBinding Set(string sourceId, string listId)
    {
        // Re-binding to the SAME source+list preserves the chosen default category; a switch resets it.
        var existing = Get();
        var keepCategory = existing != null
            && string.Equals(existing.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.ListId, listId, StringComparison.OrdinalIgnoreCase)
            ? existing.DefaultCategoryId : null;
        var binding = new RemoteBinding
        {
            SourceId = sourceId,
            ListId = listId,
            BoundAtUtc = DateTime.UtcNow,
            DefaultCategoryId = keepCategory,
        };
        File.WriteAllText(FilePath, JsonSerializer.Serialize(binding, JsonOptions));
        return binding;
    }

    public RemoteBinding? SetDefaultCategory(string? categoryId)
    {
        var binding = Get();
        if (binding == null) return null;
        binding.DefaultCategoryId = string.IsNullOrWhiteSpace(categoryId) ? null : categoryId;
        File.WriteAllText(FilePath, JsonSerializer.Serialize(binding, JsonOptions));
        return binding;
    }

    public void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (Exception ex) { _logger.Warn($"[Remote] Failed to clear binding: {ex.Message}", "RemoteBindingStore"); }
    }
}
