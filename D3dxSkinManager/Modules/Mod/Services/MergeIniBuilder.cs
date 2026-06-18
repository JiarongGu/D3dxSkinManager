using System.Text;
using System.Text.RegularExpressions;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// One source mod feeding a merge: its group index (0 = the variant the merged mod starts on, cycles
/// 0,1,2,…) and the concatenated text of its <c>.ini</c> file(s).
/// </summary>
public sealed class MergeSourceIni
{
    /// <summary>Source mod index (0 = the variant the merged mod starts on). Drives $swapvar + the .{group} suffix.</summary>
    public int Group { get; init; }
    public string IniText { get; init; } = string.Empty;
    /// <summary>
    /// Forward-slashed path (relative to the merged .ini) that this source's files are staged under, so
    /// the merged mod's <c>[Resource] filename</c> values resolve. E.g. <c>"0/"</c> or <c>"0/sub/"</c>.
    /// </summary>
    public string PathPrefix { get; init; } = string.Empty;
}

/// <summary>
/// Pure builder that produces a merged 3DMigoto <c>.ini</c> from several source mods, faithfully porting
/// GIMI's <c>genshin_merge_mods.py</c>: dedup <c>[TextureOverride]</c> by <c>(hash, match_first_index)</c>,
/// gate each source via a <c>[CommandList]</c> branching on <c>$swapvar</c> (<c>if $swapvar == group</c> /
/// <c>else if</c> / <c>endif</c>), suffix resource/buffer binds by <c>.{group}</c>, and emit
/// <c>[Constants] $swapvar</c> + <c>[KeySwap]</c> cycle + <c>[Present]</c>. No file I/O — caller stages
/// files + writes resources. NOTE: reflection/credit/transparency special-cases are intentionally omitted
/// (the core swap is what matters); resource <c>filename</c> values are passed through unchanged.
/// </summary>
public static class MergeIniBuilder
{
    private static readonly string[] RecognizedHeaders =
        { "TextureOverride", "ShaderOverride", "Resource", "Constants", "Present", "CommandList", "CustomShader" };

    private sealed class Section
    {
        public string Header = string.Empty;          // e.g. "TextureOverride"
        public string Name = string.Empty;            // e.g. "VivianBodyPosition"
        public int Group;
        public string? Hash;
        public string? MatchFirstIndex;
        public bool IsResource;                        // has filename/type
        public string PathPrefix = string.Empty;       // staged location for this source's files
        // Ordered command/property/conditional lines, EXCLUDING meta (header/name/hash/match_first_index).
        public readonly List<(string Key, string Val)> Lines = new();
    }

    /// <param name="key">The single key that cycles the merged mod (e.g. "v").</param>
    /// <param name="activeOnly">Gate the swap to the on-screen character (`condition = $active == 1`).</param>
    public static string Build(IReadOnlyList<MergeSourceIni> sources, string key, bool activeOnly = true)
    {
        var sections = new List<Section>();
        foreach (var src in sources)
            foreach (var raw in SplitSections(src.IniText))
            {
                var sec = ParseSection(raw, src.Group, src.PathPrefix);
                if (sec != null) sections.Add(sec);
            }

        // Number of distinct source mods (= swap variants). NOT sources.Count, which counts .ini files
        // (a mod can have several), so the $swapvar cycle would otherwise get phantom extra values.
        var groupCount = sources.Count == 0 ? 0 : sources.Max(s => s.Group) + 1;
        var constants = new StringBuilder();
        constants.Append("; Constants ---------------------------\n\n");
        constants.Append("[Constants]\n").Append("global persist $swapvar = 0\n");
        if (activeOnly) constants.Append("global $active\n");
        constants.Append("\n[KeySwap]\n");
        if (activeOnly) constants.Append("condition = $active == 1\n");
        constants.Append($"key = {key}\n").Append("type = cycle\n");
        constants.Append("$swapvar = ").Append(string.Join(",", Enumerable.Range(0, groupCount))).Append("\n\n");
        if (activeOnly) constants.Append("[Present]\npost $active = 0\n\n");

        var overrides = new StringBuilder("; Overrides ---------------------------\n\n");
        var commands = new StringBuilder("; CommandList -------------------------\n\n");
        var resources = new StringBuilder("; Resources ---------------------------\n\n");

        // Dedup overrides by (hash, match_first_index); preserve first-seen order.
        var commandKeys = new List<(string Hash, string Index)>();
        var commandData = new Dictionary<(string, string), List<Section>>();

        foreach (var sec in sections)
        {
            if (sec.Hash != null)
            {
                var index = sec.MatchFirstIndex ?? "-1";
                var ckey = (sec.Hash, index);
                if (!commandData.ContainsKey(ckey))
                {
                    commandData[ckey] = new List<Section> { sec };
                    commandKeys.Add(ckey);
                    overrides.Append($"[{sec.Header}{sec.Name}]\nhash = {sec.Hash}\n");
                    if (index != "-1") overrides.Append($"match_first_index = {index}\n");
                    overrides.Append($"run = CommandList{sec.Name}\n");
                    if (activeOnly && sec.Name.Contains("Position")) overrides.Append("$active = 1\n");
                    overrides.Append('\n');
                }
                else
                {
                    commandData[ckey].Add(sec);
                }
            }
            else if (sec.Header == "CommandList")
            {
                var ckey = (sec.Name, "0");
                if (!commandData.ContainsKey(ckey)) { commandData[ckey] = new List<Section>(); commandKeys.Add(ckey); }
                commandData[ckey].Add(sec);
            }
            else if (sec.IsResource)
            {
                resources.Append($"[{sec.Header}{sec.Name}.{sec.Group}]\n");
                foreach (var (k, v) in sec.Lines)
                {
                    // The file lives under this source's staged path; everything else passes through.
                    var outVal = k.Equals("filename", StringComparison.OrdinalIgnoreCase) ? sec.PathPrefix + v : v;
                    resources.Append($"{k} = {outVal}\n");
                }
                resources.Append('\n');
            }
        }

        // Build command lists: one per hash, branching on $swapvar by group.
        foreach (var ckey in commandKeys)
        {
            var models = commandData[ckey];
            if (models.Count == 0) continue;
            commands.Append($"[CommandList{models[0].Name}]\nif ");
            foreach (var model in models)
            {
                commands.Append($"$swapvar == {model.Group}\n");
                var tabs = 1;
                foreach (var (cmd, val) in model.Lines)
                {
                    if (cmd == "endif")
                    {
                        tabs = Math.Max(0, tabs - 1);
                        commands.Append('\t', tabs).Append("endif");
                    }
                    else if (cmd.Contains("else if"))
                    {
                        tabs = Math.Max(0, tabs - 1);
                        commands.Append('\t', tabs).Append($"{cmd} = {val}");
                        tabs++;
                    }
                    else
                    {
                        commands.Append('\t', tabs);
                        if (cmd.StartsWith("if") || cmd.StartsWith("else if"))
                            commands.Append($"{cmd} == {val}");
                        else
                            commands.Append($"{cmd} = {val}");
                        if (cmd.StartsWith("if")) tabs++;
                        else if (NeedsGroupSuffix(cmd, val)) commands.Append($".{model.Group}");
                    }
                    commands.Append('\n');
                }
                commands.Append("else if ");
            }
            // Drop the trailing dangling "else if " and close the block.
            var text = commands.ToString();
            var cut = text.LastIndexOf("else if", StringComparison.Ordinal);
            commands.Clear().Append(text[..cut]).Append("endif\n\n");
        }

        var result = new StringBuilder();
        result.Append("; Merged mod (").Append(groupCount).Append(" variants) — generated by D3dxSkinManager\n\n");
        result.Append(constants).Append(overrides).Append(commands).Append(resources);
        return result.ToString();
    }

    /// <summary>vb/ib/ps/vs/th binds and Resource references are suffixed by group so variants don't collide.</summary>
    private static bool NeedsGroupSuffix(string cmd, string val)
    {
        var prefix = cmd.Length >= 2 ? cmd[..2] : cmd;
        var isBind = prefix is "vb" or "ib" or "ps" or "vs" or "th";
        return (isBind || val.Contains("Resource")) && !val.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Split an .ini into raw section texts (each starting with its header line), GIMI-style.</summary>
    private static IEnumerable<string> SplitSections(string iniText)
    {
        // GIMI: ["[" + x for x in text.split("[")][1:] — every chunk after a '[' is a section.
        var parts = iniText.Split('[');
        for (var i = 1; i < parts.Length; i++)
            yield return "[" + parts[i];
    }

    private static Section? ParseSection(string sectionText, int group, string pathPrefix)
    {
        var sec = new Section { Group = group, PathPrefix = pathPrefix };
        var headerSet = false;
        foreach (var rawLine in sectionText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(";") || trimmed.StartsWith("；")) continue;

            if (!headerSet && trimmed.StartsWith("["))
            {
                foreach (var h in RecognizedHeaders)
                {
                    var token = "[" + h;
                    if (trimmed.Contains(token))
                    {
                        // GIMI gives up merging the reflection fix — skip those sections.
                        if (trimmed.Contains("CommandListReflectionTexture") || trimmed.Contains("CommandListOutline"))
                            return null;
                        sec.Header = h;
                        // name = text after "[Header" up to the closing ']'
                        var after = trimmed[(trimmed.IndexOf(token, StringComparison.Ordinal) + token.Length)..];
                        sec.Name = after.TrimEnd(']');
                        headerSet = true;
                        break;
                    }
                }
                if (headerSet) continue;
            }

            if (line.Contains("=="))
            {
                var idx = line.IndexOf("==", StringComparison.Ordinal);
                sec.Lines.Add((line[..idx].Trim(), line[(idx + 2)..].Trim()));
            }
            else if (trimmed.Contains("endif"))
            {
                sec.Lines.Add(("endif", string.Empty));
            }
            else if (line.Contains('='))
            {
                var idx = line.IndexOf('=');
                var k = line[..idx].Trim();
                var v = line[(idx + 1)..].Trim();
                if (k.Contains("CharacterIB") || k.Contains("ResourceRef")) continue; // reflection-fix bits
                switch (k.ToLowerInvariant())
                {
                    case "hash": sec.Hash = v; break;
                    case "match_first_index": sec.MatchFirstIndex = v; break;
                    case "filename": sec.IsResource = true; sec.Lines.Add((k, v)); break;
                    case "type": sec.IsResource = true; sec.Lines.Add((k, v)); break;
                    default: sec.Lines.Add((k, v)); break;
                }
            }
        }
        return headerSet ? sec : null;
    }
}
