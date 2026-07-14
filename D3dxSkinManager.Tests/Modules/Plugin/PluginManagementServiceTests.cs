using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Tests.Modules.Plugin;

/// <summary>
/// PluginManagementService owns the enable/disable + list-mapping business logic that used to live in
/// PluginFacade (review finding: facade had plugin init + event-raising logic). The CRITICAL invariant
/// is the enable ordering: a never-initialized plugin must run <c>InitAsync</c> BEFORE the registry
/// flips it enabled — the registry's EnabledChanged fires on the flip and capability consumers (content
/// veil) react immediately, so the plugin has to be ready first. Uses the REAL PluginRegistry so the
/// ordering is observed against true registry behavior, not a mock's.
/// </summary>
public class PluginManagementServiceTests
{
    private static PluginRegistry Registry() => new(Mock.Of<ILogHelper>());

    private static PluginManagementService Service(
        PluginRegistry registry, IPluginStateStore? stateStore = null, IPluginContext? context = null)
        => new(registry, context ?? Mock.Of<IPluginContext>(), stateStore ?? Mock.Of<IPluginStateStore>(), Mock.Of<ILogHelper>());

    [Fact]
    public async Task SetEnabledAsync_EnablingUninitialized_InitsBeforeFlippingEnabled()
    {
        var order = new List<string>();
        var plugin = new FakePlugin("p1", onInit: () => order.Add("init"));
        var registry = Registry();
        registry.RegisterPlugin(plugin, enabled: false);
        registry.EnabledChanged += () => order.Add("setEnabled");
        var store = new Mock<IPluginStateStore>();

        await Service(registry, store.Object).SetEnabledAsync("p1", true);

        order.Should().Equal("init", "setEnabled"); // init strictly BEFORE the enabled flip
        plugin.InitCount.Should().Be(1);
        registry.GetEntry("p1")!.Initialized.Should().BeTrue();
        registry.GetEntry("p1")!.Enabled.Should().BeTrue();
        store.Verify(s => s.SetDisabled("p1", false), Times.Once);
    }

    [Fact]
    public async Task SetEnabledAsync_EnablingAlreadyInitialized_DoesNotReInit()
    {
        var plugin = new FakePlugin("p1");
        var registry = Registry();
        registry.RegisterPlugin(plugin, enabled: false);
        registry.GetEntry("p1")!.Initialized = true; // already initialized

        await Service(registry).SetEnabledAsync("p1", true);

        plugin.InitCount.Should().Be(0);
        registry.GetEntry("p1")!.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task SetEnabledAsync_Disabling_PersistsAndDisables_WithoutInit()
    {
        var plugin = new FakePlugin("p1");
        var registry = Registry();
        registry.RegisterPlugin(plugin, enabled: true);
        var store = new Mock<IPluginStateStore>();

        await Service(registry, store.Object).SetEnabledAsync("p1", false);

        plugin.InitCount.Should().Be(0);
        registry.GetEntry("p1")!.Enabled.Should().BeFalse();
        store.Verify(s => s.SetDisabled("p1", true), Times.Once);
    }

    [Fact]
    public async Task SetEnabledAsync_UnknownPlugin_ThrowsPluginNotFound()
    {
        var service = Service(Registry());

        var act = () => service.SetEnabledAsync("missing", true);

        (await act.Should().ThrowAsync<OperationException>())
            .Which.Code.Should().Be("PLUGIN_NOT_FOUND");
    }

    [Fact]
    public void GetAllPlugins_MapsEntries_IncludingEnabledStateAndCapabilities()
    {
        var registry = Registry();
        registry.RegisterPlugin(new FakePlugin("plain"), enabled: true);
        registry.RegisterPlugin(new FakePlugin("msg", msgTypes: new[] { "OPEN_UI" }), enabled: false);
        registry.RegisterPlugin(new FakeImageReviewPlugin("review"), enabled: true);

        var infos = Service(registry).GetAllPlugins();

        infos.Should().HaveCount(3);
        var plain = infos.Single(i => i.Id == "plain");
        plain.IsEnabled.Should().BeTrue();
        plain.Name.Should().Be("plain-name");
        plain.Capabilities.Should().BeEmpty();

        var msg = infos.Single(i => i.Id == "msg");
        msg.IsEnabled.Should().BeFalse();
        msg.Capabilities.Should().Contain("MessageHandler");

        var review = infos.Single(i => i.Id == "review");
        review.Capabilities.Should().Contain("ImageReview");
    }

    // ---- fakes --------------------------------------------------------------

    private class FakePlugin : IPlugin
    {
        private readonly Action? _onInit;
        private readonly string[] _msgTypes;

        public FakePlugin(string id, Action? onInit = null, string[]? msgTypes = null)
        {
            Id = id;
            _onInit = onInit;
            _msgTypes = msgTypes ?? Array.Empty<string>();
        }

        public int InitCount { get; private set; }
        public string Id { get; }
        public string Name => Id + "-name";
        public string Version => "1.0";
        public string Description => "desc";
        public string Author => "author";

        public Task InitAsync(IPluginContext context)
        {
            InitCount++;
            _onInit?.Invoke();
            return Task.CompletedTask;
        }

        public IEnumerable<string> GetHandledMessageTypes() => _msgTypes;
        public Task<IpcResponse> HandleMessageAsync(IpcRequest request) => Task.FromResult(new IpcResponse());
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeImageReviewPlugin : FakePlugin, IImageReviewPlugin
    {
        public FakeImageReviewPlugin(string id) : base(id) { }
        public Task<bool?> ReviewImageAsync(ImageReviewContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<bool?>(null);
    }
}
