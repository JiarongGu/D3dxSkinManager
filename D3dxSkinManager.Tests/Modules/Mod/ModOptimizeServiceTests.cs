using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod;

/// <summary>
/// ModOptimizeService — dedup + filename normalization over a real temp cache dir. The op queue is
/// run inline, archive-compress is stubbed true; asserts on the on-disk result (renamed files +
/// rewritten `filename =` refs). See filesystem-operation-serialization.md (renames run under the
/// per-mod lock; recompress because append can't rename entries).
/// </summary>
public class ModOptimizeServiceTests : IDisposable
{
    private const string ModId = "test-mod";

    private readonly string _dir;
    private readonly Mock<IModArchiveService> _archive = new();
    private readonly ModOptimizeService _service;

    public ModOptimizeServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-opt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);

        var cache = new Mock<IModCacheService>();
        cache.Setup(c => c.GetCachePath(ModId)).Returns(_dir);
        _archive.Setup(a => a.CompressCacheToArchiveAsync(ModId, _dir)).ReturnsAsync(true);

        // Run the queued op inline. Two concrete setups (this Moq rejects It.IsAnyType in Returns):
        // ScanAsync uses <ModOptimizeScanResult>, ApplyAsync uses <ModOptimizeResult>.
        var queue = new Mock<IModOperationQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<Func<Task<ModOptimizeScanResult>>>()))
            .Returns((string _, Func<Task<ModOptimizeScanResult>> op) => op());
        queue.Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<Func<Task<ModOptimizeResult>>>()))
            .Returns((string _, Func<Task<ModOptimizeResult>> op) => op());

        _service = new ModOptimizeService(cache.Object, _archive.Object, queue.Object,
            Mock.Of<IProfileEventBus>(), Mock.Of<IProcessRegistry>(), Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private void Write(string rel, string content) =>
        File.WriteAllText(Path.Combine(_dir, rel.Replace('/', Path.DirectorySeparatorChar)), content, new UTF8Encoding(false));

    private void WriteBytes(string rel, byte[] bytes) =>
        File.WriteAllBytes(Path.Combine(_dir, rel.Replace('/', Path.DirectorySeparatorChar)), bytes);

    [Fact]
    public async Task Scan_ReportsUnsafeReferencedNames()
    {
        WriteBytes("陈千语.buf", new byte[] { 1, 2, 3 });
        WriteBytes("plain.buf", new byte[] { 4, 5, 6 });
        Write("mod.ini", "[Resource]\nfilename = 陈千语.buf\n[Resource2]\nfilename = plain.buf\n");

        var scan = await _service.ScanAsync(ModId);

        scan.Normalizable.Should().ContainSingle();
        scan.Normalizable[0].From.Should().Be("陈千语.buf");
        scan.Normalizable[0].To.Should().Be("asset.buf", "an all-non-ASCII stem collapses to empty → 'asset'; ext kept");
    }

    [Fact]
    public async Task Apply_Normalize_RenamesFileAndRewritesRef()
    {
        WriteBytes("body模型v2.buf", new byte[] { 1, 2, 3, 4 });
        Write("mod.ini", "; a mod\n[TextureOverride]\nfilename = body模型v2.buf ; the mesh\n");

        var result = await _service.ApplyAsync(ModId, normalizeNames: true);

        result.RenamedFiles.Should().Be(1);
        result.RewrittenRefs.Should().Be(1);
        Directory.EnumerateFiles(_dir, "*.buf").Select(Path.GetFileName)
            .Should().ContainSingle().Which.Should().Be("body_v2.buf", "mid-string CJK run → single '_', ASCII kept");
        var ini = await File.ReadAllTextAsync(Path.Combine(_dir, "mod.ini"));
        ini.Should().Contain("filename = body_v2.buf ; the mesh", "ref rewritten, inline comment preserved");
        _archive.Verify(a => a.CompressCacheToArchiveAsync(ModId, _dir), Times.Once, "renames need a full recompress");
    }

    [Fact]
    public async Task Apply_WithoutNormalizeFlag_LeavesUnsafeNamesAlone()
    {
        WriteBytes("日本語.buf", new byte[] { 9, 9 });
        Write("mod.ini", "filename = 日本語.buf\n");

        var result = await _service.ApplyAsync(ModId, normalizeNames: false);

        result.RenamedFiles.Should().Be(0);
        File.Exists(Path.Combine(_dir, "日本語.buf")).Should().BeTrue("normalization is opt-in");
        _archive.Verify(a => a.CompressCacheToArchiveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never,
            "nothing changed → no recompress");
    }

    [Fact]
    public async Task Apply_Normalize_AvoidsCollisions()
    {
        // Two all-CJK names in the same dir both normalize to "asset.buf" → second gets a suffix.
        WriteBytes("图.buf", new byte[] { 1 });
        WriteBytes("圖.buf", new byte[] { 2 });
        Write("mod.ini", "filename = 图.buf\nfilename = 圖.buf\n");

        var result = await _service.ApplyAsync(ModId, normalizeNames: true);

        result.RenamedFiles.Should().Be(2);
        var names = Directory.EnumerateFiles(_dir, "*.buf").Select(Path.GetFileName).OrderBy(n => n).ToList();
        names.Should().BeEquivalentTo(new[] { "asset.buf", "asset_1.buf" });
    }
}
