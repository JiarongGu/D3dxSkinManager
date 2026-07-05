using System;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Structural tests for the namespace-based merge engine (v2): a source is namespaced + its overrides
/// gated by $swapvar, while its keybinds/constants are preserved untouched. (In-game render gating is
/// verified separately with real same-character mods.)
/// </summary>
public class NamespaceMergeBuilderTests
{
    private const string Source = @"[Constants]
global persist $hair = 0

[KeyHair]
key = h
$hair = 0,1

[TextureOverrideBody]
hash = abcd1234
handling = skip
vb0 = ResourceBody
drawindexed = auto
";

    [Fact]
    public void TransformSource_NamespacesFile_PreservesKeysAndConstants()
    {
        var ini = NamespaceMergeBuilder.TransformSource(Source, "Merge\\mod0", "Merge\\Master", 0);

        ini.Should().StartWith("namespace = Merge\\mod0");
        // Keybinds + constants kept intact (the "separate sets" requirement) — not gated, not renamed.
        ini.Should().Contain("[Constants]").And.Contain("global persist $hair = 0");
        ini.Should().Contain("[KeyHair]").And.Contain("key = h").And.Contain("$hair = 0,1");
    }

    [Fact]
    public void TransformSource_GatesOverrideBody_BySwapvar_HashStaysOutside()
    {
        var ini = NamespaceMergeBuilder.TransformSource(Source, "Merge\\mod1", "Merge\\Master", 1);

        ini.Should().Contain("[TextureOverrideBody]");
        ini.Should().Contain("allow_duplicate_hash = true");
        // The master swapvar is mirrored into a LOCAL via a cross-namespace read in [Present] (the docs'
        // one proven pattern), and each gated override branches on the LOCAL. Reading the cross-ns var
        // inline in the override (in the `if`, then in an assignment) was the invisible-character bug
        // (user reports 2026-07-06) — the mirror must live in [Present], the gate must read the local.
        ini.Should().Contain("$mergeswap = $\\Merge\\Master\\swapvar");
        ini.Should().Contain("if $mergeswap == 1");
        ini.Should().NotContain("if $\\Merge\\Master\\swapvar");        // never gate on the cross-ns read directly
        ini.Should().Contain("global $mergeswap = 0");                   // local mirror declared
        ini.Should().Contain("endif");
        // The cross-namespace read must sit in [Present] (per-frame mirror), AFTER the override's local gate.
        var presentAt = ini.IndexOf("[Present]", StringComparison.Ordinal);
        var mirrorAt = ini.IndexOf("$mergeswap = $\\Merge\\Master\\swapvar", StringComparison.Ordinal);
        presentAt.Should().BeGreaterThan(0);
        mirrorAt.Should().BeGreaterThan(presentAt, "the cross-namespace read lives in [Present], not inline in the override");
        // Active variant flags itself on-screen via a LOCAL write (the proven primitive — a cross-namespace
        // write to the master's $active never took effect, which left the switch key dead). The master
        // reads this flag cross-namespace.
        ini.Should().Contain("$mergeactive = 1");
        ini.Should().NotContain("$\\global\\Merge\\Master\\active = 1");
        // The source declares its own flag + resets it each frame.
        ini.Should().Contain("global $mergeactive = 0");
        ini.Should().Contain("post $mergeactive = 0");

        // hash is a bind-time declaration → before the gate; the draw commands → inside it.
        var hashAt = ini.IndexOf("hash = abcd1234", StringComparison.Ordinal);
        var ifAt = ini.IndexOf("if $mergeswap == 1", StringComparison.Ordinal);
        var drawAt = ini.IndexOf("drawindexed = auto", StringComparison.Ordinal);
        var endifAt = ini.IndexOf("endif", StringComparison.Ordinal);
        hashAt.Should().BeLessThan(ifAt);
        ifAt.Should().BeLessThan(drawAt);
        drawAt.Should().BeLessThan(endifAt);
        // handling=skip is a command → gated (inside the if), not a bind-time declaration.
        ini.IndexOf("handling = skip", StringComparison.Ordinal).Should().BeGreaterThan(ifAt);
    }

    [Fact]
    public void BuildMaster_HasSwapvarAndCycleKey()
    {
        var sources = new[] { "Merge\\mod0", "Merge\\mod1", "Merge\\mod2" };
        var master = NamespaceMergeBuilder.BuildMaster("Merge\\Master", sources, "v", activeOnly: true);

        master.Should().StartWith("namespace = Merge\\Master");
        master.Should().Contain("global persist $swapvar = 0");
        master.Should().Contain("[KeySwap]").And.Contain("key = v").And.Contain("type = cycle");
        master.Should().Contain("$swapvar = 0,1,2");          // one per variant
        // The cycle key fires only when a merged char is on screen — OR over each source's local flag,
        // read cross-namespace (the proven primitive). No master $active / cross-namespace write.
        master.Should().Contain("condition = $\\Merge\\mod0\\mergeactive == 1 || $\\Merge\\mod1\\mergeactive == 1 || $\\Merge\\mod2\\mergeactive == 1");
        master.Should().NotContain("$active");
    }

    [Fact]
    public void BuildMaster_NoActiveOnly_OmitsCondition_SoKeyAlwaysCycles()
    {
        var sources = new[] { "Merge\\mod0", "Merge\\mod1" };
        var master = NamespaceMergeBuilder.BuildMaster("Merge\\Master", sources, "v", activeOnly: false);

        master.Should().Contain("[KeySwap]").And.Contain("key = v").And.Contain("type = cycle");
        master.Should().NotContain("condition =");   // unconditional cycle
        master.Should().Contain("$swapvar = 0,1");
    }

    [Fact]
    public void GateAddress_MatchesDeclaredNamespace_ForGlobalRootedNamespaces()
    {
        // Production roots namespaces under `global\` (mirrors the proven 3DMigoto docs example). The gate
        // read MUST be byte-for-byte the declared master namespace — else it fails open and both draw.
        const string master = "global\\Foo\\Master";
        var src = NamespaceMergeBuilder.TransformSource(Source, "global\\Foo\\mod0", master, 0);
        // The cross-ns read address must equal the declared master namespace exactly (used as an
        // assignment RHS into the local mirror), and the gate reads the local.
        src.Should().Contain($"$mergeswap = $\\{master}\\swapvar");     // == "$mergeswap = $\global\Foo\Master\swapvar"
        src.Should().Contain("if $mergeswap == 0");
        src.Should().NotContain("$\\global\\global\\");                 // no DOUBLE global\ (the old bug class)

        var masterIni = NamespaceMergeBuilder.BuildMaster(master, new[] { "global\\Foo\\mod0" }, "v", activeOnly: true);
        masterIni.Should().StartWith("namespace = global\\Foo\\Master");
        masterIni.Should().Contain("condition = $\\global\\Foo\\mod0\\mergeactive == 1");
    }

    [Fact]
    public void TransformSource_InjectsMirrorIntoExistingPresent_Once()
    {
        // A source that already HAS a [Present] must get the swapvar mirror added to it (not a second
        // [Present]), so the per-frame cross-namespace read runs exactly once.
        const string withPresent = @"[Constants]
global persist $hair = 0

[Present]
post $active = 0

[TextureOverrideBody]
hash = abcd1234
drawindexed = auto
";
        var ini = NamespaceMergeBuilder.TransformSource(withPresent, "Merge\\mod0", "Merge\\Master", 0);

        System.Text.RegularExpressions.Regex.Matches(ini, @"\[Present\]").Count.Should().Be(1, "only one [Present]");
        System.Text.RegularExpressions.Regex.Matches(ini, @"\$mergeswap = \$\\Merge\\Master\\swapvar").Count
            .Should().Be(1, "the cross-namespace mirror is emitted exactly once, in [Present]");
        ini.Should().Contain("post $active = 0", "the source's own [Present] body is preserved");
        ini.Should().Contain("if $mergeswap == 0");
    }

    [Fact]
    public void TransformSource_StripsExistingNamespace()
    {
        var withNs = "namespace = OldThing\n\n" + Source;
        var ini = NamespaceMergeBuilder.TransformSource(withNs, "Merge\\mod0", "Merge\\Master", 0);
        ini.Should().StartWith("namespace = Merge\\mod0");
        ini.Should().NotContain("OldThing");
    }
}
