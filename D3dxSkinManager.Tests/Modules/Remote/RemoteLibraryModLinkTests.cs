using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;
using D3dxSkinManager.Tests.Helpers;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// The mod ↔ remote-library FK link: a mod references its library by RemoteLibraryId; the library entity
/// owns the display name (resolved live). Covers the one-time metadata→FK backfill (repository SQL) and
/// the enrichment that surfaces LibraryName for the comprehensive mod search.
/// </summary>
public class RemoteLibraryModLinkTests : InMemoryDatabaseTestBase
{
    private readonly ModRepository _mods;

    public RemoteLibraryModLinkTests()
    {
        CreateRemoteLibrariesTable();
        _mods = new ModRepository(MockProfilePathService.Object, Mock.Of<ILogHelper>());
    }

    private void CreateRemoteLibrariesTable()
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE RemoteLibraries (
                Id TEXT PRIMARY KEY NOT NULL, SourceId TEXT NOT NULL, ListId TEXT NOT NULL,
                Name TEXT NOT NULL DEFAULT '', TagRules TEXT, Active INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0, AddedAtUtc TEXT NOT NULL);
            INSERT INTO RemoteLibraries (Id, SourceId, ListId, Name, Active, SortOrder, AddedAtUtc)
            VALUES ('LIB1', 'huihui', '2', 'Hui站 · 绝区零', 1, 0, '2026-07-01T00:00:00Z');";
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task BackfillRemoteLibraryReferences_MapsMetadataRefToLibraryId_OnlyForRemoteMods()
    {
        // A remote-imported mod whose FK hasn't been set yet (only metadata.remote), + a plain local mod.
        await _mods.InsertAsync(new ModEntity
        {
            Id = "M1", Category = "c", Name = "Remote mod",
            Metadata = """{"remote":{"sourceId":"huihui","listId":"2","entryId":"5"}}""",
            RemoteLibraryId = null,
        });
        await _mods.InsertAsync(new ModEntity { Id = "M2", Category = "c", Name = "Local mod", RemoteLibraryId = null });

        var filled = await _mods.BackfillRemoteLibraryReferencesAsync();

        filled.Should().Be(1, "only the remote-imported mod is linked");
        (await _mods.GetByIdAsync("M1"))!.RemoteLibraryId.Should().Be("LIB1");
        (await _mods.GetByIdAsync("M2"))!.RemoteLibraryId.Should().BeNull();

        // Idempotent: a second run changes nothing.
        (await _mods.BackfillRemoteLibraryReferencesAsync()).Should().Be(0);
    }

    [Fact]
    public void Enrichment_PopulatesLibraryName_FromRemoteLibraryId_Live()
    {
        var libraryStore = new Mock<IRemoteLibraryStore>();
        libraryStore.Setup(s => s.GetState()).Returns(new RemoteLibrariesState
        {
            Libraries = new List<RemoteLibrary> { new() { Id = "LIB1", SourceId = "huihui", ListId = "2", Name = "Hui站 · 绝区零" } },
            ActiveLibraryId = "LIB1",
        });

        var enrichment = new ModEnrichmentService(
            Mock.Of<IProfilePathService>(), Mock.Of<ICategoryService>(),
            Mock.Of<ITagRepository>(), Mock.Of<IModCacheService>(), libraryStore.Object);

        var mods = new List<ModInfo>
        {
            new() { Id = "M1", RemoteLibraryId = "LIB1" },
            new() { Id = "M2", RemoteLibraryId = "GONE" }, // library removed → no name
            new() { Id = "M3" },                            // non-remote
        };

        enrichment.PopulateLibraryNames(mods);

        mods[0].LibraryName.Should().Be("Hui站 · 绝区零", "resolved live from the library table by FK");
        mods[1].LibraryName.Should().BeEmpty("a removed library resolves to no name");
        mods[2].LibraryName.Should().BeEmpty("non-remote mods have no library");
    }
}
