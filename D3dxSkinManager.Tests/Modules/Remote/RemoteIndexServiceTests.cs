using System;
using System.Collections.Generic;
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
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Synced-index v2 (per-profile SQLite): full-vs-incremental sync semantics against the REAL
/// repository on an in-memory profile DB (schema created below, mirrors migration 202607050002).
/// Browse service faked; real ProcessRegistry.
/// </summary>
public class RemoteIndexServiceTests : InMemoryDatabaseTestBase
{
    private readonly Mock<IRemoteBrowseService> _browse = new();
    private readonly RemoteSourceConfig _config = RemoteBrowseServiceTests.LoadHuihuiSeed();
    private readonly RemoteIndexRepository _repository;
    private readonly RemoteIndexService _service;

    public RemoteIndexServiceTests()
    {
        CreateRemoteIndexSchema();
        _repository = new RemoteIndexRepository(MockProfilePathService.Object);
        var store = new Mock<IRemoteSourceStore>();
        store.Setup(s => s.GetById("huihui")).Returns(_config);
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.BaseDataPath).Returns(System.IO.Path.GetTempPath());
        var registry = new ProcessRegistry(Mock.Of<D3dxSkinManager.Modules.Core.Event.IEventBus>(), Mock.Of<ILogHelper>(), paths.Object);
        _service = new RemoteIndexService(store.Object, _browse.Object, _repository, registry, Mock.Of<ILogHelper>());
    }

    private void CreateRemoteIndexSchema()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE RemoteIndexEntries (
                SourceId TEXT NOT NULL, ListId TEXT NOT NULL, EntryId TEXT NOT NULL,
                Title TEXT NOT NULL DEFAULT '', DetailUrl TEXT NOT NULL, ImageUrl TEXT NOT NULL DEFAULT '',
                Tags TEXT, DateHint TEXT, Generation INTEGER NOT NULL DEFAULT 0, SortKey INTEGER NOT NULL DEFAULT 0,
                FirstSeenUtc TEXT NOT NULL, LastSeenUtc TEXT NOT NULL, RemovedUtc TEXT,
                PRIMARY KEY (SourceId, ListId, EntryId));
            CREATE TABLE RemoteIndexMeta (
                SourceId TEXT NOT NULL, ListId TEXT NOT NULL, SyncedAtUtc TEXT,
                TotalPages INTEGER NOT NULL DEFAULT 0, Generation INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (SourceId, ListId));";
        cmd.ExecuteNonQuery();
    }

    private static RemoteModCard Card(int id, string title, string date = "20260621", params string[] tags) => new()
    {
        Title = title,
        DetailUrl = $"https://huihui168.org/?news_12/{id}.html",
        ImageUrl = $"https://huihui168.org/static/upload/image/{date}/img{id}.jpg",
        Tags = tags.ToList(),
    };

    private void SetupPages(params List<RemoteModCard>[] pages)
    {
        _browse.Reset();
        for (var i = 0; i < pages.Length; i++)
        {
            var page = i + 1;
            _browse.Setup(b => b.BrowseAsync("huihui", "2", page, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RemoteBrowseResult { Page = page, Cards = pages[page - 1], TotalPages = pages.Length });
        }
    }

    private async Task SyncAndWaitAsync(bool full = false)
    {
        var before = (await _repository.GetMetaAsync("huihui", "2"))?.SyncedAtUtc;
        _service.StartSync("huihui", "2", full).Should().NotBeEmpty();
        for (var i = 0; i < 100; i++)
        {
            var meta = await _repository.GetMetaAsync("huihui", "2");
            if (meta?.SyncedAtUtc != null && meta.SyncedAtUtc != before) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("sync did not finish");
    }

    [Fact]
    public async Task FullSync_BuildsIndex_WithStableIds_DateHints_SiteOrder()
    {
        SetupPages(
            new List<RemoteModCard> { Card(10, "反虚化3.0"), Card(11, "星见雅-虚狩") },
            new List<RemoteModCard> { Card(12, "简 黑丝卧底", date: "20250526") });

        await SyncAndWaitAsync();
        var result = await _service.QueryAsync("huihui", "2", null, 1, 50);

        result.Info.EntryCount.Should().Be(3);
        result.Info.TotalPages.Should().Be(2);
        result.Entries.Select(e => e.Id).Should().ContainInOrder("10", "11", "12");
        result.Entries[0].DateHint.Should().Be("2026-06-21");
        result.Entries[2].DateHint.Should().Be("2025-05-26");
    }

    [Fact]
    public async Task UpdateSync_StopsAtTheFirstFullyKnownPage()
    {
        SetupPages(
            new List<RemoteModCard> { Card(1, "A"), Card(2, "B") },
            new List<RemoteModCard> { Card(3, "C"), Card(4, "D") },
            new List<RemoteModCard> { Card(5, "E") });
        await SyncAndWaitAsync(); // full: 3 pages

        // Next sync: one NEW mod pushed everything down one slot — page 1 has 1 new entry, page 2
        // has none new → the crawl must stop after page 2 and never request page 3.
        SetupPages(
            new List<RemoteModCard> { Card(99, "NEW"), Card(1, "A") },
            new List<RemoteModCard> { Card(2, "B"), Card(3, "C") },
            new List<RemoteModCard> { Card(4, "D"), Card(5, "E") });
        await SyncAndWaitAsync();

        // SetupPages() resets the mock between syncs, so recorded invocations here are the UPDATE
        // sync's only — it must have stopped after page 2 and never requested page 3.
        _browse.Verify(b => b.BrowseAsync("huihui", "2", 3, It.IsAny<CancellationToken>()), Times.Never,
            "the update sync stops at the first fully-known page");
        var result = await _service.QueryAsync("huihui", "2", null, 1, 50);
        result.Info.EntryCount.Should().Be(6);
        // Recency order: the recrawled pages (new generation) come first in site order, the
        // un-recrawled tail keeps its old relative order after them.
        result.Entries.Select(e => e.Id).Should().ContainInOrder("99", "1", "2", "3", "4", "5");
    }

    [Fact]
    public async Task UpdateSync_PreservesFirstSeen_AndRefreshesTitles()
    {
        SetupPages(new List<RemoteModCard> { Card(10, "Old title") });
        await SyncAndWaitAsync();
        var firstSeen = (await _service.QueryAsync("huihui", "2", null, 1, 10)).Entries.Single().FirstSeenUtc;

        SetupPages(new List<RemoteModCard> { Card(99, "Brand new"), Card(10, "New title") });
        await SyncAndWaitAsync();

        var entries = (await _service.QueryAsync("huihui", "2", null, 1, 10)).Entries;
        entries.Select(e => e.Id).Should().ContainInOrder("99", "10");
        var old = entries.Single(e => e.Id == "10");
        old.Title.Should().Be("New title");
        old.FirstSeenUtc.Should().BeCloseTo(firstSeen, TimeSpan.FromSeconds(1), "firstSeen survives re-syncs");
    }

    [Fact]
    public async Task Query_FiltersByTitleTerms_SortsByDate_AndPages()
    {
        SetupPages(new List<RemoteModCard>
        {
            Card(1, "薇薇安 恶魔", date: "20260101"), Card(2, "薇薇安 泳装", date: "20260301"), Card(3, "星见雅 泳装", date: "20260201"),
        });
        await SyncAndWaitAsync();

        (await _service.QueryAsync("huihui", "2", "薇薇安", 1, 50)).Total.Should().Be(2);
        (await _service.QueryAsync("huihui", "2", "薇薇安 泳装", 1, 50)).Entries.Single().Id.Should().Be("2");
        var paged = await _service.QueryAsync("huihui", "2", null, 2, 2);
        paged.Total.Should().Be(3);
        paged.Entries.Single().Id.Should().Be("3");
        var byDate = await _service.QueryAsync("huihui", "2", null, 1, 50, sort: "date");
        byDate.Entries.Select(e => e.Id).Should().ContainInOrder("2", "3", "1");
    }

    [Fact]
    public async Task FullReindex_PrunesEntriesNoLongerOnSite_ButUpdateDoesNot()
    {
        SetupPages(new List<RemoteModCard> { Card(1, "A"), Card(2, "B"), Card(3, "C") });
        await SyncAndWaitAsync(); // first sync is full

        // The site dropped mod 2. An UPDATE must NOT prune (it stops early and can't tell removed
        // from below-the-stop-page); the entry stays visible.
        SetupPages(new List<RemoteModCard> { Card(1, "A"), Card(3, "C") });
        await SyncAndWaitAsync();
        (await _service.QueryAsync("huihui", "2", null, 1, 50)).Entries.Select(e => e.Id)
            .Should().Contain("2", "an incremental update never prunes");

        // A FULL reindex sees every page, so mod 2 (still on the old generation) is soft-deleted.
        await SyncAndWaitAsync(full: true);
        var result = await _service.QueryAsync("huihui", "2", null, 1, 50);
        result.Entries.Select(e => e.Id).Should().BeEquivalentTo(new[] { "1", "3" });
        result.Info.EntryCount.Should().Be(2, "removed entries are excluded from the count");
    }

    [Fact]
    public async Task FullReindex_UnremovesAnEntryThatReappears()
    {
        SetupPages(new List<RemoteModCard> { Card(1, "A"), Card(2, "B") });
        await SyncAndWaitAsync();
        SetupPages(new List<RemoteModCard> { Card(1, "A") });
        await SyncAndWaitAsync(full: true); // prunes mod 2
        (await _service.QueryAsync("huihui", "2", null, 1, 50)).Info.EntryCount.Should().Be(1);

        // Mod 2 comes back → the re-seen upsert clears RemovedUtc, so it's visible again.
        SetupPages(new List<RemoteModCard> { Card(1, "A"), Card(2, "B") });
        await SyncAndWaitAsync(full: true);
        (await _service.QueryAsync("huihui", "2", null, 1, 50)).Entries.Select(e => e.Id)
            .Should().BeEquivalentTo(new[] { "1", "2" });
    }

    [Fact]
    public async Task Query_FiltersByTag_AndListsDistinctTags()
    {
        SetupPages(new List<RemoteModCard>
        {
            Card(1, "A", tags: new[] { "Skins", "Hu Tao" }), Card(2, "B", tags: new[] { "Skins" }), Card(3, "C", tags: new[] { "Other/Misc" }),
        });
        await SyncAndWaitAsync();

        (await _service.QueryAsync("huihui", "2", null, 1, 50, tag: "Skins")).Total.Should().Be(2);
        (await _service.QueryAsync("huihui", "2", null, 1, 50, tag: "Hu Tao")).Entries.Single().Id.Should().Be("1");
        (await _service.QueryAsync("huihui", "2", null, 1, 50, tag: "Other/Misc")).Entries.Single().Id.Should().Be("3");
        (await _service.QueryAsync("huihui", "2", null, 1, 50)).Total.Should().Be(3, "no filter = all");

        // Entries carry their tag lists on the wire.
        var all = await _service.QueryAsync("huihui", "2", null, 1, 50);
        all.Entries.Single(e => e.Id == "1").Tags.Should().BeEquivalentTo("Skins", "Hu Tao");

        var tags = await _service.GetTagsAsync("huihui", "2");
        tags.Should().HaveCount(3);
        tags[0].Name.Should().Be("Skins");   // most frequent first
        tags[0].Count.Should().Be(2);
    }

    [Fact]
    public async Task Query_NeverSynced_ReturnsEmptyInfo()
    {
        var result = await _service.QueryAsync("huihui", "2", null, 1, 50);
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
    public void RemoteMetadata_RoundTrips_StandardizedIdentity_AndPreservesOtherFields()
    {
        var merged = RemoteImportService.WriteRemoteMetadata(
            """{"keybindingOrder":["9","0"]}""", "huihui", "2", "2845", "https://huihui168.org/?news_12/2845.html", "abc123");

        merged.Should().Contain("keybindingOrder", "existing metadata fields survive");
        var remote = RemoteImportService.ReadRemote(merged);
        remote.Should().NotBeNull();
        remote!.Value.Key.Should().Be(RemoteImportService.ImportedKey("huihui", "2", "2845"),
            "the durable identity is sourceId|listId|entryId (detailUrl breaks when a site moves hosts)");
        remote.Value.DetailUrl.Should().Be("https://huihui168.org/?news_12/2845.html");
        RemoteImportService.ReadRemote("{broken").Should().BeNull();
        RemoteImportService.ReadRemote(null).Should().BeNull();

        // A LEGACY import (no listId/entryId) still resolves its detailUrl and yields no key.
        var legacy = RemoteImportService.WriteRemoteMetadata(null, "huihui", null, null, "https://x/y", "sha");
        RemoteImportService.ReadRemote(legacy)!.Value.Key.Should().BeNull();
        RemoteImportService.ReadRemoteDetailUrl(legacy).Should().Be("https://x/y");
    }
}
