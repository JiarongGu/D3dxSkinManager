using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod;

/// <summary>
/// The d3dx_user.ini persist-store read/merge behind preset var-state capture/restore. All fixtures are
/// SYNTHETIC (fake mod ids/folder names) — mirrors 3DMigoto's real format (verified against a live ZZMI
/// install) without embedding any real path/content.
/// </summary>
public class D3dmigotoUserConfigServiceTests : IDisposable
{
    private readonly string _workDir;
    private readonly D3dmigotoUserConfigService _service;

    public D3dmigotoUserConfigServiceTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "d3dx-user-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
        var paths = new Mock<IProfilePathService>();
        paths.Setup(p => p.WorkDirectory).Returns(_workDir);
        _service = new D3dmigotoUserConfigService(paths.Object, Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, true); } catch { }
    }

    private string IniPath => Path.Combine(_workDir, "d3dx_user.ini");

    // The verified real shape: a header, [Constants], then $\<ns>\var = value lines whose namespace embeds
    // the deployed mod id (mods\<modId>\...). Two mods + the importer's own var + a comment.
    private const string Sample =
        "; AUTOMATICALLY GENERATED FILE - DO NOT EDIT\n" +
        ";\n" +
        "[Constants]\n" +
        "$\\zzmiv1\\first_run = 0\n" +
        "$\\mods\\aaa111\\char one\\one.ini\\swapkey0 = 1\n" +
        "$\\mods\\aaa111\\char one\\one.ini\\swapkey1 = 2\n" +
        "$\\mods\\bbb222\\char two\\two.ini\\hair = 0\n";

    private void WriteSample() => File.WriteAllText(IniPath, Sample);

    [Fact]
    public void Capture_ReturnsOnlyTheGivenMods_Vars_SkippingOthersAndComments()
    {
        WriteSample();
        var lines = _service.CaptureVarLines(new[] { "aaa111" });

        lines.Should().BeEquivalentTo(new[]
        {
            "$\\mods\\aaa111\\char one\\one.ini\\swapkey0 = 1",
            "$\\mods\\aaa111\\char one\\one.ini\\swapkey1 = 2",
        }, "only aaa111's vars — not bbb222, not the importer's $\\zzmiv1, not comments/headers");
    }

    [Fact]
    public void Capture_ExcludesUnmanagedMods_NotInTheManagedSet()
    {
        // A preset captures state ONLY for the app's MANAGED (loaded) mods. An anonymous/unmanaged mod
        // deployed straight into the Mods folder has vars in d3dx_user.ini too, but we can't redeploy it
        // from a managed archive → its state must NEVER be captured (user directive 2026-07-13). Keying
        // capture on the managed id set (here: aaa111) excludes bbb222 + the importer's own $\zzmiv1.
        WriteSample();
        _service.CaptureVarLines(new[] { "aaa111" })
            .Should().OnlyContain(l => l.Contains("aaa111"), "unmanaged mods + importer vars excluded");
    }

    [Fact]
    public void Capture_IsCaseInsensitiveOnTheModId()
    {
        WriteSample();
        // 3DMigoto lowercases namespaces; the app's mod ids are uppercase GUIDs — match must ignore case.
        _service.CaptureVarLines(new[] { "AAA111" }).Should().HaveCount(2);
    }

    [Fact]
    public void Capture_NoFile_ReturnsEmpty()
    {
        _service.CaptureVarLines(new[] { "aaa111" }).Should().BeEmpty("an internal profile has no d3dx_user.ini");
    }

    [Fact]
    public void Apply_ReplacesMatchingValues_AndPreservesHeaderAndOtherVars()
    {
        WriteSample();
        // A captured snapshot with DIFFERENT values for aaa111.
        var snapshot = new[]
        {
            "$\\mods\\aaa111\\char one\\one.ini\\swapkey0 = 9",
            "$\\mods\\aaa111\\char one\\one.ini\\swapkey1 = 8",
        };

        _service.ApplyVarLines(snapshot, new[] { "aaa111" }).Should().BeGreaterThan(0);

        var result = File.ReadAllLines(IniPath);
        result[0].Should().StartWith("; AUTOMATICALLY GENERATED", "header preserved");
        result.Should().Contain("$\\mods\\aaa111\\char one\\one.ini\\swapkey0 = 9", "value overwritten");
        result.Should().Contain("$\\mods\\aaa111\\char one\\one.ini\\swapkey1 = 8");
        result.Should().Contain("$\\zzmiv1\\first_run = 0", "the importer's own var is untouched");
        result.Should().Contain("$\\mods\\bbb222\\char two\\two.ini\\hair = 0", "another mod's var is untouched");
        // No duplicate of the replaced key.
        result.Count(l => l.TrimStart().StartsWith("$\\mods\\aaa111\\char one\\one.ini\\swapkey0")).Should().Be(1);
    }

    [Fact]
    public void Apply_AppendsVarsNotAlreadyPresent_IntoConstants()
    {
        WriteSample();
        var snapshot = new[] { "$\\mods\\ccc333\\char three\\three.ini\\swapkey0 = 5" }; // a mod not in the file yet

        _service.ApplyVarLines(snapshot, new[] { "ccc333" }).Should().BeGreaterThan(0);

        File.ReadAllLines(IniPath).Should().Contain("$\\mods\\ccc333\\char three\\three.ini\\swapkey0 = 5");
    }

    [Fact]
    public void Apply_CreatesTheFile_WhenAbsent()
    {
        File.Exists(IniPath).Should().BeFalse();
        var snapshot = new[] { "$\\mods\\aaa111\\char one\\one.ini\\swapkey0 = 3" };

        _service.ApplyVarLines(snapshot, new[] { "aaa111" }).Should().BeGreaterThan(0);

        var result = File.ReadAllLines(IniPath);
        result.Should().Contain("[Constants]");
        result.Should().Contain("$\\mods\\aaa111\\char one\\one.ini\\swapkey0 = 3");
    }

    [Fact]
    public void Apply_EmptySnapshot_IsNoOp()
    {
        _service.ApplyVarLines(Array.Empty<string>(), Array.Empty<string>()).Should().Be(0);
        File.Exists(IniPath).Should().BeFalse();
    }

    [Fact]
    public void CaptureThenApply_RoundTrips_TheModsState()
    {
        // Capture aaa111 at state {1,2}; the user then changes it in-game to {0,0}; applying the preset
        // restores {1,2} while leaving bbb222 alone.
        WriteSample();
        var snapshot = _service.CaptureVarLines(new[] { "aaa111" });

        File.WriteAllText(IniPath, Sample
            .Replace("swapkey0 = 1", "swapkey0 = 0")
            .Replace("swapkey1 = 2", "swapkey1 = 0"));

        _service.ApplyVarLines(snapshot, new[] { "aaa111" }).Should().BeGreaterThan(0);

        var result = File.ReadAllLines(IniPath);
        result.Should().Contain("$\\mods\\aaa111\\char one\\one.ini\\swapkey0 = 1");
        result.Should().Contain("$\\mods\\aaa111\\char one\\one.ini\\swapkey1 = 2");
        result.Should().Contain("$\\mods\\bbb222\\char two\\two.ini\\hair = 0");
    }

    [Fact]
    public void Apply_DriftedInnerPath_RewritesTheCurrentNamespaceLine_NotAppendsAGhost()
    {
        // The mod was re-fixed/merged/renamed since capture: 3DMigoto now persists aaa111's swapkey0 under a
        // DIFFERENT inner path (new-char\new.ini). The captured LHS points at the OLD path. Matching by full
        // LHS would append a ghost line under the stale path that 3DMigoto never reads (the reported "some
        // mod state does not loaded" bug). Instead we must overwrite the CURRENT line's value, keeping its
        // namespace, matched by mod id + var name.
        File.WriteAllText(IniPath,
            "; AUTOMATICALLY GENERATED FILE - DO NOT EDIT\n" +
            "[Constants]\n" +
            "$\\mods\\aaa111\\new-char\\new.ini\\swapkey0 = 0\n" +   // current namespace (drifted from capture)
            "$\\mods\\bbb222\\char two\\two.ini\\hair = 0\n");

        // Preset captured under the OLD inner path.
        var snapshot = new[] { "$\\mods\\aaa111\\char one\\one.ini\\swapkey0 = 7" };

        _service.ApplyVarLines(snapshot, new[] { "aaa111" }).Should().BeGreaterThan(0);

        var result = File.ReadAllLines(IniPath);
        result.Should().Contain("$\\mods\\aaa111\\new-char\\new.ini\\swapkey0 = 7",
            "the captured value is written onto the CURRENT namespace line so 3DMigoto binds it");
        result.Should().NotContain(l => l.Contains("char one\\one.ini\\swapkey0"),
            "no ghost line under the stale captured path");
        result.Should().Contain("$\\mods\\bbb222\\char two\\two.ini\\hair = 0", "other mods untouched");
    }

    [Fact]
    public void Apply_AmbiguousDrift_AppendsVerbatim_RatherThanGuessingWhichLine()
    {
        // If the current file has TWO lines for the same mod+var (e.g. two .ini files each declaring swapkey0),
        // a drift rewrite can't know which one to update — so it must NOT guess. Fall back to appending the
        // captured line verbatim (no worse than before, never a wrong rewrite).
        File.WriteAllText(IniPath,
            "; AUTOMATICALLY GENERATED FILE - DO NOT EDIT\n" +
            "[Constants]\n" +
            "$\\mods\\aaa111\\a\\one.ini\\swapkey0 = 0\n" +
            "$\\mods\\aaa111\\b\\two.ini\\swapkey0 = 0\n");

        var snapshot = new[] { "$\\mods\\aaa111\\c\\old.ini\\swapkey0 = 9" };

        _service.ApplyVarLines(snapshot, new[] { "aaa111" }).Should().BeGreaterThan(0);

        var result = File.ReadAllLines(IniPath);
        result.Should().Contain("$\\mods\\aaa111\\a\\one.ini\\swapkey0 = 0", "ambiguous existing lines left alone");
        result.Should().Contain("$\\mods\\aaa111\\b\\two.ini\\swapkey0 = 0");
        result.Should().Contain("$\\mods\\aaa111\\c\\old.ini\\swapkey0 = 9", "captured line appended verbatim");
    }

    [Fact]
    public void Apply_ReturnsTheNumberOfVarsWritten()
    {
        WriteSample();
        var snapshot = new[]
        {
            "$\\mods\\aaa111\\char one\\one.ini\\swapkey0 = 9", // exact match
            "$\\mods\\ccc333\\x\\x.ini\\newvar = 4",            // appended
        };

        _service.ApplyVarLines(snapshot, new[] { "aaa111", "ccc333" }).Should().Be(2);
    }
}
