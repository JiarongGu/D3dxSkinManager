using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.System.Models;

namespace D3dxSkinManager.Modules.System.Services;

/// <summary>
/// App self-update: checks GitHub releases, and (on user request) downloads + stages the update
/// package next to the install. The actual file swap is applied by the C++ launcher on the next
/// startup (a running exe can't replace itself) — this service only stages it. Two-phase flow:
///   1. CheckForUpdateAsync — version + changeset (for the update screen).
///   2. DownloadUpdateAsync — download the release zip, extract to {install}/.update/staged, write
///      {install}/.update/ready.json. The launcher applies it next launch (see updater.cpp).
/// GetUpdateStateAsync reports whether a downloaded update is waiting to be applied.
/// </summary>
public class UpdateService : IUpdateService
{
    // GitHub repo that publishes releases for this app.
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/JiarongGu/D3dxSkinManager/releases/latest";

    // Stable "latest release asset" redirect (no API call needed for the zip download).
    private const string LatestDownloadBase =
        "https://github.com/JiarongGu/D3dxSkinManager/releases/latest/download/";

    // Static HttpClient (intended to be long-lived / reused — avoids socket exhaustion).
    private static readonly HttpClient Http = CreateHttpClient();

    private readonly ILogHelper _logger;
    private readonly IProcessRegistry _processRegistry;

    public UpdateService(ILogHelper logger, IProcessRegistry processRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processRegistry = processRegistry ?? throw new ArgumentNullException(nameof(processRegistry));
    }

    // Update staging lives next to the install (same dir the launcher reads). The launcher applies
    // {StagingRoot}/staged over the install and clears StagingRoot on the next startup.
    private static string StagingRoot => Path.Combine(AppContext.BaseDirectory, ".update");
    private static string StagedDir => Path.Combine(StagingRoot, "staged");
    private static string ReadyMarkerPath => Path.Combine(StagingRoot, "ready.json");

    private static HttpClient CreateHttpClient()
    {
        // No total timeout: a download can take longer than the default 100s; per-read is handled by
        // the stream copy. (The check call is small and fast regardless.)
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
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
    /// Download the latest release zip and stage it under {install}/.update/staged, then write
    /// ready.json. The launcher applies it on the next startup. Long-running — the caller (facade)
    /// kicks this off fire-and-forget; progress flows through the ProcessRegistry (Activity panel).
    /// </summary>
    public async Task DownloadUpdateAsync()
    {
        var procId = _processRegistry.Start(ProcessType.Download, "Downloading update");
        try
        {
            var info = await CheckForUpdateAsync().ConfigureAwait(false);
            if (!info.UpdateAvailable || string.IsNullOrWhiteSpace(info.LatestVersion))
            {
                _processRegistry.Complete(procId); // nothing to download
                return;
            }

            // Fresh staging dir.
            if (Directory.Exists(StagingRoot)) Directory.Delete(StagingRoot, recursive: true);
            Directory.CreateDirectory(StagingRoot);

            var zipName = $"D3dxSkinManager-v{info.LatestVersion}-win-x64.zip";
            var zipUrl = LatestDownloadBase + zipName;
            var zipPath = Path.Combine(StagingRoot, "update.zip");

            _logger.Info($"Downloading update {info.LatestVersion} from {zipUrl}", "UpdateService");
            await DownloadToFileAsync(zipUrl, zipPath, procId).ConfigureAwait(false);

            _processRegistry.Report(procId, 92, "Extracting");
            if (Directory.Exists(StagedDir)) Directory.Delete(StagedDir, recursive: true);
            ZipFile.ExtractToDirectory(zipPath, StagedDir, overwriteFiles: true);
            File.Delete(zipPath);

            // Mark ready — the launcher reads this on next startup.
            await File.WriteAllTextAsync(
                ReadyMarkerPath,
                JsonSerializer.Serialize(new { version = info.LatestVersion })).ConfigureAwait(false);

            _processRegistry.Report(procId, 100, "Ready to install on restart");
            _processRegistry.Complete(procId);
            _logger.Info($"Update {info.LatestVersion} staged; will apply on next launch.", "UpdateService");
        }
        catch (Exception ex)
        {
            _logger.Warn($"Update download failed: {ex.Message}", "UpdateService");
            _processRegistry.Fail(procId, ex.Message);
            try { if (Directory.Exists(StagingRoot)) Directory.Delete(StagingRoot, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>Whether a downloaded update is staged and waiting to be applied on the next startup.</summary>
    public async Task<UpdateState> GetUpdateStateAsync()
    {
        if (!File.Exists(ReadyMarkerPath))
        {
            return new UpdateState { Pending = false };
        }

        try
        {
            var json = await File.ReadAllTextAsync(ReadyMarkerPath).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var version = GetString(doc.RootElement, "version");
            return new UpdateState { Pending = true, PendingVersion = version };
        }
        catch
        {
            return new UpdateState { Pending = true }; // marker exists but unreadable — still pending
        }
    }

    /// <summary>Stream a URL to a file, reporting 0–90% download progress on the process.</summary>
    private async Task DownloadToFileAsync(string url, string destPath, string procId)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? -1L;
        await using var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var dst = File.Create(destPath);

        var buffer = new byte[81920];
        long readTotal = 0;
        int n;
        while ((n = await src.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n)).ConfigureAwait(false);
            readTotal += n;
            if (total > 0)
            {
                _processRegistry.Report(procId, (int)(readTotal * 90 / total));
            }
        }
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
