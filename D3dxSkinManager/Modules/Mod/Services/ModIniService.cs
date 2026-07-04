using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// General mod .ini editor backend: parses a mod's extracted .ini files into sections + key/value
/// entries, classifies each entry as user-editable (Key/Constants tuning) or advanced/read-only
/// (hash / *Override / Resource / Shader / command-list), and writes a single edited value back —
/// patching just the one .ini into the archive via the fast single-file path (no full recompress).
/// Builds on the same parse + write-back foundation as <see cref="ModKeybindingService"/>.
/// </summary>
public interface IModIniService
{
    /// <summary>Parse all .ini files in the mod's extracted cache into the editable model. Empty if not extracted.</summary>
    Task<List<ModIniFile>> GetIniFilesAsync(string modId);

    /// <summary>
    /// Change one entry's value (identified by file + line index), preserving the key, indentation and any
    /// trailing comment. Re-validates server-side that the line is editable (never writes a locked line).
    /// Patches only that .ini into the archive. Returns the rewritten line.
    /// </summary>
    Task<string> UpdateEntryAsync(string modId, string relativePath, int lineIndex, string newValue);
}

public class ModIniService : IModIniService
{
    private readonly IModCacheService _cacheService;
    private readonly IModArchiveService _archiveService;
    private readonly IModOperationQueue _operationQueue;

    public ModIniService(
        IModCacheService cacheService,
        IModArchiveService archiveService,
        IModOperationQueue operationQueue)
    {
        _cacheService = cacheService;
        _archiveService = archiveService;
        _operationQueue = operationQueue;
    }

    // ---- Editability classification (the crux) -------------------------------------------------
    // Only [Key*] (keybind/toggle tuning) and [Constants] (variable defaults) sections are tunable.
    // Everything else — *Override (hash binds), Resource, Shader*, CommandList*, Present, Loader,
    // Device, Rendering, etc. — is advanced/read-only. Within a tunable section, a `run = CommandList`
    // line (or a value that begins a command) is still locked. When unsure, lock.

    private static bool IsAdvancedSection(string sectionName) =>
        !(sectionName.StartsWith("Key", StringComparison.OrdinalIgnoreCase)
          || sectionName.Equals("Constants", StringComparison.OrdinalIgnoreCase));

    /// <summary>Classify an entry. Returns (editable, lockReason|null).</summary>
    private static (bool Editable, string? Reason) Classify(string sectionName, string key, string value)
    {
        if (IsAdvancedSection(sectionName)) return (false, "advancedSection");

        var lhs = key.Trim().ToLowerInvariant();
        if (lhs == "run" || lhs.StartsWith("run ")) return (false, "command");

        var v = value.TrimStart().ToLowerInvariant();
        // A value that begins a command-list statement is not a plain editable value.
        if (v.StartsWith("run ") || v == "if" || v.StartsWith("if ")
            || v.StartsWith("elif") || v == "else" || v.StartsWith("endif")
            || v.StartsWith("draw") || v.StartsWith("dispatch"))
            return (false, "command");

        return (true, null);
    }

    // Strip an inline comment (`;` or fullwidth `；`) from a value's RHS, returning the value part only.
    private static string StripInlineComment(string rhs)
    {
        var idx = rhs.IndexOfAny(new[] { ';', '；' });
        return (idx >= 0 ? rhs[..idx] : rhs).Trim();
    }

    public async Task<List<ModIniFile>> GetIniFilesAsync(string modId)
    {
        var files = new List<ModIniFile>();
        var cacheDir = _cacheService.GetCachePath(modId);
        if (cacheDir == null) return files;

        foreach (var iniPath in Directory.GetFiles(cacheDir, "*.ini", SearchOption.AllDirectories)
                     // Skip disabled .ini (e.g. a merged mod's DISABLED*.ini sources) — inactive in-game.
                     .Where(p => !Path.GetFileName(p).Contains("disabled", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var lines = await File.ReadAllLinesAsync(iniPath).ConfigureAwait(false);
            var file = new ModIniFile
            {
                RelativePath = Path.GetRelativePath(cacheDir, iniPath).Replace('\\', '/'),
                FileName = Path.GetFileName(iniPath),
            };

            ModIniSection? section = null;
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(";") || trimmed.StartsWith("；")) continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    var name = trimmed.Trim('[', ']').Trim();
                    section = new ModIniSection { Name = name, Advanced = IsAdvancedSection(name) };
                    file.Sections.Add(section);
                    continue;
                }

                // Capture the 3DMigoto `namespace = X` directive (relates files together). It may appear
                // before any section, so check it regardless of section context.
                if (file.Namespace == null)
                {
                    var ns = Regex.Match(trimmed, @"^namespace\s*=\s*(.+?)\s*(?:[;；].*)?$", RegexOptions.IgnoreCase);
                    if (ns.Success) { file.Namespace = ns.Groups[1].Value.Trim(); continue; }
                }

                if (section == null) continue; // assignment before any section — ignore for editing
                var eq = trimmed.IndexOf('=');
                if (eq <= 0) continue;

                var rawKey = trimmed[..eq].Trim();
                var value = StripInlineComment(trimmed[(eq + 1)..]);
                var (editable, reason) = Classify(section.Name, rawKey, value);
                section.Entries.Add(new ModIniEntry
                {
                    Key = rawKey,
                    Value = value,
                    LineIndex = i,
                    Editable = editable,
                    LockReason = reason,
                });
            }

            // Only surface files that actually have sections (skip junk).
            if (file.Sections.Count > 0) files.Add(file);
        }

        return files;
    }

    public Task<string> UpdateEntryAsync(string modId, string relativePath, int lineIndex, string newValue)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("relativePath is required");
        // Newlines in a value would corrupt the line/section structure.
        if (newValue.Contains('\n') || newValue.Contains('\r'))
            throw new OperationException("INI_VALUE_INVALID", "value", newValue);

        return _operationQueue.EnqueueAsync(modId, async () =>
        {
            var cacheDir = _cacheService.GetCachePath(modId);
            if (cacheDir == null)
                throw new OperationException("MOD_NOT_EXTRACTED", "id", modId);

            // Resolve + contain the target file under the cache dir (no path traversal).
            var fullPath = Path.GetFullPath(Path.Combine(cacheDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var cacheFull = Path.GetFullPath(cacheDir);
            if (!fullPath.StartsWith(cacheFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                throw new OperationException("INI_FILE_NOT_FOUND", "file", relativePath);

            var lines = await File.ReadAllLinesAsync(fullPath).ConfigureAwait(false);
            if (lineIndex < 0 || lineIndex >= lines.Length)
                throw new OperationException("INI_LINE_INVALID", "line", lineIndex.ToString());

            var original = lines[lineIndex];
            // prefix = leading ws + key + ws + '=' + ws ; value ; trailing = ws + optional comment
            var m = Regex.Match(original, @"^(\s*[^=;；\[][^=]*?\s*=\s*)(.*?)(\s*(?:[;；].*)?)$");
            if (!m.Success)
                throw new OperationException("INI_LINE_INVALID", "line", lineIndex.ToString());

            // Server-side guard: recompute the section for this line and re-classify — never write a locked line.
            var sectionName = SectionNameForLine(lines, lineIndex);
            var key = m.Groups[1].Value.TrimEnd().TrimEnd('=').Trim();
            var (editable, _) = Classify(sectionName, key, newValue);
            if (!editable)
                throw new OperationException("INI_ENTRY_READONLY", "key", key);

            var rewritten = m.Groups[1].Value + newValue.Trim() + m.Groups[3].Value;
            if (rewritten == original) return rewritten; // no-op, skip the archive patch

            lines[lineIndex] = rewritten;
            await File.WriteAllLinesAsync(fullPath, lines).ConfigureAwait(false);

            // Persist FAST: patch only this .ini inside the archive (no full recompress).
            await _archiveService.UpdateFileInArchiveAsync(modId, fullPath, relativePath.Replace('\\', '/')).ConfigureAwait(false);
            return rewritten;
        });
    }

    /// <summary>Walk up from a line to find the enclosing section header name ("" if none).</summary>
    private static string SectionNameForLine(string[] lines, int lineIndex)
    {
        for (var i = lineIndex; i >= 0; i--)
        {
            var t = lines[i].Trim();
            if (t.StartsWith("[") && t.EndsWith("]")) return t.Trim('[', ']').Trim();
        }
        return string.Empty;
    }
}
