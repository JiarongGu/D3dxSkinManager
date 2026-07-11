using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Tests.Modules.Plugin;

/// <summary>
/// The registry raises <see cref="IPluginRegistry.EnabledChanged"/> on a plugin toggle — the seam the
/// content veil hooks to drop its verdict cache when an image-review plugin turns on/off (the verdict
/// logic flips plugin↔CV). Event wiring is silent at runtime, so it's locked here.
/// </summary>
public class PluginRegistryTests
{
    private static PluginRegistry NewRegistry() => new(new Mock<ILogHelper>().Object);

    [Fact]
    public void SetEnabled_Toggle_RaisesEnabledChanged_AndFlipsVisibility()
    {
        var reg = NewRegistry();
        reg.RegisterPlugin(new StubPlugin { Id = "p" }, enabled: true);
        var raised = 0;
        reg.EnabledChanged += () => raised++;

        reg.SetEnabled("p", false);
        raised.Should().Be(1);
        reg.GetPlugin("p").Should().BeNull("a disabled plugin is invisible to consumers");

        reg.SetEnabled("p", true);
        raised.Should().Be(2);
        reg.GetPlugin("p").Should().NotBeNull();
    }

    [Fact]
    public void SetEnabled_NoActualChange_DoesNotRaise()
    {
        var reg = NewRegistry();
        reg.RegisterPlugin(new StubPlugin { Id = "p" }, enabled: true);
        var raised = 0;
        reg.EnabledChanged += () => raised++;

        reg.SetEnabled("p", true); // already enabled — no-op
        raised.Should().Be(0);
    }

    [Fact]
    public void SetEnabled_UnknownId_NoThrow_NoRaise()
    {
        var reg = NewRegistry();
        var raised = 0;
        reg.EnabledChanged += () => raised++;

        var act = () => reg.SetEnabled("missing", false);
        act.Should().NotThrow();
        raised.Should().Be(0);
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
