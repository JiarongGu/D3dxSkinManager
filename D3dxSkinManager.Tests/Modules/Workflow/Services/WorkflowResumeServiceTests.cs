using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Workflow;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Services;

namespace D3dxSkinManager.Tests.Modules.Workflow.Services;

/// <summary>
/// WorkflowResumeService — orphaned import-temp cleanup. The pure selector is tested directly; an
/// integration test runs the real cleanup over a temp dir with a mocked repo/paths, proving crash
/// leftovers are swept while an ACTIVE workflow's compress temp is retained (so its resume still
/// finds the archive).
/// </summary>
public class WorkflowResumeServiceTests : IDisposable
{
    private readonly string _tempDir;

    public WorkflowResumeServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "d3dx-resume-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
    }

    // ---- pure selector ----------------------------------------------------------------------

    [Fact]
    public void SelectOrphanTempEntries_KeepsActiveMic_DeletesOrphanMic_Auc_And_RemoteDirs()
    {
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "wf-active" };
        var entries = new[]
        {
            new TempEntry("/t/wf-active.mic", "wf-active.mic", false),   // KEEP — active workflow
            new TempEntry("/t/wf-gone.mic", "wf-gone.mic", false),      // delete — no active workflow
            new TempEntry("/t/some-mod.auc", "some-mod.auc", false),    // delete — archive-update temp
            new TempEntry("/t/remote-abc", "remote-abc", true),         // delete — remote staging dir
            new TempEntry("/t/notes.txt", "notes.txt", false),          // keep — unrelated file
            new TempEntry("/t/keepdir", "keepdir", true),               // keep — unrelated dir
        };

        var toDelete = WorkflowResumeService.SelectOrphanTempEntries(entries, active);

        toDelete.Should().BeEquivalentTo(new[] { "/t/wf-gone.mic", "/t/some-mod.auc", "/t/remote-abc" });
    }

    [Fact]
    public void SelectOrphanTempEntries_NoActiveWorkflows_DeletesAllMic()
    {
        var entries = new[] { new TempEntry("/t/a.mic", "a.mic", false), new TempEntry("/t/b.mic", "b.mic", false) };
        var toDelete = WorkflowResumeService.SelectOrphanTempEntries(entries, new HashSet<string>());
        toDelete.Should().BeEquivalentTo(new[] { "/t/a.mic", "/t/b.mic" });
    }

    // ---- integration over a real temp dir ---------------------------------------------------

    [Fact]
    public async Task CleanupOrphanedImportTempAsync_SweepsLeftovers_KeepsActiveWorkflowTemp()
    {
        // Seed the temp dir with a mix of leftovers.
        File.WriteAllText(Path.Combine(_tempDir, "active-wf.mic"), "x");   // keep (active)
        File.WriteAllText(Path.Combine(_tempDir, "dead-wf.mic"), "x");     // delete (orphan)
        File.WriteAllText(Path.Combine(_tempDir, "mod123.auc"), "x");      // delete
        Directory.CreateDirectory(Path.Combine(_tempDir, "remote-xyz"));   // delete
        File.WriteAllText(Path.Combine(_tempDir, "remote-xyz", "f"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "user.txt"), "x");        // keep

        var repo = new Mock<IWorkflowRepository>();
        repo.Setup(r => r.GetActiveByTypeAsync("MOD_IMPORT"))
            .ReturnsAsync(new List<WorkflowInfo> { new() { Id = "active-wf", Type = "MOD_IMPORT" } });

        var handler = new Mock<IWorkflowHandler>();
        handler.SetupGet(h => h.WorkflowType).Returns("MOD_IMPORT");

        var paths = new Mock<IProfilePathService>();
        paths.SetupGet(p => p.TempDirectory).Returns(_tempDir);

        var svc = new WorkflowResumeService(repo.Object, new[] { handler.Object }, paths.Object, Mock.Of<ILogHelper>());

        await svc.CleanupOrphanedImportTempAsync();

        File.Exists(Path.Combine(_tempDir, "active-wf.mic")).Should().BeTrue("active workflow's compress temp is retained for resume");
        File.Exists(Path.Combine(_tempDir, "user.txt")).Should().BeTrue("unrelated files are left alone");
        File.Exists(Path.Combine(_tempDir, "dead-wf.mic")).Should().BeFalse("orphaned .mic is swept");
        File.Exists(Path.Combine(_tempDir, "mod123.auc")).Should().BeFalse("archive-update temp is swept");
        Directory.Exists(Path.Combine(_tempDir, "remote-xyz")).Should().BeFalse("remote staging dir is swept");
    }
}
