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

    // The on-screen flag each source declares + sets in ITS OWN namespace. The master KeySwap OR-reads
    // these cross-namespace. Named distinctively so it never clashes with a source mod's own $active/var.
    private const string ActiveVar = "$mergeactive";

    /// <summary>
    /// Transform a source `.ini`: strip any existing top-level <c>namespace</c>, prepend
    /// <paramref name="sourceNamespace"/>, and gate every hash-bearing <c>[TextureOverride*]</c>/
    /// <c>[ShaderOverride*]</c> on <c>$\global\{masterNamespace}\swapvar == {group}</c>.
    ///
    /// When <paramref name="activeOnly"/>, the source also declares a LOCAL <c>global $mergeactive</c>,
    /// sets it to 1 inside its gated (active) override, and resets it each frame in <c>[Present]</c>.
    /// The master's cycle key reads these flags cross-namespace. This split matters: the namespace docs
    /// only prove cross-namespace <b>reads</b> (and local writes) — a cross-namespace <b>write</b>
    /// (the old <c>$\global\Master\active = 1</c> from a source) does NOT take effect, which left the
    /// master's condition permanently false and the switch key dead even though the swap rendered fine.
    /// </summary>
    public static string TransformSource(string iniText, string sourceNamespace, string masterNamespace, int group, bool activeOnly = true)
    {
        var sb = new StringBuilder();
        sb.Append($"namespace = {sourceNamespace}\n\n");

        // Cross-namespace var read = $\<namespace>\<var> (per the 3DMigoto namespace docs: a var declared
        // in `namespace = global\tracking` is read as `$\global\tracking\isSwimming` — the leading
        // `global` is PART OF THE NAMESPACE NAME, not a magic prefix). So the address MUST equal the
        // master's declared namespace exactly. The earlier code prepended an EXTRA `global\` only on the
        // read side → the address didn't match the declared namespace → the var never resolved → the `if`
        // failed open and BOTH variants drew. (ModMergeService roots the namespaces under `global\` so
        // the declared namespace already starts with it.) Reads across namespaces are the proven
        // primitive; writes are not.
        var swapVar = $"$\\{masterNamespace}\\swapvar";

        var sawConstants = false;
        var sawPresent = false;

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

            // [Constants]: re-emit untouched + declare our own LOCAL on-screen flag (a same-namespace
            // declaration; the master reads it cross-namespace).
            if (activeOnly && name.Equals("Constants", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(section.Header).Append('\n');
                foreach (var line in section.Body) sb.Append(line).Append('\n');
                sb.Append($"global {ActiveVar} = 0\n");
                sawConstants = true;
                continue;
            }

            // [Present]: re-emit + reset the flag at frame end (post), so it clears when the char leaves.
            if (activeOnly && name.Equals("Present", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(section.Header).Append('\n');
                foreach (var line in section.Body) sb.Append(line).Append('\n');
                sb.Append($"post {ActiveVar} = 0\n");
                sawPresent = true;
                continue;
            }

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
            sb.Append($"if {swapVar} == {group}\n");
            // When the active variant's hash renders, flag the character on-screen (LOCAL write — the
            // proven primitive). The master's cycle key reads this cross-namespace so it only fires for
            // the character currently on screen, not globally.
            if (activeOnly) sb.Append($"  {ActiveVar} = 1\n");
            foreach (var c in cmds) sb.Append("  ").Append(c.TrimStart()).Append('\n');
            sb.Append("endif\n");
        }

        // If the source had no [Constants]/[Present], add them so the flag exists + resets each frame.
        if (activeOnly && !sawConstants) sb.Append($"\n[Constants]\nglobal {ActiveVar} = 0\n");
        if (activeOnly && !sawPresent) sb.Append($"\n[Present]\npost {ActiveVar} = 0\n");

        return sb.ToString();
    }

    /// <summary>
    /// The master `.ini`: declares the swap var + the cycle key. Sources reference its swapvar; the key
    /// reads each source's on-screen flag cross-namespace (OR-ed) when <paramref name="activeOnly"/>.
    /// </summary>
    public static string BuildMaster(string masterNamespace, IReadOnlyList<string> sourceNamespaces, string key, bool activeOnly)
    {
        var sb = new StringBuilder();
        sb.Append($"namespace = {masterNamespace}\n\n");
        sb.Append("[Constants]\n").Append("global persist $swapvar = 0\n");
        sb.Append('\n');
        sb.Append("[KeySwap]\n");
        // Only fire the cycle when one of the merged characters is on screen (each source sets its own
        // $mergeactive when its variant renders). Cross-namespace READS are the proven primitive, so the
        // key OR-reads the source-local flags rather than relying on a (non-working) cross-namespace write.
        if (activeOnly && sourceNamespaces.Count > 0)
        {
            var ors = string.Join(" || ", sourceNamespaces.Select(ns => $"$\\{ns}\\mergeactive == 1"));
            sb.Append($"condition = {ors}\n");
        }
        sb.Append($"key = {key}\n").Append("type = cycle\n");
        sb.Append("$swapvar = ").Append(string.Join(",", Enumerable.Range(0, sourceNamespaces.Count))).Append('\n');
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
