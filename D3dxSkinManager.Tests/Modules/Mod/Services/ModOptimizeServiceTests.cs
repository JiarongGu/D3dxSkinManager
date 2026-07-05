using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Tests for the duplicate-asset optimizer (user ask 2026-07-05: "mod optimization (dedup asset
/// files)"). Real temp directories + files; archive service mocked. Safety invariants: .ini files
/// are never deduplicated, refs are rewritten before deletion, still-referenced copies are kept,
/// and deletions force a full recompress.
/// </summary>
public class ModOptimizeServiceTests : IDisposable
{
    private readonly Mock<IModCacheService> _cache = new();
    private readonly Mock<IModArchiveService> _archive = new();
    private readonly Mock<IProfileEventBus> _eventBus = new();
    private readonly Mock<IProcessRegistry> _registry = new();
    private readonly IModOperationQueue _queue = new ModOperationQueue(Mock.Of<ILogHelper>());
    private readonly ModOptimizeService _service;
    private readonly string _root;

    public ModOptimizeServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "d3dx-optimize-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _cache.Setup(c => c.GetCachePath(It.IsAny<string>())).Returns((string id) =>
        {
            var dir = Path.Combine(_root, id);
            return Directory.Exists(dir) ? dir : null;
        });
        _eventBus.Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        _registry.Setup(r => r.Start(It.IsAny<ProcessType>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("proc-1");
        _registry.Setup(r => r.GetToken(It.IsAny<string>())).Returns(CancellationToken.None);
        _archive.Setup(a => a.CompressCacheToArchiveAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        _service = new ModOptimizeService(
            _cache.Object, _archive.Object, _queue,
            _eventBus.Object, _registry.Object, Mock.Of<ILogHelper>());
    }

    /// <summary>Builds a mod cache: a subfolder ini referencing textures, one texture duplicated.</summary>
    private string BuildDuplicatedMod(string id)
    {
        var dir = Path.Combine(_root, id, "SubMod");
        Directory.CreateDirectory(dir);
        var bytes = new byte[4096];
        new Random(42).NextBytes(bytes);
        File.WriteAllBytes(Path.Combine(dir, "body.dds"), bytes);
        File.WriteAllBytes(Path.Combine(dir, "body_copy.dds"), bytes);       // identical duplicate
        File.WriteAllBytes(Path.Combine(dir, "unique.dds"), new byte[2048]); // different content
        File.WriteAllText(Path.Combine(dir, "mod.ini"),
            "[ResourceBody]\nfilename = body.dds\n\n[ResourceBodyCopy]\nfilename = body_copy.dds\n\n[ResourceUnique]\nfilename = unique.dds\n");
        return Path.Combine(_root, id);
    }

    [Fact]
    public async Task Scan_GroupsIdenticalFiles_SkipsIniFiles_ComputesWastedBytes()
    {
        BuildDuplicatedMod("MODA");
        // An identical pair of .ini files must NOT be deduplicated (sections load per-file).
        File.WriteAllText(Path.Combine(_root, "MODA", "a.ini"), "[X]\ny = 1\n");
        File.WriteAllText(Path.Combine(_root, "MODA", "b.ini"), "[X]\ny = 1\n");

        var scan = await _service.ScanAsync("MODA");

        scan.Groups.Should().ContainSingle();
        scan.Groups[0].Canonical.Should().Be("SubMod/body.dds");
        scan.Groups[0].Duplicates.Should().ContainSingle().Which.Should().Be("SubMod/body_copy.dds");
        scan.WastedBytes.Should().Be(4096);
    }

    [Fact]
    public async Task Apply_RewritesRefs_DeletesDuplicate_FullRecompress()
    {
        var cacheDir = BuildDuplicatedMod("MODB");

        var result = await _service.ApplyAsync("MODB");

        result.RemovedFiles.Should().Be(1);
        result.RewrittenRefs.Should().Be(1);
        result.FreedBytes.Should().Be(4096);
        // The duplicate is gone, the canonical stays.
        File.Exists(Path.Combine(cacheDir, "SubMod", "body_copy.dds")).Should().BeFalse();
        File.Exists(Path.Combine(cacheDir, "SubMod", "body.dds")).Should().BeTrue();
        // The ref that pointed at the duplicate now points at the canonical (line-ending agnostic).
        var iniLines = await File.ReadAllLinesAsync(Path.Combine(cacheDir, "SubMod", "mod.ini"));
        iniLines.Should().NotContain(l => l.Contains("body_copy.dds"));
        iniLines.Should().ContainInOrder("[ResourceBodyCopy]", "filename = body.dds");
        // Files were deleted → full recompress (append can't remove entries).
        _archive.Verify(a => a.CompressCacheToArchiveAsync("MODB", cacheDir), Times.Once);
        _registry.Verify(r => r.Complete("proc-1"), Times.Once);
    }

    [Fact]
    public async Task Apply_NoDuplicates_LeavesEverythingUntouched()
    {
        var dir = Path.Combine(_root, "MODC");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "one.dds"), new byte[128]);
        File.WriteAllText(Path.Combine(dir, "mod.ini"), "[R]\nfilename = one.dds\n");

        var result = await _service.ApplyAsync("MODC");

        result.RemovedFiles.Should().Be(0);
        _archive.Verify(a => a.CompressCacheToArchiveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _registry.Verify(r => r.Complete("proc-1"), Times.Once);
    }

    [Fact]
    public async Task Apply_RefsAcrossSubfolders_RewrittenRelativeToEachIni()
    {
        // The referencing .ini lives in a DIFFERENT folder than the duplicate — the rewritten path
        // must stay relative to the .ini's own directory.
        var root = Path.Combine(_root, "MODD");
        Directory.CreateDirectory(Path.Combine(root, "A"));
        Directory.CreateDirectory(Path.Combine(root, "B"));
        var bytes = new byte[1024];
        File.WriteAllBytes(Path.Combine(root, "A", "tex.dds"), bytes);
        File.WriteAllBytes(Path.Combine(root, "B", "tex.dds"), bytes);
        File.WriteAllText(Path.Combine(root, "B", "mod.ini"), "[R]\nfilename = tex.dds\n");

        var result = await _service.ApplyAsync("MODD");

        result.RemovedFiles.Should().Be(1);
        // Canonical (shortest-then-ordinal) is A/tex.dds; B's ini must now point at ..\A\tex.dds.
        var ini = await File.ReadAllTextAsync(Path.Combine(root, "B", "mod.ini"));
        ini.Should().Contain(@"filename = ..\A\tex.dds");
        File.Exists(Path.Combine(root, "A", "tex.dds")).Should().BeTrue();
        File.Exists(Path.Combine(root, "B", "tex.dds")).Should().BeFalse();
    }

    [Fact]
    public async Task Scan_NoCache_ThrowsOptimizeNoCache()
    {
        var act = () => _service.ScanAsync("GHOST");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("MOD_OPTIMIZE_NO_CACHE");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
