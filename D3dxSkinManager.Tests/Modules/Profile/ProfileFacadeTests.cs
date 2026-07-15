using System.Text.Json;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using FluentAssertions;
using Moq;
using Xunit;
using ProfileModel = D3dxSkinManager.Modules.Profiles.Models.Profile;

namespace D3dxSkinManager.Tests.Modules.Profile;

/// <summary>
/// Locks the ProfileFacade settings-bundle IPC contract: EXPORT_SETTINGS / IMPORT_SETTINGS must NOT
/// await the long export/import inside the handler (that blocks the bridge until timeout + freezes the
/// UI — background-task-tracking.md). They ack immediately (`{ started = true }`) and deliver the result
/// via PROFILE/EXPORT_SETTINGS_COMPLETE / IMPORT_SETTINGS_COMPLETE (a failed run still emits). A
/// successful import also fires CREATED so the profile list refreshes. ANALYZE_BUNDLE stays awaited.
/// </summary>
public class ProfileFacadeTests
{
    private readonly Mock<IProfileBundleService> _bundle = new();
    private readonly Mock<IProfileService> _profileService = new();
    private readonly Mock<IEventEmitter> _events = new();
    private readonly ProfileFacade _facade;

    public ProfileFacadeTests()
    {
        _events.Setup(e => e.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
            .Returns(Task.CompletedTask);

        _facade = new ProfileFacade(
            _profileService.Object,
            _bundle.Object,
            new PayloadHelper(),           // real payload parsing (config built from JSON)
            _events.Object,
            Mock.Of<IPathHelper>(),
            Mock.Of<IGlobalPathService>(),
            Mock.Of<ILogHelper>());
    }

    private static IpcRequest Req(string type, string? json = null) => new()
    {
        Id = "r1",
        Type = type,
        Module = "PROFILE",
        Payload = json == null ? null : JsonSerializer.Deserialize<JsonElement>(json),
    };

    [Fact]
    public async Task ExportSettings_IsFireAndForget_AcksImmediately_ThenEmitsCompleteWithResult()
    {
        var expected = new ProfileBundleExportResult { Success = true, OutputPath = "C:/out/My.zip", ProfileName = "My" };

        var gate = new TaskCompletionSource();
        _bundle.Setup(s => s.ExportAsync(It.IsAny<ProfileBundleExportConfig>()))
            .Returns(async () => { await gate.Task; return expected; });

        var emitted = new TaskCompletionSource<object?>();
        _events.Setup(e => e.EmitAsync(ModuleNames.PROFILE, ProfileEvents.EXPORT_SETTINGS_COMPLETE, It.IsAny<object?>()))
            .Callback<string, string, object?>((_, _, payload) => emitted.TrySetResult(payload))
            .Returns(Task.CompletedTask);

        var resp = await _facade.HandleMessageAsync(Req("EXPORT_SETTINGS", "{\"profileId\":\"p1\",\"outputPath\":\"C:/out\"}"));

        resp.Success.Should().BeTrue();
        emitted.Task.IsCompleted.Should().BeFalse("export runs in the background — nothing emitted until it finishes");

        gate.SetResult();
        var winner = await Task.WhenAny(emitted.Task, Task.Delay(2000));
        winner.Should().Be(emitted.Task);
        (await emitted.Task).Should().Be(expected);
    }

    [Fact]
    public async Task ImportSettings_IsFireAndForget_EmitsComplete_AndCreated()
    {
        var expected = new ProfileBundleImportResult { Success = true, NewProfileId = "new-id", ProfileName = "Imported" };
        _profileService.Setup(s => s.GetProfileByIdAsync("new-id"))
            .ReturnsAsync(new ProfileModel { Id = "new-id", Name = "Imported" });

        var gate = new TaskCompletionSource();
        _bundle.Setup(s => s.ImportAsync(It.IsAny<ProfileBundleImportConfig>()))
            .Returns(async () => { await gate.Task; return expected; });

        var completed = new TaskCompletionSource<object?>();
        _events.Setup(e => e.EmitAsync(ModuleNames.PROFILE, ProfileEvents.IMPORT_SETTINGS_COMPLETE, It.IsAny<object?>()))
            .Callback<string, string, object?>((_, _, payload) => completed.TrySetResult(payload))
            .Returns(Task.CompletedTask);
        var created = new TaskCompletionSource<object?>();
        _events.Setup(e => e.EmitAsync(ModuleNames.PROFILE, ProfileEvents.CREATED, It.IsAny<object?>()))
            .Callback<string, string, object?>((_, _, payload) => created.TrySetResult(payload))
            .Returns(Task.CompletedTask);

        var resp = await _facade.HandleMessageAsync(Req("IMPORT_SETTINGS", "{\"bundlePath\":\"C:/b.zip\"}"));

        resp.Success.Should().BeTrue();
        completed.Task.IsCompleted.Should().BeFalse("import runs in the background");

        gate.SetResult();
        var winner = await Task.WhenAny(completed.Task, Task.Delay(2000));
        winner.Should().Be(completed.Task);
        (await completed.Task).Should().Be(expected);
        (await Task.WhenAny(created.Task, Task.Delay(2000))).Should().Be(created.Task, "a successful import fires CREATED so the profile list refreshes");
    }

    [Fact]
    public async Task ImportSettings_WhenServiceThrows_StillEmitsCompleteWithFailedResult()
    {
        _bundle.Setup(s => s.ImportAsync(It.IsAny<ProfileBundleImportConfig>()))
            .ThrowsAsync(new System.InvalidOperationException("boom"));

        var completed = new TaskCompletionSource<object?>();
        _events.Setup(e => e.EmitAsync(ModuleNames.PROFILE, ProfileEvents.IMPORT_SETTINGS_COMPLETE, It.IsAny<object?>()))
            .Callback<string, string, object?>((_, _, payload) => completed.TrySetResult(payload))
            .Returns(Task.CompletedTask);

        var resp = await _facade.HandleMessageAsync(Req("IMPORT_SETTINGS", "{\"bundlePath\":\"C:/b.zip\"}"));

        resp.Success.Should().BeTrue("the IPC ack succeeds even though the background op fails");
        var winner = await Task.WhenAny(completed.Task, Task.Delay(2000));
        winner.Should().Be(completed.Task);
        (await completed.Task).Should().BeOfType<ProfileBundleImportResult>().Which.Success.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeBundle_StaysAwaited_ReturnsAnalysisDirectly()
    {
        var analysis = new ProfileBundleAnalysis { IsValid = true, ProfileName = "P" };
        _bundle.Setup(s => s.AnalyzeAsync("C:/b.zip")).ReturnsAsync(analysis);

        var resp = await _facade.HandleMessageAsync(Req("ANALYZE_BUNDLE", "{\"bundlePath\":\"C:/b.zip\"}"));

        resp.Success.Should().BeTrue();
        resp.Data.Should().Be(analysis);
        _bundle.Verify(s => s.AnalyzeAsync("C:/b.zip"), Times.Once);
    }
}
