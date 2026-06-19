using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.System.Models;

namespace D3dxSkinManager.Modules.System.Services;

/// <summary>
/// Checks the GitHub Releases API for a newer app version and opens the release page.
/// Read-only network call; emits no events. The "self-replace a running exe" step is deliberately
/// NOT done here — that is the dangerous part. Instead the UI offers a "Download" action that opens
/// the release page in the browser.
/// </summary>
public class UpdateService : IUpdateService
{
    // GitHub repo that publishes releases for this app.
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/JiarongGu/D3dxSkinManager/releases/latest";

    // Static HttpClient (intended to be long-lived / reused — avoids socket exhaustion).
    private static readonly HttpClient Http = CreateHttpClient();

    private readonly ILogHelper _logger;

    public UpdateService(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub rejects requests with no User-Agent (HTTP 403).
        client.DefaultRequestHeaders.UserAgent.ParseAdd("D3dxSkinManager-UpdateCheck");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public async Task<UpdateInfo> CheckForUpdateAsync()
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            _logger.Info($"Checking for update (current {currentVersion})...", "UpdateService");

            var json = await Http.GetStringAsync(LatestReleaseApiUrl).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = GetString(root, "tag_name");
            var latestVersion = NormalizeVersion(tag);

            var info = new UpdateInfo
            {
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                ReleaseName = GetString(root, "name"),
                ReleaseNotes = GetString(root, "body"),
                ReleaseUrl = GetString(root, "html_url"),
                PublishedAt = GetString(root, "published_at"),
                UpdateAvailable = IsNewer(latestVersion, currentVersion),
            };

            // Best-effort file-level changeset: diff the release manifest asset against the locally
            // installed manifest. Purely informational (for the update screen) — never fails the check.
            if (info.UpdateAvailable)
            {
                await TryAttachManifestDiffAsync(root, info).ConfigureAwait(false);
            }

            _logger.Info(
                $"Update check: latest {info.LatestVersion}, available={info.UpdateAvailable}, " +
                $"manifest={info.HasManifest} ({info.ChangedFileCount} files)",
                "UpdateService");

            return info;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Update check failed: {ex.Message}", "UpdateService");
            throw new OperationException(
                "UPDATE_CHECK_FAILED",
                "reason", ex.Message,
                $"Couldn't check for updates: {ex.Message}");
        }
    }

    public Task OpenUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
              url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            throw new OperationException(
                "UPDATE_CHECK_FAILED",
                "reason", "Invalid release URL",
                "Couldn't open the release page: invalid URL");
        }

        // UseShellExecute opens the URL in the default browser.
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Download the release's <c>manifest.json</c> asset and diff it against the locally-installed
    /// manifest (next to the running exe). Attaches the changed-file count + download size to
    /// <paramref name="info"/>. Best-effort: any failure (no asset, no local manifest, parse error)
    /// leaves HasManifest=false and is swallowed — the version-based check still stands.
    /// </summary>
    private async Task TryAttachManifestDiffAsync(JsonElement root, UpdateInfo info)
    {
        try
        {
            var manifestUrl = FindManifestAssetUrl(root);
            if (manifestUrl == null) return;

            var localPath = Path.Combine(AppContext.BaseDirectory, "manifest.json");
            if (!File.Exists(localPath)) return;

            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var remoteJson = await Http.GetStringAsync(manifestUrl).ConfigureAwait(false);
            var remote = JsonSerializer.Deserialize<UpdateManifest>(remoteJson, jsonOpts);
            var local = JsonSerializer.Deserialize<UpdateManifest>(
                await File.ReadAllTextAsync(localPath).ConfigureAwait(false), jsonOpts);

            if (remote == null || local == null) return;

            var diff = ManifestDiff.Compute(local, remote);
            info.HasManifest = true;
            info.ChangedFileCount = diff.ChangedFileCount;
            info.DownloadSize = diff.DownloadSize;
        }
        catch (Exception ex)
        {
            _logger.Verbose($"Manifest diff skipped: {ex.Message}", "UpdateService");
        }
    }

    /// <summary>Find the browser_download_url of the release's "manifest.json" asset, if present.</summary>
    private static string? FindManifestAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (string.Equals(GetString(asset, "name"), "manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                var url = GetString(asset, "browser_download_url");
                return string.IsNullOrEmpty(url) ? null : url;
            }
        }
        return null;
    }

    /// <summary>Running app version as "Major.Minor" (matches the csproj &lt;Version&gt;).</summary>
    private static string GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        if (v == null) return "0.0";
        // Build/Revision are usually 0 — present the meaningful Major.Minor.
        return v.Build > 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
    }

    /// <summary>Strip a leading 'v'/'V' from a release tag (e.g. "v2.5" → "2.5").</summary>
    private static string NormalizeVersion(string tag)
    {
        tag = tag.Trim();
        if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            tag = tag[1..];
        }
        return tag;
    }

    /// <summary>
    /// True when <paramref name="latest"/> is strictly newer than <paramref name="current"/>.
    /// Numeric (System.Version) comparison when both parse; falls back to a string mismatch otherwise.
    /// </summary>
    private static bool IsNewer(string latest, string current)
    {
        if (string.IsNullOrWhiteSpace(latest)) return false;

        if (Version.TryParse(Pad(latest), out var l) && Version.TryParse(Pad(current), out var c))
        {
            return l > c;
        }

        // Couldn't parse as versions — treat any non-equal, non-empty tag as an update.
        return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
    }

    // System.Version needs at least Major.Minor; pad a bare "3" to "3.0".
    private static string Pad(string v) => v.Contains('.') ? v : v + ".0";

    private static string GetString(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? string.Empty
            : string.Empty;
    }
}
