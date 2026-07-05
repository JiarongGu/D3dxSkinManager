using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;

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

    /// <summary>
    /// Repair unbalanced if/endif blocks across the mod's ACTIVE .ini files (the analyzer's
    /// UnbalancedCondition finding): appends missing <c>endif</c> lines at section end and comments
    /// out stray extra <c>endif</c> lines. Requires an extracted cache; patched files persist via
    /// the fast single-file archive path.
    /// </summary>
    Task<IniRepairResult> RepairConditionBalanceAsync(string modId);
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

    public async Task<List<ModIniFile>> GetIniFilesAsync(string modId)
    {
        var files = new List<ModIniFile>();
        var cacheDir = _cacheService.GetCachePath(modId);
        if (cacheDir == null) return files;

        foreach (var iniPath in Directory.GetFiles(cacheDir, "*.ini", SearchOption.AllDirectories)
                     // Skip disabled files/folders (DISABLED*.ini, disabled subdirs) — inactive in-game.
                     .Where(p => !IniParser.IsDisabledPath(Path.GetRelativePath(cacheDir, p)))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var lines = await File.ReadAllLinesAsync(iniPath).ConfigureAwait(false);
            var doc = IniParser.Parse(lines);
            var file = new ModIniFile
            {
                RelativePath = Path.GetRelativePath(cacheDir, iniPath).Replace('\\', '/'),
                FileName = Path.GetFileName(iniPath),
                Namespace = doc.Namespace,
            };

            foreach (var parsed in doc.Sections)
            {
                var section = new ModIniSection { Name = parsed.Name, Advanced = IsAdvancedSection(parsed.Name) };
                file.Sections.Add(section);
                foreach (var entry in parsed.Entries)
                {
                    // Control-flow / command lines without '=' (if/else/endif, bare draw) aren't
                    // key=value entries — nothing to edit.
                    if (entry.Key == null || entry.Value == null) continue;

                    var (editable, reason) = Classify(section.Name, entry.Key, entry.Value);
                    section.Entries.Add(new ModIniEntry
                    {
                        Key = entry.Key,
                        Value = entry.Value,
                        LineIndex = entry.LineIndex,
                        Editable = editable,
                        LockReason = reason,
                    });
                }
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

    public Task<IniRepairResult> RepairConditionBalanceAsync(string modId)
    {
        return _operationQueue.EnqueueAsync(modId, async () =>
        {
            var cacheDir = _cacheService.GetCachePath(modId);
            if (cacheDir == null)
                throw new OperationException("MOD_NOT_EXTRACTED", "id", modId);

            var result = new IniRepairResult();
            foreach (var iniPath in Directory.GetFiles(cacheDir, "*.ini", SearchOption.AllDirectories)
                         .Where(p => !IniParser.IsDisabledPath(Path.GetRelativePath(cacheDir, p)))
                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var lines = (await File.ReadAllLinesAsync(iniPath).ConfigureAwait(false)).ToList();
                int added = 0, commented = 0;

                // Walk sections tracking if/endif depth on NORMALIZED lines (same rules as the
                // analyzer's check): stray endif (depth would go negative) → comment the line out;
                // unclosed if at section end → insert the missing endif(s) before the next header.
                var output = new List<string>(lines.Count + 4);
                int depth = 0;
                bool inSection = false;

                void CloseSection()
                {
                    for (; depth > 0; depth--) { output.Add("endif ; auto-repaired: missing endif"); added++; }
                }

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    var meaningful = trimmed.Length > 0 && !IniParser.IsCommentLine(trimmed)
                        ? IniParser.StripInlineComment(trimmed)
                        : string.Empty;

                    if (meaningful.StartsWith('[') && meaningful.EndsWith(']'))
                    {
                        CloseSection();
                        inSection = true;
                        output.Add(line);
                        continue;
                    }

                    if (inSection && meaningful.Length > 0)
                    {
                        if (meaningful.StartsWith("if ", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(meaningful, "if", StringComparison.OrdinalIgnoreCase))
                        {
                            depth++;
                        }
                        else if (string.Equals(meaningful, "endif", StringComparison.OrdinalIgnoreCase))
                        {
                            if (depth == 0)
                            {
                                output.Add("; auto-repaired stray endif: " + line.Trim());
                                commented++;
                                continue;
                            }
                            depth--;
                        }
                    }

                    output.Add(line);
                }
                CloseSection();

                if (added == 0 && commented == 0) continue;

                await File.WriteAllLinesAsync(iniPath, output).ConfigureAwait(false);
                var relative = Path.GetRelativePath(cacheDir, iniPath).Replace('\\', '/');
                await _archiveService.UpdateFileInArchiveAsync(modId, iniPath, relative).ConfigureAwait(false);

                result.FilesChanged++;
                result.EndifsAdded += added;
                result.StraysCommented += commented;
            }

            return result;
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
