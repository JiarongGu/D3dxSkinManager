using System;
using System.IO;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Mod;

namespace D3dxSkinManager.Tests.Modules.Mod;

/// <summary>ModConventions.IsIgnoredNonModFolder — the ".claude saved as a mod" guard (2026-07-13):
/// a dot-prefixed folder without any *.ini is NOT a mod and must be skipped by folder→mod enumeration
/// (else it is treated as a mod id, disabled to DISABLED.claude, and its load fails).</summary>
public class ModConventionsTests : IDisposable
{
    private readonly string _root;

    public ModConventionsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "d3dx-conv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private string MakeDir(string name, bool withIni)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        if (withIni) File.WriteAllText(Path.Combine(dir, "mod.ini"), "[Constants]");
        return dir;
    }

    [Fact]
    public void DotFolder_WithoutIni_IsIgnored()
    {
        ModConventions.IsIgnoredNonModFolder(MakeDir(".claude", withIni: false)).Should().BeTrue();
    }

    [Fact]
    public void DotFolder_WithIni_IsNotIgnored()
    {
        // 3DMigoto would load it — a separate concern, so we do NOT skip it.
        ModConventions.IsIgnoredNonModFolder(MakeDir(".weirdmod", withIni: true)).Should().BeFalse();
    }

    [Fact]
    public void DotFolder_WithNestedIni_IsNotIgnored()
    {
        // The *.ini can live in a subfolder (merged mods) — recursion must find it.
        var dir = MakeDir(".nested", withIni: false);
        var sub = Path.Combine(dir, "inner");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "merged.ini"), "[Constants]");
        ModConventions.IsIgnoredNonModFolder(dir).Should().BeFalse();
    }

    [Fact]
    public void NormalFolder_IsNeverIgnored()
    {
        // A real mod id (no dot) is kept whether or not it currently has an .ini staged.
        ModConventions.IsIgnoredNonModFolder(MakeDir("350F7F116CC7432EB8946CF44A2D43EA", withIni: true)).Should().BeFalse();
        ModConventions.IsIgnoredNonModFolder(MakeDir("staging-no-ini", withIni: false)).Should().BeFalse();
    }
}
