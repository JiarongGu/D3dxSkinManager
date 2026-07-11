using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Plugin;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Tests.Modules.Plugin;

/// <summary>
/// PluginFacade IPC routing. Focus: GET_DIRECTORY ENSURES the plugins directory exists — the
/// "Open plugins folder" button opens the returned path, and a profile that predates the plugin
/// system (or a partial migration) can lack the dir. Regression: the folder opener threw
/// "File not found" because the frontend used the FILE opener (File.Exists is false for a dir);
/// the frontend now opens the directory, and the backend guarantees the target exists.
/// </summary>
public class PluginFacadeTests : IDisposable
{
    private readonly string _dir;
    private readonly Mock<IProfilePathService> _paths = new();
    private readonly PluginFacade _facade;

    public PluginFacadeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-pluginfacade-" + Guid.NewGuid().ToString("N"));
        _paths.Setup(p => p.PluginsDirectory).Returns(Path.Combine(_dir, "plugins"));

        _facade = new PluginFacade(
            Mock.Of<IPluginRegistry>(),
            Mock.Of<IPluginLoader>(),
            Mock.Of<IPluginContext>(),
            Mock.Of<IPluginStateStore>(),
            Mock.Of<IPluginInstallService>(),
            _paths.Object,
            new PayloadHelper(),
            Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    private static IpcRequest Req(string type) => new()
    {
        Id = "req-1",
        Type = type,
        Module = "PLUGIN",
    };

    [Fact]
    public async Task GetDirectory_CreatesTheDirIfMissing_AndReturnsIt()
    {
        var expected = _paths.Object.PluginsDirectory;
        Directory.Exists(expected).Should().BeFalse("precondition: the plugins dir does not exist yet");

        var resp = await _facade.HandleMessageAsync(Req("GET_DIRECTORY"));

        resp.Success.Should().BeTrue();
        Directory.Exists(expected).Should().BeTrue("GET_DIRECTORY must ensure the folder exists so the opener never fails");
        resp.Data.Should().NotBeNull();
        resp.Data!.GetType().GetProperty("path")!.GetValue(resp.Data).Should().Be(expected);
    }
}
