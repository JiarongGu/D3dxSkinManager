using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Per-profile remote tag labels/aliases ({profile}/remote-tag-labels.json). Regression coverage for the
/// cross-profile leak: aliases edited in one profile MUST NOT appear in another (they used to live on the
/// GLOBAL source config). Seed-once-from-global keeps shipped defaults without leaking edits.
/// </summary>
public class RemoteTagLabelStoreTests : IDisposable
{
    private readonly string _rootA = Path.Combine(Path.GetTempPath(), $"d3dx-lbl-a-{Guid.NewGuid():N}");
    private readonly string _rootB = Path.Combine(Path.GetTempPath(), $"d3dx-lbl-b-{Guid.NewGuid():N}");
    private readonly RemoteTagLabelStore _a;
    private readonly RemoteTagLabelStore _b;

    public RemoteTagLabelStoreTests()
    {
        Directory.CreateDirectory(_rootA);
        Directory.CreateDirectory(_rootB);
        _a = MakeStore(_rootA);
        _b = MakeStore(_rootB);
    }

    private static RemoteTagLabelStore MakeStore(string dir)
    {
        var paths = new Mock<IProfilePathService>();
        paths.Setup(p => p.ProfilePath).Returns(dir);
        return new RemoteTagLabelStore(paths.Object, Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { Directory.Delete(_rootA, recursive: true); } catch { /* best effort */ }
        try { Directory.Delete(_rootB, recursive: true); } catch { /* best effort */ }
    }

    private static Dictionary<string, Dictionary<string, string>> Global() => new()
    {
        ["cn"] = new() { ["Skins"] = "皮肤" },
        ["en"] = new() { ["Skins"] = "Skins" },
    };

    [Fact]
    public void EditInOneProfile_DoesNotLeakToAnother()
    {
        // Profile A renames the "Skins" cn label; profile B must still see the global default.
        _a.SetLangLabels("huihui", "cn", new() { ["Skins"] = "A-皮肤" }, Global());

        _a.GetForSource("huihui", Global())["cn"]["Skins"].Should().Be("A-皮肤");
        _b.GetForSource("huihui", Global())["cn"]["Skins"].Should().Be("皮肤", "profile B is independent of A's edit");
    }

    [Fact]
    public void GetForSource_SeedsOnceFromGlobal_ThenIgnoresLaterGlobalChanges()
    {
        // First read seeds the profile copy from the global defaults...
        _a.GetForSource("huihui", Global())["cn"]["Skins"].Should().Be("皮肤");

        // ...after which a changed global default does NOT override the profile's own copy.
        var changedGlobal = new Dictionary<string, Dictionary<string, string>>
        {
            ["cn"] = new() { ["Skins"] = "GLOBAL-CHANGED" },
        };
        _a.GetForSource("huihui", changedGlobal)["cn"]["Skins"].Should().Be("皮肤");
    }

    [Fact]
    public void SetLangLabels_PreservesOtherLanguagesFromGlobal()
    {
        // Editing only cn on a fresh profile must not drop the en defaults.
        _a.SetLangLabels("huihui", "cn", new() { ["Skins"] = "A-皮肤" }, Global());

        var effective = _a.GetForSource("huihui", Global());
        effective["cn"]["Skins"].Should().Be("A-皮肤");
        effective["en"]["Skins"].Should().Be("Skins", "the untouched language keeps its global default");
    }

    [Fact]
    public void SetLangLabels_DropsBlankPairs()
    {
        _a.SetLangLabels("huihui", "cn", new() { ["Skins"] = "皮肤", ["  "] = "x", ["Empty"] = "  " }, null);
        var cn = _a.GetForSource("huihui", null)["cn"];
        cn.Should().ContainKey("Skins").WhoseValue.Should().Be("皮肤");
        cn.Should().NotContainKey("Empty");
        cn.Should().HaveCount(1);
    }

    [Fact]
    public void GetForSource_NoGlobalNoProfile_ReturnsEmpty()
    {
        _a.GetForSource("unknown", null).Should().BeEmpty();
        File.Exists(Path.Combine(_rootA, "remote-tag-labels.json")).Should().BeFalse("nothing to persist");
    }

    [Fact]
    public void SetLangLabels_PersistsAcrossStoreInstances()
    {
        _a.SetLangLabels("huihui", "cn", new() { ["Skins"] = "A-皮肤" }, Global());
        // A fresh store over the same profile dir reads the persisted override.
        MakeStore(_rootA).GetForSource("huihui", Global())["cn"]["Skins"].Should().Be("A-皮肤");
    }
}
