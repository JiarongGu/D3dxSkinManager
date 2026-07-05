using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Synced-index behaviour: crawl merge (stable ids, firstSeen preserved, site order), local
/// search/paging, date hints, and the remote-import identity metadata round-trip.
/// Browse service faked; temp cache dir; real ProcessRegistry (in-memory).
/// </summary>
public class RemoteIndexServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly Mock<IRemoteBrowseService> _browse = new();
    private readonly RemoteSourceConfig _config = RemoteBrowseServiceTests.LoadHuihuiSeed();
    private readonly RemoteIndexService _service;

    public RemoteIndexServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-remote-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.RemoteSourcesDirectory).Returns(_dir);
        paths.Setup(p => p.BaseDataPath).Returns(_dir);
        var store = new Mock<IRemoteSourceStore>();
        store.Setup(s => s.GetById("huihui")).Returns(_config);
        var registry = new ProcessRegistry(Mock.Of<D3dxSkinManager.Modules.Core.Event.IEventBus>(), Mock.Of<ILogHelper>(), paths.Object);
        _service = new RemoteIndexService(store.Object, _browse.Object, paths.Object, registry, Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static RemoteModCard Card(int id, string title, string date = "20260621") => new()
    {
        Title = title,
        DetailUrl = $"https://huihui168.org/?news_12/{id}.html",
        ImageUrl = $"https://huihui168.org/static/upload/image/{date}/img{id}.jpg",
    };

    private void SetupPages(params List<RemoteModCard>[] pages)
    {
        for (var i = 0; i < pages.Length; i++)
        {
            var page = i + 1;
            _browse.Setup(b => b.BrowseAsync("huihui", "2", page, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RemoteBrowseResult { Page = page, Cards = pages[page - 1], TotalPages = pages.Length });
        }
    }

    private async Task SyncAndWaitAsync()
    {
        var before = _service.Query("huihui", "2", null, 1, 1).Info.SyncedAtUtc;
        var procId = _service.StartSync("huihui", "2");
        procId.Should().NotBeEmpty();
        // The sync is fire-and-forget — wait for the cache file to carry a NEWER syncedAt stamp.
        for (var i = 0; i < 100; i++)
        {
            var info = _service.Query("huihui", "2", null, 1, 1).Info;
            if (info.SyncedAtUtc != null && info.SyncedAtUtc != before) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("sync did not finish");
    }

    [Fact]
    public async Task Sync_BuildsTheIndex_WithStableIds_DateHints_AndSiteOrder()
    {
        SetupPages(
            new List<RemoteModCard> { Card(10, "反虚化3.0"), Card(11, "星见雅-虚狩") },
            new List<RemoteModCard> { Card(12, "简 黑丝卧底", date: "20250526") });

        await SyncAndWaitAsync();
        var result = _service.Query("huihui", "2", null, 1, 50);

        result.Info.EntryCount.Should().Be(3);
        result.Info.TotalPages.Should().Be(2);
        result.Entries.Select(e => e.Id).Should().ContainInOrder("10", "11", "12");
        result.Entries[0].DateHint.Should().Be("2026-06-21");
        result.Entries[2].DateHint.Should().Be("2025-05-26");
    }

    [Fact]
    public async Task Resync_PreservesFirstSeen_AndUpdatesOrder()
    {
        SetupPages(new List<RemoteModCard> { Card(10, "Old title") });
        await SyncAndWaitAsync();
        var firstSeen = _service.Query("huihui", "2", null, 1, 10).Entries.Single().FirstSeenUtc;

        // Next sync: a NEW mod appears first (site recency order), the old one renamed.
        SetupPages(new List<RemoteModCard> { Card(99, "Brand new"), Card(10, "New title") });
        await SyncAndWaitAsync();

        var entries = _service.Query("huihui", "2", null, 1, 10).Entries;
        entries.Select(e => e.Id).Should().ContainInOrder("99", "10");
        var old = entries.Single(e => e.Id == "10");
        old.Title.Should().Be("New title");
        old.FirstSeenUtc.Should().Be(firstSeen, "firstSeen survives re-syncs");
    }

    [Fact]
    public async Task Query_FiltersByTitleTerms_AndPages()
    {
        SetupPages(new List<RemoteModCard>
        {
            Card(1, "薇薇安 恶魔"), Card(2, "薇薇安 泳装"), Card(3, "星见雅 泳装"),
        });
        await SyncAndWaitAsync();

        _service.Query("huihui", "2", "薇薇安", 1, 50).Total.Should().Be(2);
        _service.Query("huihui", "2", "薇薇安 泳装", 1, 50).Entries.Single().Id.Should().Be("2");
        var paged = _service.Query("huihui", "2", null, 2, 2);
        paged.Total.Should().Be(3);
        paged.Entries.Single().Id.Should().Be("3");
    }

    [Fact]
    public void Query_NeverSynced_ReturnsEmptyInfo()
    {
        var result = _service.Query("huihui", "2", null, 1, 50);
        result.Info.SyncedAtUtc.Should().BeNull();
        result.Info.EntryCount.Should().Be(0);
        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public void ExtractEntryId_UsesThePattern_FallsBackToUrl()
    {
        RemoteIndexService.ExtractEntryId(_config, "https://huihui168.org/?news_12/2845.html").Should().Be("2845");
        RemoteIndexService.ExtractEntryId(new RemoteSourceConfig(), "https://x/y").Should().Be("https://x/y");
    }

    [Fact]
    public void RemoteMetadata_RoundTrips_AndPreservesOtherFields()
    {
        var merged = RemoteImportService.WriteRemoteMetadata(
            """{"keybindingOrder":["9","0"]}""", "huihui", "https://huihui168.org/?news_12/2845.html", "abc123");

        merged.Should().Contain("keybindingOrder", "existing metadata fields survive");
        RemoteImportService.ReadRemoteDetailUrl(merged).Should().Be("https://huihui168.org/?news_12/2845.html");
        RemoteImportService.ReadRemoteDetailUrl("{broken").Should().BeNull();
        RemoteImportService.ReadRemoteDetailUrl(null).Should().BeNull();
    }
}
