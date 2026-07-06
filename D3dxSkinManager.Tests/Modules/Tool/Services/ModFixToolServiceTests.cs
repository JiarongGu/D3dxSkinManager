using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Tool.Services;

namespace D3dxSkinManager.Tests.Modules.Tool.Services;

/// <summary>
/// Tests for the PER-PROFILE fix-tool library ({profile}/fixtools): top-level scan (loose
/// executables + folders), entry auto-resolution (lone exe over helper .py), unresolved +
/// candidates, MULTIPLE entries per toolset via SetEntries, rename (folder-only), deletion, and
/// the one-time seed from the legacy shared {data}/fixtools location.
/// </summary>
public class ModFixToolServiceTests : IDisposable
{
    private readonly string _fixDir;
    private readonly string _legacyDir;
    private readonly ModFixToolService _service;

    public ModFixToolServiceTests()
    {
        _fixDir = Path.Combine(Path.GetTempPath(), "d3dx-fixtools-test-" + Guid.NewGuid().ToString("N"));
        _legacyDir = Path.Combine(Path.GetTempPath(), "d3dx-fixtools-legacy-" + Guid.NewGuid().ToString("N"));
        var profilePaths = new Mock<IProfilePathService>();
        profilePaths.Setup(p => p.FixToolsDirectory).Returns(_fixDir);
        var globalPaths = new Mock<IGlobalPathService>();
        globalPaths.Setup(p => p.FixToolsDirectory).Returns(_legacyDir);
        _service = new ModFixToolService(profilePaths.Object, globalPaths.Object, Mock.Of<ILogHelper>());
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

    [Fact]
    public async Task Rename_FolderTool_MovesFolder_AndKeepsContent()
    {
        var src = MakeFolderWith("run.exe");
        try
        {
            var tool = await _service.ImportAsync("Old Name", src, isFolder: true);

            var newId = await _service.RenameAsync(tool.Id, "New Name");

            newId.Should().Be("New Name");
            var all = await _service.GetAllAsync();
            all.Should().ContainSingle(t => t.Id == "New Name");
            all.Should().NotContain(t => t.Id == tool.Id);
            File.Exists(Path.Combine(_fixDir, "New Name", "run.exe")).Should().BeTrue();
        }
        finally { Directory.Delete(src, true); }
    }

    [Fact]
    public async Task Rename_LooseFileTool_Throws()
    {
        Directory.CreateDirectory(_fixDir);
        File.WriteAllText(Path.Combine(_fixDir, "loose.bat"), "x");

        var act = () => _service.RenameAsync("loose.bat", "Renamed");
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("FIX_TOOL_RENAME_FOLDER_ONLY");
    }

    // ===== Enable/disable toggle + per-entry aliases (persisted in a .fixmeta sidecar) =====

    [Fact]
    public async Task NewTool_IsEnabledByDefault()
    {
        var src = MakeFile(".exe");
        try
        {
            var tool = await _service.ImportAsync("Fresh", src, isFolder: false);
            tool.Enabled.Should().BeTrue();
            (await _service.GetAllAsync()).Single(t => t.Id == tool.Id).Enabled.Should().BeTrue();
        }
        finally { File.Delete(src); }
    }

    [Fact]
    public async Task SetEnabled_FolderTool_TogglesAndPersists_AndCleansMetaWhenReEnabled()
    {
        var src = MakeFolderWith("run.exe");
        try
        {
            var tool = await _service.ImportAsync("Toggle Me", src, isFolder: true);

            await _service.SetEnabledAsync(tool.Id, false);
            (await _service.GetAllAsync()).Single(t => t.Id == tool.Id).Enabled.Should().BeFalse();
            File.Exists(Path.Combine(_fixDir, tool.Id, ".fixmeta")).Should().BeTrue("disabled state is persisted");

            await _service.SetEnabledAsync(tool.Id, true);
            (await _service.GetAllAsync()).Single(t => t.Id == tool.Id).Enabled.Should().BeTrue();
            File.Exists(Path.Combine(_fixDir, tool.Id, ".fixmeta")).Should().BeFalse("all-default meta is removed to keep the folder clean");
        }
        finally { Directory.Delete(src, true); }
    }

    [Fact]
    public async Task SetEnabled_LooseTool_UsesSidecarMeta()
    {
        Directory.CreateDirectory(_fixDir);
        File.WriteAllText(Path.Combine(_fixDir, "loose.bat"), "x");

        await _service.SetEnabledAsync("loose.bat", false);

        (await _service.GetAllAsync()).Single(t => t.Id == "loose.bat").Enabled.Should().BeFalse();
        File.Exists(Path.Combine(_fixDir, "loose.bat.fixmeta")).Should().BeTrue("loose tools store meta in a sibling sidecar");
        // The sidecar must NOT be picked up as its own tool (it isn't runnable).
        (await _service.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task SetEntryAlias_SetsDisplayName_AndClearingRemovesIt()
    {
        var src = MakeFolderWith("a.exe", "b.exe");
        try
        {
            var tool = await _service.ImportAsync("Aliased", src, isFolder: true);
            await _service.SetEntriesAsync(tool.Id, new List<string> { "a.exe", "b.exe" });

            await _service.SetEntryAliasAsync(tool.Id, "a.exe", "Skin Fix");
            var entry = (await _service.GetAllAsync()).Single(t => t.Id == tool.Id).Entries.Single(e => e.Name == "a.exe");
            entry.DisplayName.Should().Be("Skin Fix");
            // The unaliased entry keeps no display name; candidates are unaffected by the sidecar.
            var refreshed = (await _service.GetAllAsync()).Single(t => t.Id == tool.Id);
            refreshed.Entries.Single(e => e.Name == "b.exe").DisplayName.Should().BeNull();
            refreshed.Candidates.Should().BeEquivalentTo(new[] { "a.exe", "b.exe" });

            await _service.SetEntryAliasAsync(tool.Id, "a.exe", "");
            (await _service.GetAllAsync()).Single(t => t.Id == tool.Id)
                .Entries.Single(e => e.Name == "a.exe").DisplayName.Should().BeNull("empty alias clears it");
        }
        finally { Directory.Delete(src, true); }
    }

    [Fact]
    public async Task SetEnabled_UnknownTool_Throws()
    {
        Directory.CreateDirectory(_fixDir);
        var act = () => _service.SetEnabledAsync("does-not-exist", false);
        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("FIX_TOOL_NOT_FOUND");
    }

    // ===== Legacy seed (global {data}/fixtools → {profile}/fixtools, one-time) =====

    [Fact]
    public async Task GetAll_FirstAccess_SeedsFromLegacySharedFolder()
    {
        Directory.CreateDirectory(Path.Combine(_legacyDir, "OldTool"));
        File.WriteAllText(Path.Combine(_legacyDir, "OldTool", "fix.exe"), "x");
        File.WriteAllText(Path.Combine(_legacyDir, "loose.bat"), "x");

        var tools = await _service.GetAllAsync();

        tools.Select(t => t.Id).Should().BeEquivalentTo(["OldTool", "loose.bat"]);
        File.Exists(Path.Combine(_fixDir, "OldTool", "fix.exe")).Should().BeTrue("legacy tools are copied into the profile");
        Directory.Exists(_legacyDir).Should().BeTrue("the legacy folder is left untouched");
    }

    [Fact]
    public async Task GetAll_AfterProfileDirExists_DoesNotReseed()
    {
        Directory.CreateDirectory(_fixDir); // profile already initialized (even if empty)
        Directory.CreateDirectory(_legacyDir);
        File.WriteAllText(Path.Combine(_legacyDir, "late.bat"), "x");

        var tools = await _service.GetAllAsync();

        tools.Should().BeEmpty("dir-exists is the seeded marker — deleting all tools must not resurrect legacy ones");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_fixDir)) Directory.Delete(_fixDir, true); } catch { }
        try { if (Directory.Exists(_legacyDir)) Directory.Delete(_legacyDir, true); } catch { }
    }
}
