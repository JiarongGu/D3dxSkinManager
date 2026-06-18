using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Structural tests for the GIMI-port merge engine: dedup overrides by hash, branch command lists on
/// $swapvar, suffix binds by group, emit the swapvar constants + KeySwap. (In-game swap behaviour is
/// verified separately with real same-character mods.)
/// </summary>
public class MergeIniBuilderTests
{
    private const string ModA = @"[TextureOverrideBody]
hash = abcd1234
vb0 = ResourceBodyA

[ResourceBodyA]
filename = BodyA.buf
";

    private const string ModB = @"[TextureOverrideBody]
hash = abcd1234
vb0 = ResourceBodyB

[ResourceBodyB]
filename = BodyB.buf
";

    private static string Merge() => MergeIniBuilder.Build(new List<MergeSourceIni>
    {
        new() { Group = 0, IniText = ModA },
        new() { Group = 1, IniText = ModB },
    }, key: "v", activeOnly: true);

    [Fact]
    public void Build_EmitsSwapvarConstantsAndKeySwap()
    {
        var ini = Merge();
        ini.Should().Contain("[Constants]").And.Contain("global persist $swapvar = 0");
        ini.Should().Contain("[KeySwap]").And.Contain("key = v").And.Contain("type = cycle");
        ini.Should().Contain("$swapvar = 0,1");       // one entry per source
        ini.Should().Contain("condition = $active == 1").And.Contain("post $active = 0");
    }

    [Fact]
    public void Build_DedupsOverride_ByHash()
    {
        var ini = Merge();
        // Both mods share hash abcd1234 → exactly ONE override, routed to a command list.
        System.Text.RegularExpressions.Regex.Matches(ini, @"\[TextureOverrideBody\]").Count.Should().Be(1);
        ini.Should().Contain("run = CommandListBody");
    }

    [Fact]
    public void Build_BranchesCommandListOnSwapvar_AndSuffixesBindsByGroup()
    {
        var ini = Merge();
        ini.Should().Contain("[CommandListBody]");
        ini.Should().Contain("if $swapvar == 0");
        ini.Should().Contain("else if $swapvar == 1");
        ini.Should().Contain("endif");
        // The vb bind of each variant is suffixed by its group so they don't collide.
        ini.Should().Contain("vb0 = ResourceBodyA.0");
        ini.Should().Contain("vb0 = ResourceBodyB.1");
    }

    [Fact]
    public void Build_EmitsGroupSuffixedResources()
    {
        var ini = Merge();
        ini.Should().Contain("[ResourceBodyA.0]").And.Contain("filename = BodyA.buf");
        ini.Should().Contain("[ResourceBodyB.1]").And.Contain("filename = BodyB.buf");
    }

    [Fact]
    public void Build_OrdersBlocks_ConstantsThenOverridesThenCommandsThenResources()
    {
        var ini = Merge();
        var c = ini.IndexOf("[Constants]", System.StringComparison.Ordinal);
        var o = ini.IndexOf("[TextureOverrideBody]", System.StringComparison.Ordinal);
        var cmd = ini.IndexOf("[CommandListBody]", System.StringComparison.Ordinal);
        var r = ini.IndexOf("[ResourceBodyA.0]", System.StringComparison.Ordinal);
        c.Should().BeLessThan(o);
        o.Should().BeLessThan(cmd);
        cmd.Should().BeLessThan(r);
    }
}
