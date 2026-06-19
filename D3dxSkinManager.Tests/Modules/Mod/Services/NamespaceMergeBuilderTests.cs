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
        // Cross-namespace read of the master's GLOBAL swapvar uses the \global\ prefix (otherwise the
        // gate never resolves and both variants render).
        ini.Should().Contain("if $\\global\\Merge\\Master\\swapvar == 1");
        ini.Should().Contain("endif");
        // Active variant flags itself on-screen so the cycle key only fires for this character.
        ini.Should().Contain("$\\global\\Merge\\Master\\active = 1");

        // hash is a bind-time declaration → before the gate; the draw commands → inside it.
        var hashAt = ini.IndexOf("hash = abcd1234", StringComparison.Ordinal);
        var ifAt = ini.IndexOf("if $\\global\\Merge\\Master\\swapvar == 1", StringComparison.Ordinal);
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
        var master = NamespaceMergeBuilder.BuildMaster("Merge\\Master", "v", 3, activeOnly: true);

        master.Should().StartWith("namespace = Merge\\Master");
        master.Should().Contain("global persist $swapvar = 0");
        master.Should().Contain("[KeySwap]").And.Contain("key = v").And.Contain("type = cycle");
        master.Should().Contain("$swapvar = 0,1,2");          // one per variant
        master.Should().Contain("condition = $active == 1").And.Contain("post $active = 0");
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
