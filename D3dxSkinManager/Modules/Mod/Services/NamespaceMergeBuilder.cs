using System.Text;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Helpers;

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
/// The render gate follows the docs' ONE proven cross-namespace pattern: each source's <c>[Present]</c>
/// mirrors the master's <c>$swapvar</c> into a LOCAL <c>$mergeswap</c> once per frame
/// (<c>$mergeswap = $\global\{master}\swapvar</c> — identical in shape to the docs'
/// <c>$swapvar = $\global\tracking\isSwimming</c>), and each gated override branches on the LOCAL
/// (<c>if $mergeswap == N</c>). Doing the cross-namespace read inline inside the override — the two
/// earlier attempts — is undocumented and rendered the character invisible in-game (2026-07-06).
///
/// NOTE: still confirm in-game with two real same-character mods. If it still misbehaves, the
/// guaranteed-working fallback is <paramref name="activeOnly"/>=false + no gate (the swap key just
/// cycles unconditionally).
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

    // LOCAL mirror of the master's swapvar. The cross-namespace READ that populates it lives in the
    // source's [Present] — this is the EXACT (and only) proven cross-namespace pattern in the 3DMigoto
    // namespace docs: `[Present] $swapvar = $\global\tracking\isSwimming` mirrors another namespace's
    // var into a local, once per frame. Overrides then branch on the LOCAL mirror (a same-namespace
    // read — always works). Two earlier attempts did the cross-ns read INLINE inside every
    // TextureOverride (in the `if` condition, then in an assignment) — neither is a documented pattern
    // and both left the character INVISIBLE in-game (user reports 2026-07-06). See 3dmigoto-ini-interface.md.
    private const string SwapMirror = "$mergeswap";

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
        // `global` is PART OF THE NAMESPACE NAME, not a magic prefix). ModMergeService roots the
        // namespaces under `global\` so the declared namespace already starts with it.
        // IMPORTANT: this cross-namespace read is only used as the RHS of an ASSIGNMENT into a LOCAL
        // mirror (proven primitive); the gate then reads the LOCAL. Reading it directly in an `if`
        // condition (the earlier code) is unproven and rendered the character invisible.
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
            if (name.Equals("Constants", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(section.Header).Append('\n');
                foreach (var line in section.Body) sb.Append(line).Append('\n');
                if (activeOnly) sb.Append($"global {ActiveVar} = 0\n");
                sb.Append($"global {SwapMirror} = 0\n"); // local mirror of the master swapvar
                sawConstants = true;
                continue;
            }

            // [Present]: runs every frame. Mirror the master's swapvar into our LOCAL here — this is the
            // EXACT proven cross-namespace-read pattern from the namespace docs
            // (`[Present] $swapvar = $\global\tracking\isSwimming`). Overrides branch on the LOCAL mirror.
            // When activeOnly, also reset the on-screen flag at frame end (post).
            if (name.Equals("Present", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(section.Header).Append('\n');
                foreach (var line in section.Body) sb.Append(line).Append('\n');
                sb.Append($"{SwapMirror} = {swapVar}\n");
                if (activeOnly) sb.Append($"post {ActiveVar} = 0\n");
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
                if (trimmed.Length == 0 || IniParser.IsCommentLine(trimmed)) { cmds.Add(line); continue; }
                var eq = trimmed.IndexOf('=');
                var key = eq > 0 ? trimmed[..eq].Trim() : trimmed;
                if (DeclarationKeys.Contains(key)) decls.Add(line);
                else cmds.Add(line);
            }

            foreach (var d in decls) sb.Append(d).Append('\n');
            sb.Append("allow_duplicate_hash = true\n");
            // Gate on the LOCAL swapvar mirror — a same-namespace read, which always works. The
            // cross-namespace read that populates the mirror lives in [Present] (see above). Doing the
            // cross-ns read inline here (the two previous attempts) is not a documented pattern and
            // rendered the character invisible.
            sb.Append($"if {SwapMirror} == {group}\n");
            // When the active variant's hash renders, flag the character on-screen (LOCAL write — the
            // proven primitive). The master's cycle key reads this cross-namespace so it only fires for
            // the character currently on screen, not globally.
            if (activeOnly) sb.Append($"  {ActiveVar} = 1\n");
            foreach (var c in cmds) sb.Append("  ").Append(c.TrimStart()).Append('\n');
            sb.Append("endif\n");
        }

        // If the source had no [Constants], add one declaring the swap mirror (+ the on-screen flag).
        if (!sawConstants)
        {
            sb.Append("\n[Constants]\n");
            if (activeOnly) sb.Append($"global {ActiveVar} = 0\n");
            sb.Append($"global {SwapMirror} = 0\n");
        }
        // Every source needs a [Present] to host the per-frame cross-namespace swapvar mirror — even
        // without activeOnly. (The gate in each override reads the LOCAL mirror this populates.)
        if (!sawPresent)
        {
            sb.Append("\n[Present]\n");
            sb.Append($"{SwapMirror} = {swapVar}\n");
            if (activeOnly) sb.Append($"post {ActiveVar} = 0\n");
        }

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
