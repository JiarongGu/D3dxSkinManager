using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Launch.Models;

namespace D3dxSkinManager.Modules.Launch.Services;

/// <summary>
/// Detects an XXMI Launcher install and enumerates its model importers. Read-only: it parses
/// "XXMI Launcher Config.json" to resolve each enabled importer's folder + Mods path so the UI can
/// bind a profile's work directory to an importer's Mods folder (our deploy target). See
/// .claude/rules/xxmi-integration.md.
/// </summary>
public interface IXxmiService
{
    /// <summary>
    /// Probe a folder for an XXMI Launcher install and return its importers with resolved paths.
    /// Throws XXMI_CONFIG_NOT_FOUND if the folder is not an XXMI Launcher install.
    /// </summary>
    Task<XxmiDetectResult> DetectAsync(string folderPath);
}

/// <summary>
/// Implementation of <see cref="IXxmiService"/>. No state, no events — pure filesystem read + parse.
/// </summary>
public class XxmiService : IXxmiService
{
    private const string ConfigFileName = "XXMI Launcher Config.json";
    private static readonly string[] LauncherExeRelPaths =
    {
        Path.Combine("Resources", "Bin", "XXMI Launcher.exe"),
    };

    private readonly ILogHelper _logger;

    public XxmiService(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
