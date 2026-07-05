using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Facades;

/// <summary>
/// Tests for ModFacade IPC routing + real PayloadHelper parsing: each message type reaches the right
/// service, the id payload is parsed, a missing required payload errors, and an unknown type errors.
/// All 18 services are mocked; the real PayloadHelper exercises the actual JSON payload extraction.
/// </summary>
public class ModFacadeTests
{
    private readonly Mock<IModRepository> _repo = new();
    private readonly Mock<IModEnrichmentService> _enrich = new();
    private readonly Mock<IModLifecycleService> _lifecycle = new();
    private readonly Mock<IModOperationQueue> _queue = new();
    private readonly ModFacade _facade;

    public ModFacadeTests()
    {
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ModEntity>());
        _enrich.Setup(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>()))
            .ReturnsAsync((List<ModInfo> m) => m);
        // Pass-through queue: run the enqueued op inline so routing reaches the underlying service.
        _queue.Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<System.Func<Task<ModLoadResult>>>()))
            .Returns((string _, System.Func<Task<ModLoadResult>> op) => op());

        _facade = new ModFacade(
            _repo.Object,
            _lifecycle.Object,
            Mock.Of<IModCacheService>(),
            Mock.Of<IModDeletionService>(),
            Mock.Of<IModImportService>(),
            Mock.Of<IModQueryService>(),
            _enrich.Object,
            Mock.Of<IModMetadataService>(),
            Mock.Of<IModTagService>(),
            Mock.Of<IModKeybindingService>(),
            Mock.Of<IModIniService>(),
            Mock.Of<IModMergeService>(),
            Mock.Of<IModOptimizeService>(),
            Mock.Of<IModPresetService>(),
            Mock.Of<IModArchiveService>(),
            _queue.Object,
            new PayloadHelper(),                 // real payload parsing
            Mock.Of<IImageService>(),
            Mock.Of<IModCacheWatcher>(),
            Mock.Of<ILogHelper>());
    }

    private static IpcRequest Req(string type, string? payloadJson = null) => new()
    {
        Id = "req-1",
        Type = type,
        Module = "MOD",
        Payload = payloadJson == null ? null : JsonSerializer.Deserialize<JsonElement>(payloadJson),
    };

    [Fact]
    public async Task GetAll_RoutesToRepositoryAndEnriches()
    {
        var resp = await _facade.HandleMessageAsync(Req("GET_ALL"));

        resp.Success.Should().BeTrue();
        _repo.Verify(r => r.GetAllAsync(), Times.Once);
        _enrich.Verify(e => e.EnrichAllAsync(It.IsAny<List<ModInfo>>()), Times.Once);
    }

    [Fact]
    public async Task GetById_ParsesIdPayload_AndQueriesRepository()
    {
        var resp = await _facade.HandleMessageAsync(Req("GET_BY_ID", "{\"id\":\"abc123\"}"));

        resp.Success.Should().BeTrue();
        _repo.Verify(r => r.GetByIdAsync("abc123"), Times.Once); // id parsed from the JSON payload
    }

    [Fact]
    public async Task GetById_MissingPayload_ReturnsError()
    {
        var resp = await _facade.HandleMessageAsync(Req("GET_BY_ID")); // no payload

        resp.Success.Should().BeFalse();
        resp.Error.Should().NotBeNullOrEmpty();
        _repo.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Load_ParsesId_AndDelegatesToLifecycle()
    {
        await _facade.HandleMessageAsync(Req("LOAD", "{\"id\":\"mod-9\"}"));

        _lifecycle.Verify(l => l.LoadAsync("mod-9"), Times.Once);
    }

    [Fact]
    public async Task UnknownType_ReturnsError()
    {
        var resp = await _facade.HandleMessageAsync(Req("NOPE_NOT_A_TYPE"));

        resp.Success.Should().BeFalse();
        resp.Error.Should().NotBeNullOrEmpty();
    }
}
