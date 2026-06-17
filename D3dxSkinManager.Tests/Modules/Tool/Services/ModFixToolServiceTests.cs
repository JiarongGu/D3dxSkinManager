using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Tool.Services;

namespace D3dxSkinManager.Tests.Modules.Tool.Services;

/// <summary>
/// Tests for the per-profile fix-tool library: importing a single file or a multi-file folder into the
/// managed collection, entry auto-detection, listing (with resolved EntryPath), and deletion.
/// </summary>
public class ModFixToolServiceTests : IDisposable
{
    private readonly string _fixDir;
    private readonly ModFixToolService _service;

    public ModFixToolServiceTests()
    {
        _fixDir = Path.Combine(Path.GetTempPath(), "d3dx-fixtools-test-" + Guid.NewGuid().ToString("N"));
        var paths = new Mock<IProfilePathService>();
        paths.Setup(p => p.FixToolsDirectory).Returns(_fixDir);
        _service = new ModFixToolService(paths.Object, Mock.Of<ILogHelper>());
    }

    private string MakeFile(string ext)
    {
        var p = Path.Combine(Path.GetTempPath(), $"src-{Guid.NewGuid():N}{ext}");
        File.WriteAllText(p, "echo");
        return p;
    }

    [Fact]
    public async Task Import_SingleFile_CopiesIntoLibrary_AndSetsEntry()
    {
        var src = MakeFile(".exe");
        try
        {
            var tool = await _service.ImportAsync("My Fix", src, isFolder: false);

            tool.Name.Should().Be("My Fix");
            tool.EntryFile.Should().Be(Path.GetFileName(src));
            File.Exists(tool.EntryPath!).Should().BeTrue("the file is copied into the tool folder");
            tool.EntryPath!.Should().StartWith(Path.Combine(_fixDir, tool.Id));
        }
        finally { File.Delete(src); }
    }

    [Fact]
    public async Task Import_Folder_AutoDetectsEntry_PrefersExe()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "srcfolder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "helper.py"), "x");
        File.WriteAllText(Path.Combine(srcDir, "run.exe"), "x");
        try
        {
            var tool = await _service.ImportAsync("Folder Fix", srcDir, isFolder: true);
            tool.EntryFile.Should().Be("run.exe", "exe is preferred over py");
            File.Exists(Path.Combine(_fixDir, tool.Id, "helper.py")).Should().BeTrue("all files copied");
        }
        finally { Directory.Delete(srcDir, true); }
    }

    [Fact]
    public async Task Import_Folder_NoRunnable_ImportsUnresolved()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "srcfolder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "readme.txt"), "x");
        try
        {
            var tool = await _service.ImportAsync("No Entry", srcDir, isFolder: true);
            tool.EntryFile.Should().BeEmpty("no runnable → entry left unresolved");
            tool.EntryPath.Should().BeNull();
        }
        finally { Directory.Delete(srcDir, true); }
    }

    [Fact]
    public async Task Import_Folder_MultipleExe_Unresolved_ThenSetEntryResolves()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "srcfolder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "a.exe"), "x");
        File.WriteAllText(Path.Combine(srcDir, "b.exe"), "x");
        try
        {
            var tool = await _service.ImportAsync("Two Exe", srcDir, isFolder: true);
            tool.EntryPath.Should().BeNull("two exes → ambiguous, user must pick");
            tool.Candidates.Should().BeEquivalentTo(new[] { "a.exe", "b.exe" });

            await _service.SetEntryAsync(tool.Id, "b.exe");
            var entry = await _service.GetEntryPathAsync(tool.Id);
            entry.Should().EndWith("b.exe");
            (await _service.GetAllAsync()).Single(t => t.Id == tool.Id).EntryFile.Should().Be("b.exe");
        }
        finally { Directory.Delete(srcDir, true); }
    }

    [Fact]
    public async Task Import_Folder_OneExe_PlusHelperPy_PicksExe()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "srcfolder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "helper.py"), "x");
        File.WriteAllText(Path.Combine(srcDir, "run.exe"), "x");
        try
        {
            var tool = await _service.ImportAsync("Exe Plus Helper", srcDir, isFolder: true);
            tool.EntryFile.Should().Be("run.exe", "the lone exe wins over helper .py files");
        }
        finally { Directory.Delete(srcDir, true); }
    }

    [Fact]
    public async Task GetAll_LooseTopLevelExe_IsASingleFileTool()
    {
        Directory.CreateDirectory(_fixDir);
        File.WriteAllText(Path.Combine(_fixDir, "loosefix.bat"), "x");

        var all = await _service.GetAllAsync();
        var loose = all.Single(t => t.Id == "loosefix.bat");
        loose.EntryFile.Should().Be("loosefix.bat");
        loose.EntryPath.Should().NotBeNull();
        loose.Candidates.Should().BeEmpty("a loose executable has nothing to choose");
    }

    [Fact]
    public async Task Import_EmptyName_Throws()
    {
        var src = MakeFile(".bat");
        try
        {
            var act = () => _service.ImportAsync("  ", src, isFolder: false);
            (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("FIX_TOOL_NAME_REQUIRED");
        }
        finally { File.Delete(src); }
    }

    [Fact]
    public async Task GetAll_ReturnsImported_WithEntryPath_AndDeleteRemoves()
    {
        var src = MakeFile(".py");
        try
        {
            var tool = await _service.ImportAsync("Fix A", src, isFolder: false);

            var all = await _service.GetAllAsync();
            all.Should().ContainSingle();
            all[0].EntryPath.Should().NotBeNullOrEmpty();
            File.Exists(all[0].EntryPath!).Should().BeTrue();

            await _service.DeleteAsync(tool.Id);
            (await _service.GetAllAsync()).Should().BeEmpty();
            Directory.Exists(Path.Combine(_fixDir, tool.Id)).Should().BeFalse("tool folder deleted");
        }
        finally { File.Delete(src); }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_fixDir)) Directory.Delete(_fixDir, true); } catch { }
    }
}
