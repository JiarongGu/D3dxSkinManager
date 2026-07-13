using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Launch.Models;

namespace D3dxSkinManager.Modules.Launch.Services;

/// <summary>
/// Detects an XXMI Launcher install and enumerates its model importers. Read-only: it parses
/// "XXMI Launcher Config.json" to resolve each enabled importer's folder + Mods path so the UI can
/// bind a profile's work directory to an importer's Mods folder (our deploy target). Also offers
/// the "get XXMI" assist: look up the latest launcher installer on GitHub and download+open it.
/// See .claude/knowledge/xxmi-integration.md.
/// </summary>
public interface IXxmiService
{
    /// <summary>
    /// Probe a folder for an XXMI Launcher install and return its importers with resolved paths.
    /// Throws XXMI_CONFIG_NOT_FOUND if the folder is not an XXMI Launcher install.
    /// </summary>
    Task<XxmiDetectResult> DetectAsync(string folderPath);

    /// <summary>
    /// Latest XXMI-Launcher release's Windows installer (.msi) from the GitHub API.
    /// Throws XXMI_INSTALLER_LOOKUP_FAILED when the API/parse fails or no installer asset exists.
    /// </summary>
    Task<XxmiInstallerInfo> GetLatestInstallerAsync(CancellationToken ct = default);

    /// <summary>
    /// Fire-and-forget: download the installer into the managed downloads area (progress via a
    /// cancellable Download process in the Activity panel) and OPEN it when done — the user
    /// completes XXMI's own installer, then binds the install in the picker. Returns the process id.
    /// </summary>
    string StartInstallerDownload(XxmiInstallerInfo info);
}

/// <summary>
/// Implementation of <see cref="IXxmiService"/>. No profile state, no events — filesystem read +
/// parse, plus the stateless installer-download assist.
/// </summary>
public class XxmiService : IXxmiService
{
    private const string ConfigFileName = "XXMI Launcher Config.json";

    // GitHub release facts verified 2026-07-10: assets are XXMI-Launcher-Installer-Online-v*.msi
    // (the native Windows installer) + XXMI-Launcher-Portable-v*.zip. We offer the .msi.
    private const string ReleaseApiUrl = "https://api.github.com/repos/SpectrumQT/XXMI-Launcher/releases/latest";
    public const string ReleaseDownloadPrefix = "https://github.com/SpectrumQT/XXMI-Launcher/releases/download/";

    private static readonly string[] LauncherExeRelPaths =
    {
        Path.Combine("Resources", "Bin", "XXMI Launcher.exe"),
    };

    private readonly ILogHelper _logger;
    private readonly IDownloadService _download;
    private readonly IProcessRegistry _processRegistry;

    public XxmiService(ILogHelper logger, IDownloadService download, IProcessRegistry processRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _download = download ?? throw new ArgumentNullException(nameof(download));
        _processRegistry = processRegistry ?? throw new ArgumentNullException(nameof(processRegistry));
    }

    public Task<XxmiDetectResult> DetectAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new OperationException("XXMI_CONFIG_NOT_FOUND", "folder", folderPath ?? string.Empty);
        }

        // Accept either the install root or the launcher exe / a .lnk dropped in — normalize to root.
        var root = ResolveInstallRoot(folderPath);
        var configPath = Path.Combine(root, ConfigFileName);

        if (!File.Exists(configPath))
        {
            _logger.Warn($"[Xxmi] No '{ConfigFileName}' in '{root}'");
            throw new OperationException("XXMI_CONFIG_NOT_FOUND", "folder", root);
        }

        var result = new XxmiDetectResult
        {
            Found = true,
            ConfigPath = configPath,
            LauncherExe = LauncherExeRelPaths
                .Select(rel => Path.Combine(root, rel))
                .FirstOrDefault(File.Exists),
        };

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var rootEl = doc.RootElement;

            var activeImporter = TryGetString(rootEl, "Launcher", "active_importer");
            var enabled = GetEnabledImporters(rootEl);

            // Config game_folder metadata, keyed by importer name (only used to enrich what's on disk).
            var gameFolders = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (rootEl.TryGetProperty("Importers", out var importersEl) &&
                importersEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in importersEl.EnumerateObject())
                {
                    gameFolders[prop.Name] = TryGetString(prop.Value, "Importer", "game_folder");
                }
            }

            // Discover importers by what ACTUALLY exists in the root — any top-level subfolder that looks
            // like a 3DMigoto/importer install (has a Mods folder or a d3dx.ini). NOT a fixed game list:
            // this picks up ZZMI/EFMI and any future/custom importer the user has. Config only enriches.
            foreach (var dir in Directory.GetDirectories(root))
            {
                if (!LooksLikeImporter(dir)) continue;
                var name = Path.GetFileName(dir);
                gameFolders.TryGetValue(name, out var gameFolder);

                result.Importers.Add(new XxmiImporter
                {
                    Name = name,
                    ImporterDir = dir,
                    ModsDir = Path.Combine(dir, "Mods"),
                    GameFolder = string.IsNullOrWhiteSpace(gameFolder) ? null : gameFolder,
                    IsActive = string.Equals(name, activeImporter, StringComparison.OrdinalIgnoreCase),
                    IsInstalled = true,
                });
            }

            // Active first, then enabled, then by name.
            result.Importers = result.Importers
                .OrderByDescending(i => i.IsActive)
                .ThenByDescending(i => enabled.Contains(i.Name))
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.Info($"[Xxmi] Detected install at '{root}' with {result.Importers.Count} importer(s), active='{activeImporter}'");
        }
        catch (JsonException ex)
        {
            _logger.Error($"[Xxmi] Failed to parse '{configPath}': {ex.Message}");
            throw new OperationException("XXMI_CONFIG_NOT_FOUND", "folder", root);
        }

        return Task.FromResult(result);
    }

    public async Task<XxmiInstallerInfo> GetLatestInstallerAsync(CancellationToken ct = default)
    {
        string body;
        try
        {
            body = await _download.GetStringAsync(ReleaseApiUrl,
                new Dictionary<string, string> { ["Accept"] = "application/vnd.github+json" }, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warn($"[Xxmi] Installer lookup failed: {ex.Message}");
            throw new OperationException("XXMI_INSTALLER_LOOKUP_FAILED", "reason", ex.Message);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    // The native Windows installer asset (vs the portable zip).
                    if (!name.StartsWith("XXMI-Launcher-Installer", StringComparison.OrdinalIgnoreCase) ||
                        !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                        continue;

                    return new XxmiInstallerInfo
                    {
                        Version = tag,
                        FileName = name,
                        SizeBytes = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                        Url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "",
                    };
                }
            }
            throw new OperationException("XXMI_INSTALLER_LOOKUP_FAILED", "reason", "no installer asset in latest release");
        }
        catch (JsonException ex)
        {
            _logger.Warn($"[Xxmi] Installer lookup parse failed: {ex.Message}");
            throw new OperationException("XXMI_INSTALLER_LOOKUP_FAILED", "reason", ex.Message);
        }
    }

    public string StartInstallerDownload(XxmiInstallerInfo info)
    {
        // Only ever download+execute from the official release area — the URL round-trips through
        // the frontend, so re-validate here (never run an arbitrary path).
        if (info == null || string.IsNullOrWhiteSpace(info.Url) ||
            !info.Url.StartsWith(ReleaseDownloadPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new OperationException("XXMI_INSTALLER_LOOKUP_FAILED", "reason", "unexpected installer url");
        }

        // Fire-and-forget tracked op — Start + Complete/Cancel/Fail handled by RunTrackedAsync.
        return _processRegistry.RunTrackedAsync(ProcessType.Download,
            $"Downloading XXMI installer {info.Version}",
            async (procId, ct) =>
            {
                var progress = new Progress<DownloadProgress>(p => _processRegistry.Report(procId, p.Percent));
                var result = await _download.DownloadToManagedAsync(info.Url, info.FileName, progress, null, ct)
                    .ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();

                // Hand off to XXMI's own installer UI — installing is XXMI's job, not ours.
                _processRegistry.Report(procId, 100, "Opening installer", detailKey: "process.stage.openingInstaller");
                Process.Start(new ProcessStartInfo { FileName = result.FilePath, UseShellExecute = true });
                _logger.Info($"[Xxmi] Installer {info.FileName} downloaded + opened");
            },
            cancellable: true, titleKey: "process.xxmiDownload", titleArg: info.Version,
            onError: ex => _logger.Error($"[Xxmi] Installer download failed: {ex.Message}", "XxmiService", ex));
    }

    /// <summary>
    /// Normalize a user-picked path to the launcher install root. Accepts the root itself, the
    /// launcher exe, a .lnk, or Resources\Bin — walks up until it finds the config or hits a ceiling.
    /// </summary>
    private static string ResolveInstallRoot(string path)
    {
        // If a file was picked (exe/lnk), start from its directory.
        var start = File.Exists(path) ? Path.GetDirectoryName(path) ?? path : path;

        var dir = new DirectoryInfo(start);
        for (var i = 0; dir != null && i < 4; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, ConfigFileName)))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        // Fall back to the starting folder; caller validates the config exists.
        return start;
    }

    /// <summary>
    /// A top-level subfolder is an importer if it carries 3DMigoto/importer markers: a Mods folder
    /// (the deploy target) or a d3dx.ini / d3d11.dll. Keeps Resources/Backups/Locale/Themes out.
    /// </summary>
    private static bool LooksLikeImporter(string dir)
    {
        return Directory.Exists(Path.Combine(dir, "Mods"))
            || File.Exists(Path.Combine(dir, "d3dx.ini"))
            || File.Exists(Path.Combine(dir, "d3d11.dll"));
    }

    private static HashSet<string> GetEnabledImporters(JsonElement root)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("Launcher", out var launcher) &&
            launcher.TryGetProperty("enabled_importers", out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    set.Add(item.GetString()!);
                }
            }
        }
        return set;
    }

    private static string? TryGetString(JsonElement parent, string objKey, string key)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(objKey, out var obj) &&
            obj.ValueKind == JsonValueKind.Object &&
            obj.TryGetProperty(key, out var val) &&
            val.ValueKind == JsonValueKind.String)
        {
            return val.GetString();
        }
        return null;
    }
}
