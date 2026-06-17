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
/// Concurrency tests for FileOperationPlanner driven by an in-memory file system.
///
/// These prove the planner's core guarantee — that concurrently-submitted file operations are
/// executed strictly one at a time — without touching the real disk. The in-memory FS records the
/// peak number of overlapping mutations; if the planner ever lost serialization the peak would
/// exceed 1 and these tests would fail.
/// </summary>
public class FileOperationPlannerConcurrencyTests
{
    private static FileOperationPlanner CreatePlanner(InMemoryFileSystem fs)
        => new(Mock.Of<IArchiveHelper>(), fs, Mock.Of<ILogHelper>());

    [Fact]
    public async Task ConcurrentMoves_AreSerialized_PeakConcurrencyIsOne()
    {
        // Arrange
        var fs = new InMemoryFileSystem { OperationDelayMs = 5 };
        const int count = 20;
        for (int i = 0; i < count; i++)
            fs.SeedDirectory($"cache/src{i}");

        using var planner = CreatePlanner(fs);

        // Act: fire all moves at once
        var tasks = Enumerable.Range(0, count).Select(i =>
            planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.MoveDirectory,
                SourcePath = $"cache/src{i}",
                TargetPath = $"cache/dst{i}"
            })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert: all succeeded, executed one-at-a-time, and produced the right end state
        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().Be(1, "the planner must execute file operations sequentially");
        for (int i = 0; i < count; i++)
        {
            fs.DirectoryExists($"cache/dst{i}").Should().BeTrue();
            fs.DirectoryExists($"cache/src{i}").Should().BeFalse();
        }
    }

    [Fact]
    public async Task ConcurrentDeletes_AreSerialized_PeakConcurrencyIsOne()
    {
        var fs = new InMemoryFileSystem { OperationDelayMs = 5 };
        const int count = 15;
        for (int i = 0; i < count; i++)
            fs.SeedDirectory($"cache/DISABLED-{i}");

        using var planner = CreatePlanner(fs);

        var tasks = Enumerable.Range(0, count).Select(i =>
            planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.DeleteDirectory,
                SourcePath = $"cache/DISABLED-{i}"
            })).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().Be(1);
        for (int i = 0; i < count; i++)
            fs.DirectoryExists($"cache/DISABLED-{i}").Should().BeFalse();
    }

    [Fact]
    public async Task TransientLock_OnMove_IsRetriedThenSucceeds()
    {
        // Arrange: the move will throw IOException twice before succeeding
        var fs = new InMemoryFileSystem();
        fs.SeedDirectory("cache/src");
        fs.InjectTransientLock("cache/src", times: 2);

        using var planner = CreatePlanner(fs);

        // Act
        var result = await planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "cache/src",
            TargetPath = "cache/dst"
        });

        // Assert: planner retried (3 attempts) and the move ultimately landed
        result.Success.Should().BeTrue();
        fs.DirectoryExists("cache/dst").Should().BeTrue();
        fs.DirectoryExists("cache/src").Should().BeFalse();
        fs.TotalMutations.Should().BeGreaterThanOrEqualTo(3, "two failures + one success");
    }

    [Fact]
    public async Task MixedConcurrentOps_AreSerialized_PeakConcurrencyIsOne()
    {
        var fs = new InMemoryFileSystem { OperationDelayMs = 4 };
        for (int i = 0; i < 10; i++)
        {
            fs.SeedDirectory($"cache/move{i}");
            fs.SeedDirectory($"cache/del{i}");
            fs.SeedFile($"archives/file{i}");
        }

        using var planner = CreatePlanner(fs);

        var tasks = new List<Task<FileSystemOperationResult>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.MoveDirectory,
                SourcePath = $"cache/move{i}",
                TargetPath = $"cache/moved{i}"
            }));
            tasks.Add(planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.DeleteDirectory,
                SourcePath = $"cache/del{i}"
            }));
            tasks.Add(planner.SubmitOperationAsync(new FileSystemOperation
            {
                OperationType = FileSystemOperationType.DeleteFile,
                SourcePath = $"archives/file{i}"
            }));
        }

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().Be(1, "all operation types share the single planner worker");
    }

    // ---- Real-world scenario: long compress/decompress + another process holding the file +
    //      a different operation arriving at the same time ----

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
        // Arrange: existing archive is "in use by the game" — deleting it fails twice, then released.
        var fs = new InMemoryFileSystem();
        fs.SeedDirectory("cache/mod1");
        fs.SeedFile("archives/mod1");
        fs.InjectTransientLock("archives/mod1", times: 2);

        var compressCount = 0;
        var archive = ArchiveThatCompressesTo(fs, delayMs: 40, onCompress: () => Interlocked.Increment(ref compressCount));
        using var planner = new FileOperationPlanner(archive.Object, fs, Mock.Of<ILogHelper>());

        // Act
        var result = await planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.CompressArchive,
            SourcePath = "cache/mod1",
            TargetPath = "archives/mod1",
            TempPath = "archives/mod1.tmp"
        });

        // Assert: the game released the file so the retried replace lands — and crucially the
        // expensive compression ran ONLY ONCE despite the replace being retried.
        result.Success.Should().BeTrue();
        compressCount.Should().Be(1, "compression must not be repeated just because the file replace was retried");
        fs.FileExists("archives/mod1").Should().BeTrue();
        fs.FileExists("archives/mod1.tmp").Should().BeFalse("temp archive is moved into place");
    }

    [Fact]
    public async Task SlowCompress_WhileMoveAndDeleteArrive_AllSerialized_NoOverlap()
    {
        // Arrange: a slow compression occupies the worker while a move and a delete (different
        // operations) are submitted at the same time.
        var fs = new InMemoryFileSystem { OperationDelayMs = 3 };
        fs.SeedDirectory("cache/big");
        fs.SeedDirectory("cache/other");
        fs.SeedDirectory("cache/DISABLED-x");

        var archive = ArchiveThatCompressesTo(fs, delayMs: 80);
        using var planner = new FileOperationPlanner(archive.Object, fs, Mock.Of<ILogHelper>());

        // Act: fire all three at once
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

        // Assert: serialized despite the long compression; end state correct
        results.Should().OnlyContain(r => r.Success);
        fs.MaxConcurrentMutations.Should().Be(1, "a slow compression must not overlap other queued operations");
        fs.DirectoryExists("cache/other-moved").Should().BeTrue();
        fs.DirectoryExists("cache/DISABLED-x").Should().BeFalse();
    }

    [Fact]
    public async Task PersistentExternalLock_FailsWithInUseError()
    {
        // Arrange: another process holds the folder for the entire retry budget (never releases).
        var fs = new InMemoryFileSystem();
        fs.SeedDirectory("cache/locked");
        fs.InjectTransientLock("cache/locked", times: 99);

        using var planner = CreatePlanner(fs);

        // Act
        var result = await planner.SubmitOperationAsync(new FileSystemOperation
        {
            OperationType = FileSystemOperationType.MoveDirectory,
            SourcePath = "cache/locked",
            TargetPath = "cache/moved"
        });

        // Assert: surfaces a clear, actionable "in use by another process" failure rather than hanging
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("in use");
        fs.DirectoryExists("cache/locked").Should().BeTrue("a failed move must leave the source intact");
    }
}
