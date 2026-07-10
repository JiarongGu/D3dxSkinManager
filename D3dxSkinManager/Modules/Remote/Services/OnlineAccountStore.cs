using System.Text.Json;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// GLOBAL store for online-storage logins (a download host's session cookie, keyed by provider —
/// e.g. "quark"). Not per-profile: a host recurs across sites/profiles. The cookie is captured by
/// the in-app login window (never typed) and used by the matching resolver to authenticate. Stored
/// in {data}/settings/online-accounts.json. Kept out of profile config since it's a credential
/// shared app-wide.
///
/// The cookie is PROTECTED AT REST (2026-07-10): the file holds only the DPAPI-encrypted
/// `cookieProtected` blob (CurrentUser scope — see SecretProtector). On load, a blob that fails to
/// decrypt (file copied from another machine/user, or tampered = "doesn't match") is INVALIDATED:
/// the account is treated as logged out and the cleaned file is rewritten. Legacy plaintext
/// `cookie` files are upgraded to the protected format the first time they're read.
/// </summary>
public interface IOnlineAccountStore
{
    /// <summary>The saved account for a provider, or null. Includes the raw cookie (backend only).</summary>
    OnlineStorageAccount? Get(string provider);

    /// <summary>Save/replace a provider's account (cookie + display name).</summary>
    void Save(OnlineStorageAccount account);

    /// <summary>Remove a provider's account (log out).</summary>
    void Remove(string provider);

    /// <summary>Cookie-free view of every saved account, for the management UI.</summary>
    IReadOnlyList<OnlineStorageAccountInfo> List();
}

public class OnlineAccountStore : IOnlineAccountStore
{
    private readonly string _path;
    private readonly ILogHelper _logger;
    private readonly object _lock = new();
    private Dictionary<string, OnlineStorageAccount>? _cache;

    public OnlineAccountStore(IGlobalPathService globalPaths, ILogHelper logger)
    {
        _path = Path.Combine(globalPaths.GlobalSettingsDirectory, "online-accounts.json");
        _logger = logger;
    }

    public OnlineStorageAccount? Get(string provider)
    {
        lock (_lock)
        {
            return Load().TryGetValue(Key(provider), out var acc) ? acc : null;
        }
    }

    public void Save(OnlineStorageAccount account)
    {
        lock (_lock)
        {
            var map = Load();
            account.Provider = Key(account.Provider);
            account.SavedAtUtc = DateTime.UtcNow;
            map[account.Provider] = account;
            Persist(map);
        }
    }

    public void Remove(string provider)
    {
        lock (_lock)
        {
            var map = Load();
            if (map.Remove(Key(provider))) Persist(map);
        }
    }

    public IReadOnlyList<OnlineStorageAccountInfo> List()
    {
        lock (_lock)
        {
            return Load().Values.Select(a => new OnlineStorageAccountInfo
            {
                Provider = a.Provider,
                DisplayName = a.DisplayName,
                LoggedIn = !string.IsNullOrEmpty(a.Cookie),
                SavedAtUtc = a.SavedAtUtc,
            }).ToList();
        }
    }

    private static string Key(string provider) => (provider ?? string.Empty).Trim().ToLowerInvariant();

    private Dictionary<string, OnlineStorageAccount> Load()
    {
        if (_cache != null) return _cache;
        var rewrite = false;
        try
        {
            if (File.Exists(_path))
            {
                var list = JsonSerializer.Deserialize<List<OnlineStorageAccount>>(File.ReadAllText(_path))
                           ?? new List<OnlineStorageAccount>();
                _cache = list.Where(a => !string.IsNullOrWhiteSpace(a.Provider))
                    .ToDictionary(a => Key(a.Provider), a => a);

                foreach (var account in _cache.Values)
                {
                    if (!string.IsNullOrEmpty(account.CookieProtected))
                    {
                        try
                        {
                            account.Cookie = SecretProtector.Unprotect(account.CookieProtected);
                        }
                        catch (Exception ex)
                        {
                            // The blob doesn't match this Windows user/machine (copied file, other
                            // account, tampered) — INVALIDATE: logged out + strip the dead blob.
                            _logger.Warn($"[OnlineAccounts] '{account.Provider}' token failed its protection check — invalidated, re-login required ({ex.GetType().Name})", "OnlineAccountStore");
                            account.Cookie = string.Empty;
                            account.CookieProtected = null;
                            rewrite = true;
                        }
                    }
                    else if (!string.IsNullOrEmpty(account.Cookie))
                    {
                        // Legacy PLAINTEXT cookie — upgrade the file to the protected format now.
                        rewrite = true;
                    }
                }
            }
            else
            {
                _cache = new();
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"[OnlineAccounts] Failed to load {_path}: {ex.Message}", "OnlineAccountStore");
            _cache = new();
        }
        if (rewrite) Persist(_cache!);
        return _cache!;
    }

    private void Persist(Dictionary<string, OnlineStorageAccount> map)
    {
        _cache = map;
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // NEVER write the plaintext cookie — serialize clones that carry only the DPAPI blob.
            var persisted = map.Values.Select(a => new OnlineStorageAccount
            {
                Provider = a.Provider,
                DisplayName = a.DisplayName,
                Cookie = string.Empty,
                CookieProtected = string.IsNullOrEmpty(a.Cookie) ? null : SecretProtector.Protect(a.Cookie),
                SavedAtUtc = a.SavedAtUtc,
            }).ToList();
            File.WriteAllText(_path, JsonSerializer.Serialize(persisted,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.Error($"[OnlineAccounts] Failed to save {_path}: {ex.Message}", "OnlineAccountStore", ex);
        }
    }
}
