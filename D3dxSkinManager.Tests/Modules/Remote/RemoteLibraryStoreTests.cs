using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// The redesigned per-profile libraries store (remote-library-redesign.md): a profile owns MANY
/// libraries (site+game+ordered tag rules), switchable; a legacy remote-binding.json auto-upgrades
/// into the first library. Plus the ordered tag-rule matcher used at import time.
/// </summary>
public class RemoteLibraryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"d3dx-lib-{Guid.NewGuid():N}");
    private readonly RemoteLibraryStore _store;

    public RemoteLibraryStoreTests()
    {
        Directory.CreateDirectory(_dir);
        var paths = new Mock<IProfilePathService>();
        paths.Setup(p => p.ProfilePath).Returns(_dir);
        var sources = new Mock<IRemoteSourceStore>();
        sources.Setup(s => s.GetById(It.IsAny<string>())).Returns((string id) => new RemoteSourceConfig
        {
            Id = id,
            Name = id == "huihui" ? "Hui站" : "GameBanana",
            BaseUrl = "https://x",
            Lists = new List<RemoteListConfig> { new() { Id = "2", Name = "绝区零" }, new() { Id = "8552", Name = "Genshin Impact" } },
        });
        _store = new RemoteLibraryStore(paths.Object, sources.Object, Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Add_MultipleLibraries_FirstBecomesActive_SwitchAndRemoveWork()
    {
        var a = _store.Add("huihui", "2", "");
        var b = _store.Add("gamebanana", "8552", "My GB");

        a.Name.Should().Be("Hui站 · 绝区零", "empty name composes Site · Game");
        b.Name.Should().Be("My GB");
        _store.GetState().Libraries.Should().HaveCount(2);
        _store.GetActive()!.Id.Should().Be(a.Id, "the first added library becomes active");

        _store.SetActive(b.Id);
        _store.GetActive()!.Id.Should().Be(b.Id);

        _store.Remove(b.Id).Should().BeTrue();
        _store.GetActive()!.Id.Should().Be(a.Id, "removing the active library falls back to the first remaining");
    }

    [Fact]
    public void Update_EditsNameAndRules_ButIdentityIsFixed()
    {
        var lib = _store.Add("huihui", "2", "Old");
        var edited = new RemoteLibrary
        {
            Id = lib.Id,
            SourceId = "HACKED",       // must be ignored — identity fixed after creation
            ListId = "999",
            Name = "New name",
            TagRules = new List<RemoteTagRule> { new() { Name = "r", Tags = new() { "Skins" }, CategoryId = "cat1" } },
        };

        var saved = _store.Update(edited);

        saved.SourceId.Should().Be("huihui");
        saved.ListId.Should().Be("2");
        saved.Name.Should().Be("New name");
        saved.TagRules.Should().HaveCount(1);
        _store.FindBySourceList("huihui", "2")!.TagRules.Single().CategoryId.Should().Be("cat1");
    }

    [Fact]
    public void LegacyBinding_AutoUpgradesIntoTheFirstLibrary()
    {
        File.WriteAllText(Path.Combine(_dir, "remote-binding.json"),
            """{ "sourceId": "huihui", "listId": "2", "boundAtUtc": "2026-07-05T00:00:00Z" }""");

        var state = _store.GetState();

        state.Libraries.Should().ContainSingle();
        state.Libraries[0].SourceId.Should().Be("huihui");
        state.Libraries[0].ListId.Should().Be("2");
        state.ActiveLibraryId.Should().Be(state.Libraries[0].Id);
    }

    [Fact]
    public void MatchTagRules_OrderedFirstMatchWins_AllTagsMustMatch_ElseNull()
    {
        var rules = new List<RemoteTagRule>
        {
            new() { Name = "hu tao skins", Tags = new() { "Skins", "Hu Tao" }, CategoryId = "hutao" }, // multi-tag, most specific first
            new() { Name = "any skins", Tags = new() { "skins" }, CategoryId = "skins" },              // case-insensitive
            new() { Name = "broken", Tags = new(), CategoryId = "never" },                             // empty rule skipped
        };

        RemoteImportService.MatchTagRules(rules, new[] { "Skins", "Hu Tao" }).Should().Be("hutao");
        RemoteImportService.MatchTagRules(rules, new[] { "SKINS" }).Should().Be("skins");
        RemoteImportService.MatchTagRules(rules, new[] { "UI" }).Should().BeNull("no match = uncategorized");
        RemoteImportService.MatchTagRules(new List<RemoteTagRule>(), new[] { "Skins" }).Should().BeNull();
    }
}
