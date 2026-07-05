using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Tool.Models;
using D3dxSkinManager.Modules.Tool.Services;

namespace D3dxSkinManager.Tests.Modules.Tool.Services;

/// <summary>
/// Regression tests for the cleanup-tool scanners (user-reported 2026-07-05):
/// - dot-folders/dot-files (internal/tool items users keep in the Mods dir) must never be
///   reported as orphans (B6);
/// - every scanned item carries IsDirectory from the scanner — mod archives are extensionless
///   FILES, and the UI's old name-based guess misclassified them, breaking open-in-explorer (B7).
/// </summary>
public class FileCleanupServiceTests : IDisposable
{
    private readonly Mock<IProfilePathService> _paths = new();
    private readonly Mock<IModRepository> _repository = new();
    private readonly Mock<ICategoryService> _categories = new();
    private readonly Mock<IProfileService> _profiles = new();
    private readonly Mock<ILogHelper> _logger = new();
    private readonly Mock<IProcessRegistry> _registry = new();
    private readonly FileCleanupService _service;
    private readonly string _root;

    public FileCleanupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "d3dx-cleanup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "cache"));
        Directory.CreateDirectory(Path.Combine(_root, "mods"));

        _paths.Setup(p => p.CacheModsDirectory).Returns(Path.Combine(_root, "cache"));
        _paths.Setup(p => p.ModsDirectory).Returns(Path.Combine(_root, "mods"));

        _repository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<ModEntity> { new() { Id = "KNOWN", Name = "Known", Type = "7z", Grading = "G" } });

        var globalPaths = new Mock<D3dxSkinManager.Modules.Core.Services.IGlobalPathService>();
        globalPaths.Setup(g => g.BaseDataPath).Returns(Path.Combine(_root, "data"));
        _service = new FileCleanupService(
            _paths.Object, _repository.Object, _categories.Object,
            _profiles.Object, _logger.Object, _registry.Object, globalPaths.Object);
    }

    [Fact]
    public async Task ScanModCaches_SkipsDotFolders_AndMarksItemsAsDirectories()
    {
        // Arrange: a dot-folder (internal/tool), an orphan cache dir, and a known mod's cache dir
        var cache = Path.Combine(_root, "cache");
        Directory.CreateDirectory(Path.Combine(cache, ".tools"));
        Directory.CreateDirectory(Path.Combine(cache, "ORPHAN1"));
        Directory.CreateDirectory(Path.Combine(cache, "KNOWN"));

        // Act
        var result = await _service.ScanOrphansAsync(OrphanCategory.ModCache);

        // Assert: only the orphan is reported; the dot-folder is never an orphan; it's a directory
        result.Items.Select(i => i.Name).Should().BeEquivalentTo(new[] { "ORPHAN1" });
        result.Items.Single().IsDirectory.Should().BeTrue();
    }

    [Fact]
    public async Task ScanOrphanedArchives_SkipsDotFiles_AndMarksExtensionlessArchivesAsFiles()
    {
        // Arrange: a dot-file (tool marker), an orphan extensionless archive, and a known mod's archive
        var mods = Path.Combine(_root, "mods");
        await File.WriteAllTextAsync(Path.Combine(mods, ".gitkeep"), "");
        await File.WriteAllTextAsync(Path.Combine(mods, "ORPHANARCHIVE"), "fake-archive-bytes");
        await File.WriteAllTextAsync(Path.Combine(mods, "KNOWN"), "fake-archive-bytes");

        // Act
        var result = await _service.ScanOrphansAsync(OrphanCategory.OrphanedArchive);

        // Assert: the orphan archive is a FILE (extensionless — the UI must not guess from the name)
        result.Items.Select(i => i.Name).Should().BeEquivalentTo(new[] { "ORPHANARCHIVE" });
        result.Items.Single().IsDirectory.Should().BeFalse();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }
}
