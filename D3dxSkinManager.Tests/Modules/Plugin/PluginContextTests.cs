using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Tests.Modules.Plugin;

/// <summary>
/// PluginContext.GetPluginDataPath — a plugin's data dir is the folder its OWN DLL was loaded from
/// (the install dir), so a pack is ONE folder (dll + extracted natives together) instead of split
/// across {plugins}/{packId} (dll) and {plugins}/{pluginId} (natives). Also verifies the one-time
/// retirement of the legacy per-id dir.
/// </summary>
public class PluginContextTests : IDisposable
{
    private readonly string _dir;
    private readonly Mock<IProfilePathService> _paths = new();
    private readonly PluginRegistry _registry = new(Mock.Of<ILogHelper>());
    private readonly PluginContext _context;

    public PluginContextTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "d3dx-plugctx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _paths.Setup(p => p.PluginsDirectory).Returns(Path.Combine(_dir, "plugins"));
        _context = new PluginContext(
            Mock.Of<IMessageDispatcher>(),
            Mock.Of<IEventBus>(),
            _paths.Object,
            Mock.Of<IProcessRegistry>(),
            _registry,
            Mock.Of<ILogHelper>());
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void GetPluginDataPath_ReturnsTheDllDirectory_AndRetiresTheLegacyPerIdDir()
    {
        var stub = new StubPlugin { Id = "stub.plugin" };
        _registry.RegisterPlugin(stub);
        var expectedDir = Path.GetDirectoryName(stub.GetType().Assembly.Location)!;

        // A leftover legacy {plugins}/{pluginId} data dir from an older build.
        var legacyDir = Path.Combine(_paths.Object.PluginsDirectory, stub.Id);
        Directory.CreateDirectory(legacyDir);
        File.WriteAllText(Path.Combine(legacyDir, "onnxruntime.dll"), "stale");

        var result = _context.GetPluginDataPath(stub.Id);

        result.Should().Be(expectedDir, "data lives next to the plugin's own dll — one folder per pack");
        Directory.Exists(legacyDir).Should().BeFalse("the legacy per-id data dir is retired so the pack is a single folder");
    }

    private sealed class StubPlugin : IPlugin
    {
        public string Id { get; init; } = "stub";
        public string Name => "Stub";
        public string Version => "1.0";
        public string Description => string.Empty;
        public string Author => string.Empty;
        public Task InitAsync(IPluginContext context) => Task.CompletedTask;
        public IEnumerable<string> GetHandledMessageTypes() => Array.Empty<string>();
        public Task<IpcResponse> HandleMessageAsync(IpcRequest request) => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
