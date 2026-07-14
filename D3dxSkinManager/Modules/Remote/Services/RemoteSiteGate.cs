using System.Collections.Concurrent;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Unlocks a site behind a password GATE (<see cref="RemoteSourceConfig.Gate"/>) so its pages/REST API
/// stop 401-ing. Logs in ONCE per source per app session by POSTing the shared gate password; the
/// session cookie the site sets is kept by <see cref="IDownloadService"/>'s domain-scoped cookie
/// container and auto-replayed on every later request (engines fetch normally — the cookie rides along).
/// On a 401 an engine calls <see cref="InvalidateAsync"/> then retries so an expired cookie re-logs-in.
/// Only the "wordpress-password-protected" gate type exists today (kekehxl.top — see remote-library.md).
/// </summary>
public interface IRemoteSiteGate
{
    /// <summary>No-op when the source has no gate or is already unlocked this session; otherwise logs in.
    /// Serialized per source so concurrent browses don't double-login.</summary>
    Task EnsureAuthenticatedAsync(RemoteSourceConfig config, CancellationToken ct);

    /// <summary>Forget the source's unlocked state (call after a 401) so the next
    /// <see cref="EnsureAuthenticatedAsync"/> logs in again.</summary>
    void Invalidate(string sourceId);
}

/// <inheritdoc cref="IRemoteSiteGate"/>
public class RemoteSiteGate : IRemoteSiteGate
{
    public const string WordPressPasswordProtected = "wordpress-password-protected";

    private static readonly IReadOnlyDictionary<string, string> BrowserHeaders = new Dictionary<string, string>
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36",
        ["Accept-Language"] = "zh-CN,zh;q=0.9,en;q=0.8",
    };

    private readonly IDownloadService _download;
    private readonly ILogHelper _logger;
    private readonly ConcurrentDictionary<string, bool> _unlocked = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public RemoteSiteGate(IDownloadService download, ILogHelper logger)
    {
        _download = download;
        _logger = logger;
    }

    public void Invalidate(string sourceId) => _unlocked.TryRemove(sourceId, out _);

    public async Task EnsureAuthenticatedAsync(RemoteSourceConfig config, CancellationToken ct)
    {
        var gate = config.Gate;
        if (gate == null || string.IsNullOrWhiteSpace(gate.Type)) return;
        if (_unlocked.GetValueOrDefault(config.Id)) return;

        var gateLock = _locks.GetOrAdd(config.Id, _ => new SemaphoreSlim(1, 1));
        await gateLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_unlocked.GetValueOrDefault(config.Id)) return; // won the race while waiting
            await LoginAsync(config, gate, ct).ConfigureAwait(false);
            _unlocked[config.Id] = true;
        }
        finally
        {
            gateLock.Release();
        }
    }

    private async Task LoginAsync(RemoteSourceConfig config, RemoteGateConfig gate, CancellationToken ct)
    {
        if (!string.Equals(gate.Type, WordPressPasswordProtected, StringComparison.OrdinalIgnoreCase))
            throw new OperationException("REMOTE_GATE_FAILED", "name", config.Name);

        var baseUrl = config.BaseUrl.TrimEnd('/');
        var loginPath = string.IsNullOrWhiteSpace(gate.LoginPath) ? "/" : gate.LoginPath;
        var redirectTo = baseUrl + loginPath;
        var loginUrl = $"{baseUrl}{loginPath}?password-protected=login&redirect_to={Uri.EscapeDataString(redirectTo)}";
        var pwField = string.IsNullOrWhiteSpace(gate.PasswordField) ? "password_protected_pwd" : gate.PasswordField;

        try
        {
            // GET the login page first so the plugin's cookie-test cookie is seeded (it rejects a POST
            // that arrives with no prior cookie), then POST the password.
            await _download.GetStringAsync(loginUrl, BrowserHeaders, ct).ConfigureAwait(false);

            var form = new Dictionary<string, string>
            {
                [pwField] = gate.Password,
                ["password-protected"] = "login",
                ["redirect_to"] = redirectTo,
                ["wp-submit"] = "Submit",
                ["password_protected_cookie_test"] = "1",
            };
            var body = await _download.PostFormAsync(loginUrl, form, BrowserHeaders, ct).ConfigureAwait(false);

            // On success the POST 302s to the (now-unlocked) site; on a wrong password the login page is
            // re-served — its form markers tell us the gate is still up.
            if (IsGatePage(body))
            {
                _logger.Warn($"[Remote] gate login rejected for {config.Id} (wrong password?)", nameof(RemoteSiteGate));
                throw new OperationException("REMOTE_GATE_FAILED", "name", config.Name);
            }
            _logger.Info($"[Remote] unlocked gated site {config.Id}", nameof(RemoteSiteGate));
        }
        catch (OperationException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] gate login failed for {config.Id}: {ex.Message}", nameof(RemoteSiteGate));
            throw new OperationException("REMOTE_GATE_FAILED", "name", config.Name);
        }
    }

    /// <summary>The body is still the password-gate login page (login didn't take).</summary>
    public static bool IsGatePage(string? body) =>
        !string.IsNullOrEmpty(body) &&
        (body.Contains("password-protected=login", StringComparison.OrdinalIgnoreCase) ||
         body.Contains("password_protected_pwd", StringComparison.OrdinalIgnoreCase));
}
