using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Tests.TestHelpers;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Concurrency tests for FileOperationPlanner (path-overlap dispatcher) driven by an in-memory file
/// system — integration-style, no real disk.
///
/// The planner's guarantee changed from "everything strictly serial" to:
///  - operations on OVERLAPPING paths never run at once (<see cref="InMemoryFileSystem.MaxConcurrentSamePath"/> stays 1),
///  - operations on DISJOINT paths DO run in parallel up to the cap (<see cref="InMemoryFileSystem.MaxConcurrentMutations"/> exceeds 1).
///
/// A fixed cap is passed so parallelism is deterministic regardless of the runner's core count.
/// </summary>
public class FileOperationPlannerConcurrencyTests
{
    private static FileOperationPlanner CreatePlanner(InMemoryFileSystem fs, int maxConcurrency = 4)
        => new(Mock.Of<IArchiveHelper>(), fs, Mock.Of<ILogHelper>(), maxConcurrency);

    [Fact]
    public async Task DisjointMoves_RunInParallel()
    {
        var fs = new InMemoryFileSystem { OperationDelayMs = 10 };
        const int count = 20;
        for (int i = 0; i < count; i++)
            fs.SeedDirectory($"cache/src{i}");

        using var planner = CreatePlanner(fs, maxConcurrency: 4);

        var tasks = Enumerable.Range(0, count).Select(i =>
            planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.MoveDirectory,
                SourcePath = $"cache/src{i}",
                TargetPath = $"cache/dst{i}"
            })).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().BeGreaterThan(1, "disjoint-path moves must run in parallel");
        fs.MaxConcurrentMutations.Should().BeLessThanOrEqualTo(4, "never exceed the concurrency cap");
        fs.MaxConcurrentSamePath.Should().Be(1, "no two ops touched the same path at once");
        for (int i = 0; i < count; i++)
        {
            fs.DirectoryExists($"cache/dst{i}").Should().BeTrue();
            fs.DirectoryExists($"cache/src{i}").Should().BeFalse();
        }
    }

    [Fact]
    public async Task DisjointDeletes_RunInParallel()
    {
        var fs = new InMemoryFileSystem { OperationDelayMs = 10 };
        const int count = 15;
        for (int i = 0; i < count; i++)
            fs.SeedDirectory($"cache/DISABLED-{i}");

        using var planner = CreatePlanner(fs, maxConcurrency: 4);

        var tasks = Enumerable.Range(0, count).Select(i =>
            planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.DeleteDirectory,
                SourcePath = $"cache/DISABLED-{i}"
            })).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().BeGreaterThan(1);
        fs.MaxConcurrentSamePath.Should().Be(1);
        for (int i = 0; i < count; i++)
            fs.DirectoryExists($"cache/DISABLED-{i}").Should().BeFalse();
    }

    [Fact]
    public async Task OverlappingOps_OnSamePath_AreSerialized()
    {
        // Many copies FROM the same source file (different targets → not deduped). They all mutate the
        // same source path, so the planner must serialize them: peak same-path concurrency stays 1.
        var fs = new InMemoryFileSystem { OperationDelayMs = 10 };
        fs.SeedFile("src/shared.bin");
        const int count = 6;

        using var planner = CreatePlanner(fs, maxConcurrency: 4);

        var tasks = Enumerable.Range(0, count).Select(i =>
            planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.CopyFile,
                SourcePath = "src/shared.bin",
                TargetPath = $"dst/copy{i}.bin"
            })).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().Be(1, "all ops overlap on the same source path → strictly serial");
        fs.MaxConcurrentSamePath.Should().Be(1);
    }

    [Fact]
    public async Task OverlappingOps_AncestorAndDescendant_AreSerialized()
    {
        // A copy of a CHILD file overlaps a delete of the PARENT dir (ancestor/descendant) → serialized.
        // Submitted child-first, so FIFO runs the copy before the delete removes the tree.
        var fs = new InMemoryFileSystem { OperationDelayMs = 10 };
        fs.SeedDirectory("cache/mod1");
        fs.SeedFile("cache/mod1/inner.bin");

        using var planner = CreatePlanner(fs, maxConcurrency: 4);

        var copy = planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.CopyFile,
            SourcePath = "cache/mod1/inner.bin", TargetPath = "backup/inner.bin"
        });
        var delete = planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.DeleteDirectory, SourcePath = "cache/mod1"
        });

        var results = await Task.WhenAll(copy, delete);

        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().Be(1, "ancestor/descendant paths overlap → must not run concurrently");
        fs.DirectoryExists("cache/mod1").Should().BeFalse();
        fs.FileExists("backup/inner.bin").Should().BeTrue("the child copy ran before the parent delete");
    }

    [Fact]
    public async Task MixedWorkload_DisjointRunParallel_WhileOverlappingStaySerialized()
    {
        // Integration: 8 disjoint copies (parallelizable) submitted alongside 6 copies from ONE shared
        // source (must serialize among themselves). Proves both properties hold at the same time.
        var fs = new InMemoryFileSystem { OperationDelayMs = 8 };
        for (int i = 0; i < 8; i++) fs.SeedFile($"src{i}/f.bin");
        fs.SeedFile("shared/f.bin");

        using var planner = CreatePlanner(fs, maxConcurrency: 4);

        var tasks = new List<Task<FileSystemOperationResult>>();
        for (int i = 0; i < 8; i++)
            tasks.Add(planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.CopyFile,
                SourcePath = $"src{i}/f.bin", TargetPath = $"out{i}/f.bin"
            }));
        for (int i = 0; i < 6; i++)
            tasks.Add(planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.CopyFile,
                SourcePath = "shared/f.bin", TargetPath = $"shared-out{i}/f.bin"
            }));

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().BeGreaterThan(1, "disjoint copies must parallelize");
        fs.MaxConcurrentSamePath.Should().Be(1, "the shared-source copies must never overlap each other");
    }

    [Fact]
    public async Task ConcurrencyCap_IsRespected()
    {
        var fs = new InMemoryFileSystem { OperationDelayMs = 15 };
        const int count = 12;
        for (int i = 0; i < count; i++) fs.SeedDirectory($"cache/m{i}");

        using var planner = CreatePlanner(fs, maxConcurrency: 3);

        var tasks = Enumerable.Range(0, count).Select(i =>
            planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.DeleteDirectory,
                SourcePath = $"cache/m{i}"
            })).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().BeLessThanOrEqualTo(3, "the cap bounds parallelism");
        fs.MaxConcurrentMutations.Should().BeGreaterThan(1, "but some parallelism still happens");
    }

    [Fact]
    public async Task TransientLock_OnMove_IsRetriedThenSucceeds()
    {
        var fs = new InMemoryFileSystem();
        fs.SeedDirectory("cache/src");
        fs.InjectTransientLock("cache/src", times: 2);

        using var planner = CreatePlanner(fs);

        var result = await planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "cache/src",
            TargetPath = "cache/dst"
        });

        result.Success.Should().BeTrue();
        fs.DirectoryExists("cache/dst").Should().BeTrue();
        fs.DirectoryExists("cache/src").Should().BeFalse();
        fs.TotalMutations.Should().BeGreaterThanOrEqualTo(3, "two failures + one success");
    }

    // ---- Real-world scenario: long compress + another process holding the file ----

    private static Mock<IArchiveHelper> ArchiveThatCompressesTo(InMemoryFileSystem fs, int delayMs, Action? onCompress = null)
    {
        var archive = new Mock<IArchiveHelper>();
        archive
            .Setup(x => x.CompressFolderAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ArchiveFormat>(),
                It.IsAny<SharpSevenZip.CompressionLevel>(), It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>()))
            .Returns(async (string src, string outPath, ArchiveFormat f, SharpSevenZip.CompressionLevel l, Action<int>? p, CancellationToken ct) =>
            {
                onCompress?.Invoke();
                if (delayMs > 0) await Task.Delay(delayMs);   // simulate a slow compression
                fs.SeedFile(outPath);                          // produce the temp archive
                return outPath;
            });
        return archive;
    }

    [Fact]
    public async Task Compress_WhileGameHoldsArchive_CompressesOnce_ThenRetriesReplaceAndSucceeds()
    {
        var fs = new InMemoryFileSystem();
        fs.SeedDirectory("cache/mod1");
        fs.SeedFile("archives/mod1");
        fs.InjectTransientLock("archives/mod1", times: 2);

        var compressCount = 0;
        var archive = ArchiveThatCompressesTo(fs, delayMs: 40, onCompress: () => Interlocked.Increment(ref compressCount));
        using var planner = new FileOperationPlanner(archive.Object, fs, Mock.Of<ILogHelper>());

        var result = await planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.CompressArchive,
            SourcePath = "cache/mod1",
            TargetPath = "archives/mod1",
            TempPath = "archives/mod1.tmp"
        });

        result.Success.Should().BeTrue();
        compressCount.Should().Be(1, "compression must not be repeated just because the file replace was retried");
        fs.FileExists("archives/mod1").Should().BeTrue();
        fs.FileExists("archives/mod1.tmp").Should().BeFalse("temp archive is moved into place");
    }

    [Fact]
    public async Task SlowCompress_WhileDisjointOpsArrive_RunConcurrently()
    {
        // A slow compression of one mod must NOT block a move + delete of DIFFERENT mods anymore.
        var fs = new InMemoryFileSystem { OperationDelayMs = 5 };
        fs.SeedDirectory("cache/big");
        fs.SeedDirectory("cache/other");
        fs.SeedDirectory("cache/DISABLED-x");

        var archive = ArchiveThatCompressesTo(fs, delayMs: 80);
        using var planner = new FileOperationPlanner(archive.Object, fs, Mock.Of<ILogHelper>(), maxConcurrency: 4);

        var compress = planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.CompressArchive,
            SourcePath = "cache/big", TargetPath = "archives/big", TempPath = "archives/big.tmp"
        });
        var move = planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "cache/other", TargetPath = "cache/other-moved"
        });
        var delete = planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.DeleteDirectory,
            SourcePath = "cache/DISABLED-x"
        });

        var results = await Task.WhenAll(compress, move, delete);

        results.Should().OnlyContain(r => r.Success);
        fs.DirectoryExists("cache/other-moved").Should().BeTrue();
        fs.DirectoryExists("cache/DISABLED-x").Should().BeFalse();
        fs.MaxConcurrentSamePath.Should().Be(1, "disjoint ops overlap in time but never on the same path");
    }

    [Fact]
    public async Task PersistentExternalLock_FailsWithInUseError()
    {
        var fs = new InMemoryFileSystem();
        fs.SeedDirectory("cache/locked");
        fs.InjectTransientLock("cache/locked", times: 99);

        using var planner = CreatePlanner(fs);

        var result = await planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "cache/locked",
            TargetPath = "cache/moved"
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("in use");
        fs.DirectoryExists("cache/locked").Should().BeTrue("a failed move must leave the source intact");
    }
}
