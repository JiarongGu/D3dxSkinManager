using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Tests for ModArchiveService — the planner is mocked (no real 7z), so these cover the service's own
/// logic: archive-path resolution, the exists guards, mapping planner results (success/failure +
/// detectedType/fileCount), and that the right FileSystemOperation is submitted. File.Exists is the only
/// real FS touch, backed by a temp archive file.
/// </summary>
public class ModArchiveServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _archivePath;
    private readonly Mock<IFileOperationPlanner> _planner = new();
    private readonly ModArchiveService _service;

    public ModArchiveServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-arch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _archivePath = Path.Combine(_dir, "mod-1");

        var paths = new Mock<IProfilePathService>();
        paths.Setup(p => p.GetModArchivePath(It.IsAny<string>(), It.IsAny<string>())).Returns(_archivePath);
        paths.Setup(p => p.TempDirectory).Returns(_dir);

        _service = new ModArchiveService(
            paths.Object, _planner.Object, Mock.Of<ILogHelper>(), Mock.Of<IProcessRegistry>());
    }

    private void CreateArchiveFile() => File.WriteAllText(_archivePath, "fake-archive");

    private static FileSystemOperationResult Ok(Dictionary<string, object>? data = null)
        => new() { Success = true, Data = data ?? new() };

    private static FileSystemOperationResult Fail(string msg)
        => new() { Success = false, ErrorMessage = msg };

    [Fact]
    public void GetArchivePath_And_ArchiveExists_ReflectDisk()
    {
        _service.GetArchivePath("mod-1").Should().Be(_archivePath);
        _service.ArchiveExists("mod-1").Should().BeFalse();
        CreateArchiveFile();
        _service.ArchiveExists("mod-1").Should().BeTrue();
    }

    [Fact]
    public async Task ExtractAsync_ArchiveMissing_FailsWithoutCallingPlanner()
    {
        var result = await _service.ExtractAsync("mod-1", _dir);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
        _planner.Verify(p => p.SubmitOperationAsync(It.IsAny<FileSystemOperation>()), Times.Never);
    }

    [Fact]
    public async Task ExtractAsync_Success_MapsTypeAndFileCount_SubmitsExtractOp()
    {
        CreateArchiveFile();
        _planner.Setup(p => p.SubmitOperationAsync(It.IsAny<FileSystemOperation>()))
            .ReturnsAsync(Ok(new Dictionary<string, object> { { "detectedType", "gimi" }, { "fileCount", 5 } }));

        var result = await _service.ExtractAsync("mod-1", _dir);

        result.Success.Should().BeTrue();
        result.DetectedType.Should().Be("gimi");
        result.FileCount.Should().Be(5);
        _planner.Verify(p => p.SubmitOperationAsync(
            It.Is<FileSystemOperation>(o => o.OperationType == FileSystemOperationType.ExtractArchive
                && o.SourcePath == _archivePath && o.TargetPath == _dir)), Times.Once);
    }

    [Fact]
    public async Task ExtractAsync_PlannerFails_PropagatesError()
    {
        CreateArchiveFile();
        _planner.Setup(p => p.SubmitOperationAsync(It.IsAny<FileSystemOperation>()))
            .ReturnsAsync(Fail("disk full"));

        var result = await _service.ExtractAsync("mod-1", _dir);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("disk full");
    }

    [Fact]
    public async Task DeleteArchiveAsync_Exists_SubmitsDeleteAndReturnsTrue()
    {
        CreateArchiveFile();
        _planner.Setup(p => p.SubmitOperationAsync(It.IsAny<FileSystemOperation>())).ReturnsAsync(Ok());

        (await _service.DeleteArchiveAsync("mod-1")).Should().BeTrue();
        _planner.Verify(p => p.SubmitOperationAsync(
            It.Is<FileSystemOperation>(o => o.OperationType == FileSystemOperationType.DeleteFile)), Times.Once);
    }

    [Fact]
    public async Task DeleteArchiveAsync_Missing_ReturnsFalseWithoutPlanner()
    {
        (await _service.DeleteArchiveAsync("mod-1")).Should().BeFalse();
        _planner.Verify(p => p.SubmitOperationAsync(It.IsAny<FileSystemOperation>()), Times.Never);
    }

    [Fact]
    public async Task UpdateFileInArchiveAsync_Exists_SubmitsAppendOp()
    {
        CreateArchiveFile();
        _planner.Setup(p => p.SubmitOperationAsync(It.IsAny<FileSystemOperation>())).ReturnsAsync(Ok());

        (await _service.UpdateFileInArchiveAsync("mod-1", "src.ini", "sub/mod.ini")).Should().BeTrue();
        _planner.Verify(p => p.SubmitOperationAsync(
            It.Is<FileSystemOperation>(o => o.OperationType == FileSystemOperationType.UpdateFileInArchive
                && o.ArchiveEntryPath == "sub/mod.ini")), Times.Once);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }
}
