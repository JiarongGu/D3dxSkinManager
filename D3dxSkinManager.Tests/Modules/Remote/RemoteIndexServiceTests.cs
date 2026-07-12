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
    private readonly Mock<IRemoteImportService> _import = new();
    private readonly RemoteSourceConfig _config = RemoteBrowseServiceTests.LoadHuihuiSeed();
    private readonly RemoteIndexRepository _repository;
    private readonly RemoteIndexService _service;
    private readonly Mock<IRemoteTagLabelStore> _labels = new();

    public RemoteIndexServiceTests()
    {
        CreateRemoteIndexSchema();
        _repository = new RemoteIndexRepository(MockProfilePathService.Object);
        var store = new Mock<IRemoteSourceStore>();
        store.Setup(s => s.GetById("huihui")).Returns(_config);
        var registry = new ProcessRegistry(Mock.Of<D3dxSkinManager.Modules.Core.Event.IEventBus>(), Mock.Of<ILogHelper>());
        _labels.Setup(l => l.GetForSource(It.IsAny<string>(), It.IsAny<Dictionary<string, Dictionary<string, string>>?>()))
            .Returns((string _, Dictionary<string, Dictionary<string, string>>? d) => d ?? new());
        _service = new RemoteIndexService(store.Object, _labels.Object, _browse.Object, _import.Object, _repository, registry, Mock.Of<ILogHelper>());
    }

    private void CreateRemoteIndexSchema()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE RemoteIndexEntries (
                SourceId TEXT NOT NULL, ListId TEXT NOT NULL, EntryId TEXT NOT NULL,
                Title TEXT NOT NULL DEFAULT '', DetailUrl TEXT NOT NULL, ImageUrl TEXT NOT NULL DEFAULT '',
                Tags TEXT, DateHint TEXT, Sensitive INTEGER, Generation INTEGER NOT NULL DEFAULT 0, SortKey INTEGER NOT NULL DEFAULT 0,
                FirstSeenUtc TEXT NOT NULL, LastSeenUtc TEXT NOT NULL, RemovedUtc TEXT, EnrichedUtc TEXT,
                PRIMARY KEY (SourceId, ListId, EntryId));
            CREATE TABLE RemoteIndexMeta (
                SourceId TEXT NOT NULL, ListId TEXT NOT NULL, SyncedAtUtc TEXT,
                TotalPages INTEGER NOT NULL DEFAULT 0, Generation INTEGER NOT NULL DEFAULT 0,
                FullSyncCompletedUtc TEXT,
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
    public async Task Meta_And_Entries_RoundTrip_As_UTC_Kind()
    {
        // SQLite loses DateTimeKind; the repository must re-mark UTC so JSON carries the Z suffix.
        // Without it the frontend parsed syncedAtUtc as LOCAL time and (east of UTC) every index
        // looked hours stale → the auto-sync fired on every library-page open (fixed 2026-07-06).
        SetupPages(new List<RemoteModCard> { Card(10, "反虚化3.0") });
        await SyncAndWaitAsync();

        var meta = await _repository.GetMetaAsync("huihui", "2");
        meta!.SyncedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        meta.FullSyncCompletedUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        // Freshly synced must read as "just now", not hours old.
        (DateTime.UtcNow - meta.SyncedAtUtc.Value).Should().BeLessThan(TimeSpan.FromMinutes(1));

        var page = await _service.QueryAsync("huihui", "2", null, 1, 10);
        page.Entries[0].FirstSeenUtc.Kind.Should().Be(DateTimeKind.Utc);
        page.Entries[0].LastSeenUtc.Kind.Should().Be(DateTimeKind.Utc);
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
    public async Task UpdateSync_StopsAfterTwoConsecutiveKnownPages()
    {
        SetupPages(
            new List<RemoteModCard> { Card(1, "A"), Card(2, "B") },
            new List<RemoteModCard> { Card(3, "C"), Card(4, "D") },
            new List<RemoteModCard> { Card(5, "E") },
            new List<RemoteModCard> { Card(6, "F") });
        await SyncAndWaitAsync(); // full: 4 pages → unlocks incremental

        // Next sync: one NEW mod pushed everything down — page 1 has 1 new, pages 2+3 have none new.
        // Feeds aren't strictly ordered (featured mixing), so ONE known page must not stop the crawl;
        // TWO consecutive known pages do → page 4 is never requested.
        SetupPages(
            new List<RemoteModCard> { Card(99, "NEW"), Card(1, "A") },
            new List<RemoteModCard> { Card(2, "B"), Card(3, "C") },
            new List<RemoteModCard> { Card(4, "D") },
            new List<RemoteModCard> { Card(5, "E"), Card(6, "F") });
        await SyncAndWaitAsync();

        _browse.Verify(b => b.BrowseAsync("huihui", "2", 3, It.IsAny<CancellationToken>()), Times.Once,
            "one known page is not enough to stop (unordered feeds)");
        _browse.Verify(b => b.BrowseAsync("huihui", "2", 4, It.IsAny<CancellationToken>()), Times.Never,
            "two consecutive known pages stop the update");
        var result = await _service.QueryAsync("huihui", "2", null, 1, 50);
        result.Info.EntryCount.Should().Be(7);
    }

    [Fact]
    public async Task Sync_AfterInterruptedFirstCrawl_DoesNotStopEarly()
    {
        // Simulate an INTERRUPTED first crawl: entries + meta WITHOUT FullSyncCompletedUtc (a real
        // interruption never writes meta, but a stale/legacy row could exist — either way, no
        // completed full pass on record means NO early stopping).
        SetupPages(
            new List<RemoteModCard> { Card(1, "A") },
            new List<RemoteModCard> { Card(2, "B") },
            new List<RemoteModCard> { Card(3, "C") });
        // Seed entries as if a partial crawl stored page 1 only, with meta lacking the completion flag.
        await _repository.UpsertEntriesAsync("huihui", "2", new List<RemoteIndexEntry>
        {
            new() { Id = "1", Title = "A", DetailUrl = "https://huihui168.org/?news_12/1.html", SortKey = 10000 },
        }, 1);
        await _repository.SetMetaAsync(new RemoteIndexMetaRow
        {
            SourceId = "huihui", ListId = "2", SyncedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            TotalPages = 3, Generation = 1, FullSyncCompletedUtc = null,
        });

        await SyncAndWaitAsync(); // NOT forced full — but must still crawl everything

        _browse.Verify(b => b.BrowseAsync("huihui", "2", 2, It.IsAny<CancellationToken>()), Times.Once,
            "page 1 being fully known must not stop the repair crawl");
        _browse.Verify(b => b.BrowseAsync("huihui", "2", 3, It.IsAny<CancellationToken>()), Times.Once);
        (await _service.QueryAsync("huihui", "2", null, 1, 50)).Info.EntryCount.Should().Be(3,
            "the hole left by the interrupted crawl is repaired");
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
    public async Task Update_ForcesAFullPrune_WhenTheLastFullPassIsOlderThanAWeek()
    {
        SetupPages(new List<RemoteModCard> { Card(1, "A"), Card(2, "B"), Card(3, "C") });
        await SyncAndWaitAsync(); // first sync is full → unlocks incremental

        // Backdate the completed full pass to >7 days ago: the NEXT update must force a full
        // (pruning) crawl instead of stopping early, so site-deleted entries can't linger forever.
        var meta = await _repository.GetMetaAsync("huihui", "2");
        meta!.FullSyncCompletedUtc = DateTime.UtcNow.AddDays(-8);
        await _repository.SetMetaAsync(meta);

        SetupPages(new List<RemoteModCard> { Card(1, "A"), Card(3, "C") }); // site dropped mod 2
        await SyncAndWaitAsync(); // an UPDATE — but the stale full pass forces a full crawl

        var result = await _service.QueryAsync("huihui", "2", null, 1, 50);
        result.Entries.Select(e => e.Id).Should().BeEquivalentTo(new[] { "1", "3" },
            "a >1wk-old full pass forces the next update to a pruning full crawl");
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
    public async Task Query_FreeTextSearch_MatchesTitle_Tag_AndLabelAlias()
    {
        // The toolbar search box is free-text (INDEX_QUERY search=), NOT the tag-chip filter. It must
        // match TITLE, any TAG (substring), and a tag's display LABEL/alias in any language.
        SetupPages(new List<RemoteModCard>
        {
            Card(1, "反虚化", tags: new[] { "Skins", "Hu Tao" }),
            Card(2, "星见雅", tags: new[] { "Effects" }),
        });
        await SyncAndWaitAsync();

        // Title (baseline that already worked).
        (await _service.QueryAsync("huihui", "2", "反虚化", 1, 50)).Entries.Single().Id.Should().Be("1");
        // Raw tag — exact + substring, case-insensitive.
        (await _service.QueryAsync("huihui", "2", "Hu Tao", 1, 50)).Entries.Single().Id.Should().Be("1");
        (await _service.QueryAsync("huihui", "2", "skin", 1, 50)).Entries.Single().Id.Should().Be("1");
        (await _service.QueryAsync("huihui", "2", "effect", 1, 50)).Entries.Single().Id.Should().Be("2");

        // Tag LABEL/alias: "皮肤" is the cn label for raw tag "Skins" → must find entry 1.
        _labels.Setup(l => l.GetForSource(It.IsAny<string>(), It.IsAny<Dictionary<string, Dictionary<string, string>>?>()))
            .Returns(new Dictionary<string, Dictionary<string, string>> { ["cn"] = new() { ["Skins"] = "皮肤" } });
        (await _service.QueryAsync("huihui", "2", "皮肤", 1, 50)).Entries.Single().Id.Should().Be("1");
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
    public async Task Sync_EnrichesDetails_WhenEngineProvidesDetailTags_AndOnlyOnce()
    {
        SetupPages(new List<RemoteModCard> { Card(1, "A", tags: new[] { "Skins" }), Card(2, "B") });
        _browse.Setup(b => b.DetailProvidesTags("huihui", It.IsAny<string?>())).Returns(true);
        _browse.Setup(b => b.GetDetailAsync("huihui", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string url, string? __, CancellationToken ___) => new RemoteModDetail
            {
                DetailUrl = url,
                Tags = new List<string> { url.Contains("/1.") ? "Jane Doe" : "Ellen" },
            });

        await SyncAndWaitAsync();
        // Wait for the enrichment tail (runs inside the same process, after the crawl).
        for (var i = 0; i < 100; i++)
        {
            var check = await _service.QueryAsync("huihui", "2", null, 1, 10);
            if (check.Entries.All(e => e.Tags.Count >= 1) && check.Entries.Single(e => e.Id == "1").Tags.Count == 2) break;
            await Task.Delay(50);
        }

        var entries = (await _service.QueryAsync("huihui", "2", null, 1, 10)).Entries;
        entries.Single(e => e.Id == "1").Tags.Should().BeEquivalentTo(new[] { "Skins", "Jane Doe" },
            because: "the detail tag merges with the list tag");
        entries.Single(e => e.Id == "2").Tags.Should().BeEquivalentTo(new[] { "Ellen" });

        // Second sync: everything already enriched — no further detail fetches for old entries.
        _browse.Invocations.Clear();
        SetupPages(new List<RemoteModCard> { Card(1, "A", tags: new[] { "Skins" }), Card(2, "B") });
        _browse.Setup(b => b.DetailProvidesTags("huihui", It.IsAny<string?>())).Returns(true);
        await SyncAndWaitAsync();
        await Task.Delay(200); // give a would-be enrichment a moment to (wrongly) start
        _browse.Verify(b => b.GetDetailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never, "already-enriched entries are not refetched");

        // And the re-upsert must NOT wipe the merged detail tags back to the list page's coarse tag
        // (the card shows the DETAIL tag — GameBanana sub category; regression 2026-07-06: a plain
        // Tags overwrite lost "Jane Doe" on every re-sync and the entry was never re-enriched).
        var after = (await _service.QueryAsync("huihui", "2", null, 1, 10)).Entries;
        after.Single(e => e.Id == "1").Tags.Should().BeEquivalentTo(new[] { "Skins", "Jane Doe" },
            because: "a re-sync keeps the richer merged tag list");
        after.Single(e => e.Id == "2").Tags.Should().BeEquivalentTo(new[] { "Ellen" });
    }

    [Fact]
    public async Task QueryAnnotated_AnnotatesResults_WithoutRestricting_WhenNotImportedOnly()
    {
        // The import↔index orchestration moved out of RemoteFacade (thin-delegate refactor 2026-07-13):
        // a normal query annotates every returned page against the import lookup but does NOT restrict.
        SetupPages(new List<RemoteModCard> { Card(1, "A"), Card(2, "B") });
        await SyncAndWaitAsync();

        var page = await _service.QueryAnnotatedAsync("huihui", "2", null, 1, 50);

        page.Entries.Select(e => e.Id).Should().BeEquivalentTo(new[] { "1", "2" });
        _import.Verify(i => i.GetImportedEntryIdsAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never,
            "no id restriction unless importedOnly");
        _import.Verify(i => i.AnnotateImportedAsync(page.Entries, "huihui", "2"), Times.Once,
            "every returned page is annotated against the import lookup");
    }

    [Fact]
    public async Task QueryAnnotated_ImportedOnly_RestrictsToImportedEntryIds()
    {
        SetupPages(new List<RemoteModCard> { Card(1, "A"), Card(2, "B"), Card(3, "C") });
        await SyncAndWaitAsync();
        _import.Setup(i => i.GetImportedEntryIdsAsync("huihui", "2")).ReturnsAsync(new[] { "2" });

        var page = await _service.QueryAnnotatedAsync("huihui", "2", null, 1, 50, importedOnly: true);

        page.Entries.Select(e => e.Id).Should().Equal("2");
        _import.Verify(i => i.GetImportedEntryIdsAsync("huihui", "2"), Times.Once);
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
