using System.Text.RegularExpressions;

namespace D3dxSkinManager.Modules.Core.Helpers;

/// <summary>
/// Tolerant, game-agnostic reader for 3DMigoto mod <c>.ini</c> files.
/// Grounded in the authoritative INI docs (leotorrez.github.io/modding/docs) and real mods on disk —
/// see <c>.claude/rules/3dmigoto-ini-interface.md</c>. Key tolerances real mods require:
/// comments start with <c>;</c> OR the fullwidth <c>；</c> (mods mix them), values may carry inline
/// comments, and files/folders prefixed "DISABLED" are skipped by the runtime (XXMI
/// <c>exclude_recursive</c> / the GIMI merge convention) so they must not count as active content.
/// Read-only: services that WRITE ini lines keep their own line-level rewriters.
/// </summary>
public static class IniParser
{
    private static readonly Regex SectionHeaderRegex = new(@"^\[(.+)\]$", RegexOptions.Compiled);

    /// <summary>A meaningful line inside a section. <see cref="Key"/> is null for control-flow /
    /// command lines without '=' (if/else/endif, drawindexed = is keyed, bare 'endif' is not).</summary>
    public sealed class IniEntry
    {
        public int LineIndex { get; init; }
        public string Raw { get; init; } = string.Empty;
        public string? Key { get; init; }
        public string? Value { get; init; }
    }

    public sealed class IniSectionData
    {
        public string Name { get; init; } = string.Empty;
        public int LineIndex { get; init; }
        public List<IniEntry> Entries { get; } = [];

        /// <summary>First value for a key (case-insensitive), or null.</summary>
        public string? GetValue(string key) =>
            Entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

        public bool HasKey(string key) =>
            Entries.Any(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

        public bool HasKeyStartingWith(string prefix) =>
            Entries.Any(e => e.Key != null && e.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public sealed class IniDocumentData
    {
        /// <summary>The file's <c>namespace =</c> directive (must be the first meaningful line), if any.</summary>
        public string? Namespace { get; set; }
        public List<IniSectionData> Sections { get; } = [];
    }

    /// <summary>True when the trimmed line is a full-line comment (<c>;</c> or fullwidth <c>；</c>).</summary>
    public static bool IsCommentLine(string trimmed) =>
        trimmed.StartsWith(';') || trimmed.StartsWith('；');

    /// <summary>Strips an inline comment (first <c>;</c>/<c>；</c> after content) and trailing space.</summary>
    public static string StripInlineComment(string line)
    {
        var ascii = line.IndexOf(';');
        var fullwidth = line.IndexOf('；');
        var ci = (ascii, fullwidth) switch
        {
            (< 0, < 0) => -1,
            (< 0, _) => fullwidth,
            (_, < 0) => ascii,
            _ => Math.Min(ascii, fullwidth),
        };
        return ci > 0 ? line[..ci].TrimEnd() : line;
    }

    /// <summary>
    /// True when a path (relative to the mod root) is disabled by convention: any segment —
    /// file or folder — starting with "disabled" (case-insensitive). Matches XXMI's
    /// <c>exclude_recursive = DISABLED*</c> and the GIMI merge's <c>DISABLED*.ini</c> renames.
    /// </summary>
    public static bool IsDisabledPath(string relativePath) =>
        relativePath.Split('\\', '/').Any(seg => seg.StartsWith("disabled", StringComparison.OrdinalIgnoreCase));

    private static bool IsControlFlowLine(string trimmed) =>
        trimmed.StartsWith("if ", StringComparison.OrdinalIgnoreCase) ||
        trimmed.StartsWith("elif ", StringComparison.OrdinalIgnoreCase) ||
        trimmed.StartsWith("else", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(trimmed, "endif", StringComparison.OrdinalIgnoreCase);

    /// <summary>Parse ini lines into sections + entries (comments stripped, blank lines dropped).</summary>
    public static IniDocumentData Parse(IReadOnlyList<string> lines)
    {
        var doc = new IniDocumentData();
        IniSectionData? current = null;
        bool seenMeaningful = false;

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length == 0 || IsCommentLine(trimmed)) continue;
            trimmed = StripInlineComment(trimmed);
            if (trimmed.Length == 0) continue;

            var sm = SectionHeaderRegex.Match(trimmed);
            if (sm.Success)
            {
                current = new IniSectionData { Name = sm.Groups[1].Value.Trim(), LineIndex = i };
                doc.Sections.Add(current);
                seenMeaningful = true;
                continue;
            }

            // Control-flow lines (`if $x == 1`, `else if …`, `endif`) contain comparison operators —
            // never split them on '=' or the first '=' of '==' produces a bogus key.
            var eq = IsControlFlowLine(trimmed) ? -1 : trimmed.IndexOf('=');
            string? key = null, value = null;
            if (eq > 0)
            {
                key = trimmed[..eq].Trim();
                value = trimmed[(eq + 1)..].Trim();
            }

            // The namespace directive must be the FIRST meaningful line of the file (before any section).
            if (!seenMeaningful && current == null &&
                string.Equals(key, "namespace", StringComparison.OrdinalIgnoreCase))
            {
                doc.Namespace = value;
                seenMeaningful = true;
                continue;
            }
            seenMeaningful = true;

            current?.Entries.Add(new IniEntry { LineIndex = i, Raw = trimmed, Key = key, Value = value });
        }

        return doc;
    }
}
