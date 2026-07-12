using System.Text;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Reads/merges 3DMigoto's persistent-variable store, <c>d3dx_user.ini</c>, which lives in the importer
/// dir (= the profile's <see cref="IProfilePathService.WorkDirectory"/> in xxmi/external mode — the folder
/// that CONTAINS the deploy <c>Mods/</c> subfolder). 3DMigoto auto-saves every <c>global persist $var</c> to
/// this file on exit / F10 and RESTORES it on next load, so a mod loads carrying that runtime state WITHOUT
/// editing the mod's own <c>.ini</c> default. This is how presets snapshot + restore per-mod var state.
///
/// Real format (verified against a live ZZMI install):
/// <code>
/// ; AUTOMATICALLY GENERATED FILE - DO NOT EDIT
/// [Constants]
/// $\zzmiv1\first_run = 0
/// $\mods\&lt;modId&gt;\&lt;folder&gt;\&lt;file&gt;.ini\swapkey3 = 1
/// </code>
/// The var's namespace embeds the deployed path <c>mods\&lt;modId&gt;\…</c>, so a line is attributable to a
/// mod by its <c>\&lt;modId&gt;\</c> segment. Merge is keyed by the full left-hand side (the whole
/// <c>$\…\var</c>), so we copy 3DMigoto's exact line verbatim and never reconstruct the namespace.
/// </summary>
public interface ID3dmigotoUserConfigService
{
    /// <summary>The persisted <c>[Constants]</c> assignment lines whose namespace belongs to one of
    /// <paramref name="modIds"/> (case-insensitive <c>\{modId}\</c> match). Empty when there's no
    /// d3dx_user.ini yet (e.g. an internal-work-dir profile, or the game never persisted anything).</summary>
    IReadOnlyList<string> CaptureVarLines(IReadOnlyCollection<string> modIds);

    /// <summary>Merge <paramref name="capturedLines"/> back into d3dx_user.ini's <c>[Constants]</c> —
    /// replace any existing line with the same LHS, append the rest, and preserve the header + every other
    /// var (other mods, the importer's own). Creates the file if absent. No-op (returns false) when there
    /// are no lines or no work dir. Not called while the game is running (presets apply pre-launch).</summary>
    bool ApplyVarLines(IReadOnlyList<string> capturedLines);
}

public class D3dmigotoUserConfigService : ID3dmigotoUserConfigService
{
    private const string FileName = "d3dx_user.ini";
    private const string ConstantsSection = "[Constants]";
    private static readonly string[] Header =
    {
        "; AUTOMATICALLY GENERATED FILE - DO NOT EDIT",
        "; Written by D3dxSkinManager (preset var-state restore). 3DMigoto overwrites this on exit/F10.",
    };

    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;

    public D3dmigotoUserConfigService(IProfilePathService profilePaths, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _logger = logger;
    }

    /// <summary>Locate d3dx_user.ini — it sits NEXT TO the 3DMigoto DLL / d3dx.ini (user note 2026-07-13),
    /// NOT inside the deploy Mods/ folder. In xxmi/external mode the work dir IS the importer dir (has
    /// d3dx.ini + Mods/) — verified. If the work dir was instead pointed AT the Mods folder, the 3DMigoto
    /// root is its parent. Prefer whichever dir actually holds d3dx.ini; fall back to the work dir.</summary>
    private string? ResolveUserIniPath()
    {
        var wd = _profilePaths.WorkDirectory;
        if (string.IsNullOrEmpty(wd)) return null;
        var candidates = new[] { wd, Path.GetDirectoryName(wd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) };
        foreach (var dir in candidates)
            if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "d3dx.ini")))
                return Path.Combine(dir, FileName);
        return Path.Combine(wd, FileName);
    }

    /// <summary>The LHS (everything before the first '=') of an assignment line, trimmed. A var value is a
    /// bare number so it never contains '='; returns null for non-assignment lines.</summary>
    private static string? Lhs(string line)
    {
        var eq = line.IndexOf('=');
        return eq <= 0 ? null : line[..eq].Trim();
    }

    private static bool IsSectionHeader(string trimmed) => trimmed.StartsWith('[');
    private static bool IsCommentOrBlank(string trimmed) =>
        trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('；');

    public IReadOnlyList<string> CaptureVarLines(IReadOnlyCollection<string> modIds)
    {
        var path = ResolveUserIniPath();
        if (modIds.Count == 0 || path == null || !File.Exists(path)) return Array.Empty<string>();

        // Pre-format the id needles once (\{id}\ — the namespace segment 3DMigoto writes for a deployed mod).
        var needles = modIds.Select(id => $"\\{id}\\").ToArray();
        var captured = new List<string>();
        var inConstants = false;
        foreach (var raw in File.ReadAllLines(path))
        {
            var trimmed = raw.Trim();
            if (IsSectionHeader(trimmed)) { inConstants = trimmed.Equals(ConstantsSection, StringComparison.OrdinalIgnoreCase); continue; }
            if (!inConstants || IsCommentOrBlank(trimmed) || Lhs(trimmed) == null) continue;
            if (needles.Any(n => trimmed.Contains(n, StringComparison.OrdinalIgnoreCase)))
                captured.Add(trimmed);
        }
        return captured;
    }

    public bool ApplyVarLines(IReadOnlyList<string> capturedLines)
    {
        if (capturedLines.Count == 0) return false;
        var path = ResolveUserIniPath();
        if (path == null) return false;

        // LHS(lower) -> full captured line. Later duplicates win (deterministic).
        var byKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in capturedLines)
        {
            var key = Lhs(line);
            if (key != null) byKey[key] = line.Trim();
        }
        if (byKey.Count == 0) return false;

        try
        {
            if (!File.Exists(path))
            {
                var fresh = new List<string>(Header) { "", ConstantsSection };
                fresh.AddRange(byKey.Values);
                File.WriteAllLines(path, fresh);
                _logger.Info($"Created {FileName} with {byKey.Count} preset var(s)", "D3dmigotoUserConfig");
                return true;
            }

            var lines = File.ReadAllLines(path).ToList();
            var inConstants = false;
            var constantsEnd = -1; // index just past the last line of the [Constants] section
            var replaced = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (IsSectionHeader(trimmed))
                {
                    if (inConstants) constantsEnd = i; // a NEW section ends the [Constants] block
                    inConstants = trimmed.Equals(ConstantsSection, StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inConstants) continue;
                constantsEnd = i + 1; // track the running end of [Constants] content
                var key = Lhs(trimmed);
                if (key != null && byKey.TryGetValue(key, out var repl))
                {
                    lines[i] = repl; // overwrite the persisted value with the preset's
                    seen.Add(key);
                    replaced++;
                }
            }

            var missing = byKey.Where(kv => !seen.Contains(kv.Key)).Select(kv => kv.Value).ToList();
            if (missing.Count > 0)
            {
                if (constantsEnd < 0)
                {
                    // No [Constants] section at all — append one.
                    lines.Add(ConstantsSection);
                    lines.AddRange(missing);
                }
                else
                {
                    lines.InsertRange(constantsEnd, missing);
                }
            }

            File.WriteAllLines(path, lines);
            _logger.Info($"Applied preset vars to {FileName}: {replaced} replaced, {missing.Count} added", "D3dmigotoUserConfig");
            return true;
        }
        catch (Exception ex)
        {
            // Best-effort: a failure to restore var state must never fail the whole preset apply.
            _logger.Warn($"Failed to write {FileName}: {ex.Message}", "D3dmigotoUserConfig");
            return false;
        }
    }
}
