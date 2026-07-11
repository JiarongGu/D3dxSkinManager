using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Tests for the imported-lookup helpers moved out of RemoteFacade into RemoteImportService
/// (GetImportedEntryIdsAsync / AnnotateImportedAsync / GetImportedStateAsync). Only the mod repository
/// participates in these reads; the other ctor deps are stubbed.
/// </summary>
public class RemoteImportServiceImportedLookupTests
{
    private readonly Mock<IModRepository> _repo = new();

    private RemoteImportService CreateService(params ModEntity[] mods)
    {
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(mods.ToList());
        return new RemoteImportService(
            Mock.Of<ICloudreveShareResolver>(),
            Mock.Of<IQuarkShareResolver>(),
            Mock.Of<IDownloadService>(),
            Mock.Of<IModImportService>(),
            _repo.Object,
            Mock.Of<IImageService>(),
            Mock.Of<IRemoteLibraryStore>(),
            Mock.Of<IProfilePathService>(),
            Mock.Of<IProcessRegistry>(),
            Mock.Of<IArchiveHelper>(),
            Mock.Of<IEventBus>(),
            Mock.Of<ILogHelper>());
    }

    private static ModEntity Imported(string modId, string sourceId, string listId, string entryId, string detailUrl)
        => new() { Id = modId, Metadata = RemoteImportService.WriteRemoteMetadata(null, sourceId, listId, entryId, detailUrl, "sha") };

    [Fact]
    public async Task GetImportedEntryIdsAsync_ReturnsEntryIdsForThatSourceList_ExcludesOthers()
    {
        var svc = CreateService(
            Imported("m1", "src1", "list1", "entry1", "d1"),
            Imported("m2", "src1", "list1", "entry2", "d2"),
            Imported("m3", "src2", "list1", "entryX", "dx"));

        var ids = await svc.GetImportedEntryIdsAsync("src1", "list1");

        ids.Should().BeEquivalentTo(new[] { "entry1", "entry2" });
    }

    [Fact]
    public async Task AnnotateImportedAsync_FlagsAndLocatesImportedEntries()
    {
        var svc = CreateService(Imported("m1", "src1", "list1", "entry1", "d1"));
        var entries = new List<RemoteIndexEntry>
        {
            new() { Id = "entry1", DetailUrl = "d1" },
            new() { Id = "entryX", DetailUrl = "dx" },
        };

        await svc.AnnotateImportedAsync(entries, "src1", "list1");

        entries[0].Imported.Should().BeTrue();
        entries[0].LocalModIds.Should().Contain("m1");
        entries[1].Imported.Should().BeFalse();
    }

    [Fact]
    public async Task GetImportedStateAsync_MatchesByKeyThenDetailUrl_ElseNotImported()
    {
        var svc = CreateService(Imported("m1", "src1", "list1", "entry1", "d1"));

        var byKey = await svc.GetImportedStateAsync("src1", "list1", "entry1", null);
        byKey.Imported.Should().BeTrue();
        byKey.LocalModIds.Should().ContainSingle().Which.Should().Be("m1");

        var byUrl = await svc.GetImportedStateAsync("src1", "list1", "nope", "d1");
        byUrl.Imported.Should().BeTrue();

        var miss = await svc.GetImportedStateAsync("src1", "list1", "missing", "missing");
        miss.Imported.Should().BeFalse();
        miss.LocalModIds.Should().BeEmpty();
    }
}
