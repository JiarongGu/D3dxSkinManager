using System.Text;
using System.Text.RegularExpressions;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Namespace-based mod-merge engine. Unlike the GIMI hash-dedup approach (which rebuilds everything and
/// drops each source's keybinds), this keeps each source `.ini`
/// INTACT under its own 3DMigoto <c>namespace</c> — so every variant's <c>[Key*]</c> shortcuts,
/// <c>[Constants]</c> vars and resources are preserved as separate, collision-free sets. The only edit
/// to a source is: prepend its <c>namespace</c> and gate each <c>[TextureOverride*]</c>/<c>[ShaderOverride*]</c>
/// (with a hash) so it only renders when the master's <c>$swapvar</c> selects that variant
/// (<c>allow_duplicate_hash</c> lets the variants share a hash; <c>if $\Master\swapvar == N … endif</c>
/// picks the active one). A separate master `.ini` declares <c>$swapvar</c> + the cycle <c>[KeySwap]</c>.
///
/// NOTE: the cross-namespace gate + duplicate-hash behaviour is the part that must be confirmed in-game
/// with two real same-character mods — the INI docs confirm cross-namespace var reads but not the render
/// gating end-to-end.
/// </summary>
public static class NamespaceMergeBuilder
{
    // Bind-time keys that must stay OUTSIDE the swapvar gate (they're matched when the hash binds, not
    // command-list ops). Everything else in the override body is a command and gets gated.
    private static readonly HashSet<string> DeclarationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "hash", "match_first_index", "match_index_count", "match_priority", "match_type",
        "allow_duplicate_hash",
    };

    /// <summary>
    /// Transform a source `.ini`: strip any existing top-level <c>namespace</c>, prepend
    /// <paramref name="sourceNamespace"/>, and gate every hash-bearing <c>[TextureOverride*]</c>/
    /// <c>[ShaderOverride*]</c> on <c>$\{masterNamespace}\swapvar == {group}</c>.
    /// </summary>
    public static string TransformSource(string iniText, string sourceNamespace, string masterNamespace, int group)
    {
        var sb = new StringBuilder();
        sb.Append($"namespace = {sourceNamespace}\n\n");

        foreach (var section in SplitSections(iniText))
        {
            // Drop a pre-existing namespace declaration (we set our own); keep other preamble.
            if (section.Header == null)
            {
                foreach (var line in section.Body)
                    if (!Regex.IsMatch(line, @"^\s*namespace\s*=", RegexOptions.IgnoreCase))
                        sb.Append(line).Append('\n');
                continue;
            }

            var name = section.Header.Trim().Trim('[', ']').Trim();
            var gate = (name.StartsWith("TextureOverride", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("ShaderOverride", StringComparison.OrdinalIgnoreCase))
                       && section.Body.Any(l => Regex.IsMatch(l, @"^\s*hash\s*=", RegexOptions.IgnoreCase));

            sb.Append(section.Header).Append('\n');

            if (!gate)
            {
                foreach (var line in section.Body) sb.Append(line).Append('\n');
                continue;
            }

            // Gated override: declarations first (+ allow_duplicate_hash so variants can share the hash),
            // then the command body wrapped in the swapvar gate.
            var decls = new List<string>();
            var cmds = new List<string>();
            foreach (var line in section.Body)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(";") || trimmed.StartsWith("；")) { cmds.Add(line); continue; }
                var eq = trimmed.IndexOf('=');
                var key = eq > 0 ? trimmed[..eq].Trim() : trimmed;
                if (DeclarationKeys.Contains(key)) decls.Add(line);
                else cmds.Add(line);
            }

            foreach (var d in decls) sb.Append(d).Append('\n');
            sb.Append("allow_duplicate_hash = true\n");
            sb.Append($"if $\\{masterNamespace}\\swapvar == {group}\n");
            foreach (var c in cmds) sb.Append(c).Append('\n');
            sb.Append("endif\n");
        }

        return sb.ToString();
    }

    /// <summary>The master `.ini`: declares the swap var + the cycle key. Sources reference its swapvar.</summary>
    public static string BuildMaster(string masterNamespace, string key, int variantCount, bool activeOnly)
    {
        var sb = new StringBuilder();
        sb.Append($"namespace = {masterNamespace}\n\n");
        sb.Append("[Constants]\n").Append("global persist $swapvar = 0\n");
        if (activeOnly) sb.Append("global $active\n");
        sb.Append('\n');
        sb.Append("[KeySwap]\n");
        if (activeOnly) sb.Append("condition = $active == 1\n");
        sb.Append($"key = {key}\n").Append("type = cycle\n");
        sb.Append("$swapvar = ").Append(string.Join(",", Enumerable.Range(0, variantCount))).Append('\n');
        if (activeOnly) sb.Append("\n[Present]\npost $active = 0\n");
        return sb.ToString();
    }

    private sealed class Section
    {
        public string? Header;
        public readonly List<string> Body = new();
    }

    private static List<Section> SplitSections(string iniText)
    {
        var sections = new List<Section> { new() }; // preamble (header == null)
        foreach (var raw in iniText.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                sections.Add(new Section { Header = raw });
            else
                sections[^1].Body.Add(raw);
        }
        return sections;
    }
}
