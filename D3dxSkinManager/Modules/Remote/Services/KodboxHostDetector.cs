using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Detects whether an unmatched download host is a kodbox (可道云 / KodExplorer) instance, so a site that
/// moves its Hui盘 to a new mirror (whose URL shape a static resolver rule doesn't catch) still resolves.
/// A site may serve several download methods, so this is the auto-detect FALLBACK behind the config match
/// list (opt-in via <see cref="Models.RemoteSourceConfig.AutoDetect"/>).
///
/// Cheap + safe: a pre-filter probes ONLY share-shaped URLs (`/s/` or `/#s/`) — never ad/social links — and
/// the host root is fetched ONCE per origin (cached) and checked for the kodbox fingerprint
/// (`Powered by kodbox` / `generator content="kodbox"`, grounded on the real mirror 2026-07-14).
/// </summary>
public interface IKodboxHostDetector
{
    /// <summary>True when <paramref name="url"/>'s host is a kodbox instance (→ resolve as type "kodbox").
    /// Returns false without any network for URLs that don't look like a file share.</summary>
    Task<bool> IsKodboxAsync(string url, CancellationToken ct = default);
}

public class KodboxHostDetector : IKodboxHostDetector
{
    // kodbox's SPA share route (`#s/<key>`) or a server path form (`/s/<key>`). Ad/social links
    // (`/#/register`, `/zh/auth/signup`, t.me/…) don't match, so they're never probed.
    private static readonly Regex ShareShaped = new(@"/#?s/[^/?#]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    // kodbox stamps every page: <title>… Powered by kodbox</title> + <meta name="generator" content="kodbox …">.
    private static readonly Regex Fingerprint = new("Powered by kodbox|content=\"kodbox",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IRemotePageFetcher _fetcher;
    private readonly ILogHelper _logger;
    // origin → is-kodbox. One probe per host; idempotent, so last-writer-wins is fine.
    private readonly ConcurrentDictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

    public KodboxHostDetector(IRemotePageFetcher fetcher, ILogHelper logger)
    {
        _fetcher = fetcher;
        _logger = logger;
    }

    public async Task<bool> IsKodboxAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url) || !ShareShaped.IsMatch(url)) return false; // pre-filter: no probe
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var origin = $"{uri.Scheme}://{uri.Authority}";

        if (_cache.TryGetValue(origin, out var cached)) return cached;

        bool result;
        try
        {
            var html = await _fetcher.GetStringAsync($"{origin}/", ct).ConfigureAwait(false);
            result = Fingerprint.IsMatch(html);
        }
        catch (Exception ex)
        {
            // Unreachable / non-HTML / cancelled → treat as not-kodbox; don't fail the detail load.
            _logger.Verbose($"[Remote] kodbox probe failed for {origin}: {ex.Message}", nameof(KodboxHostDetector));
            result = false;
        }
        _cache[origin] = result;
        return result;
    }
}
