using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Cleanup.Steps;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Tests.Modules.Core.Cleanup;

/// <summary>
/// The topology migration renames the launcher to D3dxSkinManager.exe (runtime → lib/). This step
/// removes the orphaned OLD launcher, but ONLY once the new launcher is present — deleting a
/// pre-migration install's live launcher would brick it, so that guard is locked here.
/// </summary>
public class LegacyLauncherCleanupStepTests : IDisposable
{
    private const string OldLauncher = "D3dxSkinManager Launcher.exe";
    private const string NewLauncher = "D3dxSkinManager.exe";

    private readonly string _installDir;
    private readonly LegacyLauncherCleanupStep _step;

    public LegacyLauncherCleanupStepTests()
    {
        _installDir = Path.Combine(Path.GetTempPath(), "d3dx-legacy-launcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_installDir);

        var appEnv = new Mock<IAppEnvironment>();
        appEnv.Setup(e => e.BaseDirectory).Returns(_installDir);
        _step = new LegacyLauncherCleanupStep(appEnv.Object, new Mock<ILogHelper>().Object);
    }

    private string Path2(string name) => Path.Combine(_installDir, name);

    [Fact]
    public async Task Deletes_OldLauncher_WhenNewLauncherPresent_Migrated()
    {
        File.WriteAllText(Path2(OldLauncher), "old");
        File.WriteAllText(Path2(NewLauncher), "new");

        await _step.RunAsync();

        File.Exists(Path2(OldLauncher)).Should().BeFalse("the install has migrated — the old launcher is orphaned");
        File.Exists(Path2(NewLauncher)).Should().BeTrue("the new launcher must never be touched");
    }

    [Fact]
    public async Task Keeps_OldLauncher_WhenNewLauncherAbsent_NotYetMigrated()
    {
        // A pre-migration install: the old launcher is still the live entry point. Deleting it bricks it.
        File.WriteAllText(Path2(OldLauncher), "old");

        await _step.RunAsync();

        File.Exists(Path2(OldLauncher)).Should().BeTrue("without the new launcher this is the live launcher — do not delete");
    }

    [Fact]
    public async Task NoOp_WhenNeitherPresent()
    {
        var run = async () => await _step.RunAsync();
        await run.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        try { Directory.Delete(_installDir, recursive: true); } catch { }
    }
}
