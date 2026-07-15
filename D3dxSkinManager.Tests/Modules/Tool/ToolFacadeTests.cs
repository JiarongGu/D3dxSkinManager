using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Tool;
using D3dxSkinManager.Modules.Tool.ScreenCapture.Services;
using D3dxSkinManager.Modules.Tool.ModPackage.Models;
using D3dxSkinManager.Modules.Tool.ModPackage.Services;
using D3dxSkinManager.Modules.Tool.Services;
using D3dxSkinManager.Modules.Mod.Services;   // IModCacheService lives in the Mod module
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Tests.Modules.Tool;

/// <summary>
/// Tests the ToolFacade mod-package IPC contract after the fire-and-forget fix (code-review H+M):
/// MOD_PACKAGE_EXPORT / MOD_PACKAGE_IMPORT must NOT await the long export/import inside the handler
/// (that blocks the bridge until it times out + freezes the UI — background-task-tracking.md). Instead
/// the handler acks immediately (`{ started = true }`) and the ExportResult/ImportResult is delivered
/// via TOOL/MOD_PACKAGE_EXPORT_COMPLETE / MOD_PACKAGE_IMPORT_COMPLETE. Analyze is quick and STAYS awaited.
/// </summary>
public class ToolFacadeTests
{
    private readonly Mock<IModPackageService> _pkg = new();
    private readonly Mock<IProfileEventBus> _eventBus = new();
    private readonly ToolFacade _facade;

    public ToolFacadeTests()
    {
        _eventBus.Setup(b => b.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
            .Returns(Task.CompletedTask);

        _facade = new ToolFacade(
            Mock.Of<IModCacheService>(),
            Mock.Of<IScreenCaptureProfileService>(),
            Mock.Of<IScreenCaptureService>(),
            _pkg.Object,
            Mock.Of<IFileCleanupService>(),
            Mock.Of<IModAnalysisService>(),
            Mock.Of<IModIdMigrationService>(),
            Mock.Of<IModFixService>(),
            Mock.Of<IModFixToolService>(),
            Mock.Of<IFixToolsWatcher>(),
            Mock.Of<IAnalyzerWindowService>(),
            new PayloadHelper(),          // real payload parsing (config built from JSON)
            _eventBus.Object,
            Mock.Of<ILogHelper>());
    }

    private static IpcRequest Req(string type, string? json = null) => new()
    {
        Id = "r1",
        Type = type,
        Module = "TOOL",
        Payload = json == null ? null : JsonSerializer.Deserialize<JsonElement>(json),
    };

    [Fact]
    public async Task Export_IsFireAndForget_AcksImmediately_ThenEmitsCompleteWithResult()
    {
        var expected = new ExportResult { Success = true, ExportedCount = 2, OutputPath = "C:/out/pkg" };

        // Gate the service so it is still running when the handler returns — proves the handler
        // does NOT await the long op.
        var gate = new TaskCompletionSource();
        _pkg.Setup(s => s.ExportAsync(It.IsAny<ExportConfig>()))
            .Returns(async () => { await gate.Task; return expected; });

        var emitted = new TaskCompletionSource<object?>();
        _eventBus.Setup(b => b.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_PACKAGE_EXPORT_COMPLETE, It.IsAny<object?>()))
            .Callback<string, string, object?>((_, _, payload) => emitted.TrySetResult(payload))
            .Returns(Task.CompletedTask);

        var json = "{\"packageName\":\"Pkg\",\"outputPath\":\"C:/out\",\"modIds\":[\"m1\",\"m2\"]}";
        var resp = await _facade.HandleMessageAsync(Req("MOD_PACKAGE_EXPORT", json));

        resp.Success.Should().BeTrue();
        emitted.Task.IsCompleted.Should().BeFalse("export runs in the background — nothing is emitted until it finishes");

        // Let the background op finish → the completion event must fire with the service's result.
        gate.SetResult();
        var winner = await Task.WhenAny(emitted.Task, Task.Delay(2000));
        winner.Should().Be(emitted.Task, "the completion event must fire once the background export finishes");
        (await emitted.Task).Should().Be(expected);
    }

    [Fact]
    public async Task Import_IsFireAndForget_AcksImmediately_ThenEmitsCompleteWithResult()
    {
        var expected = new ImportResult { ImportedCount = 2 };

        var gate = new TaskCompletionSource();
        _pkg.Setup(s => s.ImportAsync(It.IsAny<ImportConfig>()))
            .Returns(async () => { await gate.Task; return expected; });

        var emitted = new TaskCompletionSource<object?>();
        _eventBus.Setup(b => b.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_PACKAGE_IMPORT_COMPLETE, It.IsAny<object?>()))
            .Callback<string, string, object?>((_, _, payload) => emitted.TrySetResult(payload))
            .Returns(Task.CompletedTask);

        var json = "{\"packagePath\":\"C:/pkg\",\"selectedModIds\":[\"m1\"]}";
        var resp = await _facade.HandleMessageAsync(Req("MOD_PACKAGE_IMPORT", json));

        resp.Success.Should().BeTrue();
        emitted.Task.IsCompleted.Should().BeFalse("import runs in the background — nothing is emitted until it finishes");

        gate.SetResult();
        var winner = await Task.WhenAny(emitted.Task, Task.Delay(2000));
        winner.Should().Be(emitted.Task, "the completion event must fire once the background import finishes");
        (await emitted.Task).Should().Be(expected);
    }

    [Fact]
    public async Task Export_WhenServiceThrows_StillEmitsCompleteWithFailedResult()
    {
        _pkg.Setup(s => s.ExportAsync(It.IsAny<ExportConfig>()))
            .ThrowsAsync(new System.InvalidOperationException("boom"));

        var emitted = new TaskCompletionSource<object?>();
        _eventBus.Setup(b => b.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_PACKAGE_EXPORT_COMPLETE, It.IsAny<object?>()))
            .Callback<string, string, object?>((_, _, payload) => emitted.TrySetResult(payload))
            .Returns(Task.CompletedTask);

        var json = "{\"packageName\":\"Pkg\",\"outputPath\":\"C:/out\",\"modIds\":[\"m1\"]}";
        var resp = await _facade.HandleMessageAsync(Req("MOD_PACKAGE_EXPORT", json));

        resp.Success.Should().BeTrue("the IPC ack succeeds even though the background op will fail");
        var winner = await Task.WhenAny(emitted.Task, Task.Delay(2000));
        winner.Should().Be(emitted.Task, "a failed background export must still emit a completion event so the UI leaves the running state");
        (await emitted.Task).Should().BeOfType<ExportResult>()
            .Which.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Analyze_StaysAwaited_ReturnsAnalysisDirectly()
    {
        var analysis = new PackageAnalysis { IsValid = true, PackageName = "P" };
        _pkg.Setup(s => s.AnalyzePackageAsync("C:/pkg")).ReturnsAsync(analysis);

        var resp = await _facade.HandleMessageAsync(Req("MOD_PACKAGE_ANALYZE", "{\"packagePath\":\"C:/pkg\"}"));

        resp.Success.Should().BeTrue();
        resp.Data.Should().Be(analysis);
        _pkg.Verify(s => s.AnalyzePackageAsync("C:/pkg"), Times.Once);
    }
}
