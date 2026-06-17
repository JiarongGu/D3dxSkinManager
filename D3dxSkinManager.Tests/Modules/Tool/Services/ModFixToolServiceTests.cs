using System;
using System.Collections.Generic;
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
/// Tests for the per-profile fix-tool library: top-level scan (loose executables + folders), entry
/// auto-resolution (lone exe over helper .py), unresolved + candidates, MULTIPLE entries per toolset
/// via SetEntries, and deletion.
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

    private static string MakeFile(string ext)
    {
        var p = Path.Combine(Path.GetTempPath(), $"src-{Guid.NewGuid():N}{ext}");
        File.WriteAllText(p, "echo");
        return p;
    }

    private static string MakeFolderWith(params string[] fileNames)
    {
        var dir = Path.Combine(Path.GetTempPath(), "srcfolder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var f in fileNames) File.WriteAllText(Path.Combine(dir, f), "x");
        return dir;
    }

    [Fact]
    public async Task Import_SingleFile_CopiesIntoLibrary_AndResolvesEntry()
    {
        var src = MakeFile(".exe");
        try
        {
            var tool = await _service.ImportAsync("My Fix", src, isFolder: false);
            tool.Entries.Should().ContainSingle();
            tool.Entries[0].Name.Should().Be(Path.GetFileName(src));
            File.Exists(tool.Entries[0].Path).Should().BeTrue("the file is copied into the tool folder");
        }
        finally { File.Delete(src); }
    }

    [Fact]
    public async Task Import_Folder_OneExe_PlusHelperPy_PicksExe()
    {
        var src = MakeFolderWith("helper.py", "run.exe");
        try
        {
            var tool = await _service.ImportAsync("Exe Plus Helper", src, isFolder: true);
            tool.Entries.Should().ContainSingle().Which.Name.Should().Be("run.exe");
        }
        finally { Directory.Delete(src, true); }
    }

    [Fact]
    public async Task Import_Folder_NoRunnable_ImportsUnresolved()
    {
        var src = MakeFolderWith("readme.txt");
        try
        {
            var tool = await _service.ImportAsync("No Entry", src, isFolder: true);
            tool.Entries.Should().BeEmpty("no runnable → unresolved");
            tool.Candidates.Should().BeEmpty();
        }
        finally { Directory.Delete(src, true); }
    }

    [Fact]
    public async Task Import_Folder_MultipleExe_Unresolved_ThenSetEntriesResolvesMultiple()
    {
        var src = MakeFolderWith("a.exe", "b.exe", "c.exe");
        try
        {
            var tool = await _service.ImportAsync("Many Exe", src, isFolder: true);
            tool.Entries.Should().BeEmpty("several exes → ambiguous, user must pick");
            tool.Candidates.Should().BeEquivalentTo(new[] { "a.exe", "b.exe", "c.exe" });

            // A toolset can expose MULTIPLE entries.
            await _service.SetEntriesAsync(tool.Id, new List<string> { "a.exe", "c.exe" });

            var refreshed = (await _service.GetAllAsync()).Single(t => t.Id == tool.Id);
            refreshed.Entries.Select(e => e.Name).Should().BeEquivalentTo(new[] { "a.exe", "c.exe" });
            refreshed.Entries.Should().OnlyContain(e => File.Exists(e.Path));

            // Clearing reverts to auto-resolution (still ambiguous → none).
            await _service.SetEntriesAsync(tool.Id, new List<string>());
            (await _service.GetAllAsync()).Single(t => t.Id == tool.Id).Entries.Should().BeEmpty();
        }
        finally { Directory.Delete(src, true); }
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
    public async Task GetAll_LooseTopLevelExe_IsAToolWithItselfAsEntry_AndDeleteRemoves()
    {
        Directory.CreateDirectory(_fixDir);
        File.WriteAllText(Path.Combine(_fixDir, "loosefix.bat"), "x");

        var all = await _service.GetAllAsync();
        var loose = all.Single(t => t.Id == "loosefix.bat");
        loose.Entries.Should().ContainSingle().Which.Name.Should().Be("loosefix.bat");
        loose.Candidates.Should().BeEmpty("a loose executable has nothing to choose");

        await _service.DeleteAsync("loosefix.bat");
        (await _service.GetAllAsync()).Should().BeEmpty();
        File.Exists(Path.Combine(_fixDir, "loosefix.bat")).Should().BeFalse();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_fixDir)) Directory.Delete(_fixDir, true); } catch { }
    }
}
