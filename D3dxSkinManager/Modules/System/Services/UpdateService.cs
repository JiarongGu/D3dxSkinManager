using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
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

    // GitHub API wants this Accept header; the User-Agent is set by the DownloadService.
    private static readonly IReadOnlyDictionary<string, string> GitHubHeaders =
        new Dictionary<string, string> { { "Accept", "application/vnd.github+json" } };

    private readonly ILogHelper _logger;
    private readonly IProcessRegistry _processRegistry;
    private readonly IAppEnvironment _appEnvironment;
    private readonly IDownloadService _downloadService;

    public UpdateService(ILogHelper logger, IProcessRegistry processRegistry, IAppEnvironment appEnvironment,
        IDownloadService downloadService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processRegistry = processRegistry ?? throw new ArgumentNullException(nameof(processRegistry));
        _appEnvironment = appEnvironment ?? throw new ArgumentNullException(nameof(appEnvironment));
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
    }

    // The install dir (where the exe + manifest.json live; the dir the launcher manages).
    private string InstallDir => _appEnvironment.BaseDirectory;
    // Update staging lives next to the install (same dir the launcher reads). The launcher applies
    // {StagingRoot}/staged over the install and clears StagingRoot on the next startup.
    private string StagingRoot => Path.Combine(InstallDir, ".update");
    private string StagedDir => Path.Combine(StagingRoot, "staged");
    private string ReadyMarkerPath => Path.Combine(StagingRoot, "ready.json");

    public async Task<UpdateInfo> CheckForUpdateAsync()
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            _logger.Info($"Checking for update (current {currentVersion})...", "UpdateService");

            var json = await _downloadService.GetStringAsync(LatestReleaseApiUrl, GitHubHeaders).ConfigureAwait(false);
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
            // Reuse the shared DownloadService; map its 0–100% to the registry's 0–90% (extract/verify
            // take the last 10%). The zip itself has no published hash — staged files are verified post-extract.
            var progress = new Progress<DownloadProgress>(p =>
            {
                if (p.Percent.HasValue) _processRegistry.Report(procId, p.Percent.Value * 90 / 100);
            });
            await _downloadService.DownloadAsync(
                new DownloadRequest { Url = zipUrl, DestinationPath = zipPath }, progress).ConfigureAwait(false);

            _processRegistry.Report(procId, 92, "Extracting");
            if (Directory.Exists(StagedDir)) Directory.Delete(StagedDir, recursive: true);
            ZipFile.ExtractToDirectory(zipPath, StagedDir, overwriteFiles: true);
            File.Delete(zipPath);

            // Verify every staged file against the staged manifest's sha256 BEFORE marking ready, so the
            // launcher never applies a corrupt/partial download. A mismatch aborts the stage.
            _processRegistry.Report(procId, 97, "Verifying");
            var problems = await VerifyStagedFilesAsync(StagedDir).ConfigureAwait(false);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Update verification failed ({problems.Count} file(s)): {string.Join(", ", problems.Take(3))}");
            }

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

    /// <summary>
    /// Verify every file the staged manifest lists against its sha256. Returns a list of problems
    /// (missing or hash-mismatched paths); empty = all good. Public for testing.
    /// A missing/unreadable staged manifest is itself a problem (fail safe — don't apply unverifiable).
    /// </summary>
    public async Task<List<string>> VerifyStagedFilesAsync(string stagedDir)
    {
        var problems = new List<string>();
        var manifestPath = Path.Combine(stagedDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            problems.Add("manifest.json (missing)");
            return problems;
        }

        UpdateManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<UpdateManifest>(
                await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            problems.Add($"manifest.json (unreadable: {ex.Message})");
            return problems;
        }
        if (manifest == null) { problems.Add("manifest.json (empty)"); return problems; }

        foreach (var file in manifest.Files)
        {
            var rel = file.Path.Replace('/', Path.DirectorySeparatorChar);
            var full = Path.Combine(stagedDir, rel);
            if (!File.Exists(full))
            {
                problems.Add($"{file.Path} (missing)");
                continue;
            }
            if (string.IsNullOrEmpty(file.Sha256)) continue; // nothing to check against

            var actual = await ComputeSha256Async(full).ConfigureAwait(false);
            if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{file.Path} (hash mismatch)");
            }
        }
        return problems;
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
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

            var localPath = Path.Combine(InstallDir, "manifest.json");
            if (!File.Exists(localPath)) return;

            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var remoteJson = await _downloadService.GetStringAsync(manifestUrl).ConfigureAwait(false);
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

    /// <summary>Running app version as "Major.Minor" (matches the csproj &lt;Version&gt;). Virtual so tests
    /// can pin a deterministic current version for the comparison.</summary>
    protected virtual string GetCurrentVersion()
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
