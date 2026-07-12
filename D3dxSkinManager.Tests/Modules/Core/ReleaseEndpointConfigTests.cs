using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Core;

/// <summary>
/// ReleaseEndpointConfig — resolves the app-update + plugin-catalog LOCATIONS from an optional
/// data/settings/endpoints.json over shipped defaults. Tests the pure overlay (no file/network), so it
/// doubles as the offline guarantee: with no overrides, every consumer gets a usable default location.
/// </summary>
public class ReleaseEndpointConfigTests
{
    [Fact]
    public void NoOverrides_UsesShippedDefaults()
    {
        var cfg = new ReleaseEndpointConfig((EndpointConfig?)null);

        cfg.AppReleaseApi.Should().Be(ReleaseEndpointConfig.DefaultAppReleaseApi);
        cfg.AppDownloadBase.Should().Be(ReleaseEndpointConfig.DefaultAppDownloadBase);
        cfg.PluginReleaseApi.Should().Be(ReleaseEndpointConfig.DefaultPluginReleaseApi);
        cfg.PluginDownloadPrefix.Should().Be(ReleaseEndpointConfig.DefaultPluginDownloadPrefix);
        cfg.PluginManifestAsset.Should().Be(ReleaseEndpointConfig.DefaultPluginManifestAsset);
    }

    [Fact]
    public void Overrides_ApplyPerField_RestStayDefault()
    {
        var cfg = new ReleaseEndpointConfig(new EndpointConfig
        {
            PluginReleaseApi = "https://api.github.com/repos/acme/mirror/releases/latest",
            PluginDownloadPrefix = "https://github.com/acme/mirror/releases/download/",
        });

        cfg.PluginReleaseApi.Should().Be("https://api.github.com/repos/acme/mirror/releases/latest");
        cfg.PluginDownloadPrefix.Should().Be("https://github.com/acme/mirror/releases/download/");
        // Untouched fields keep the shipped defaults.
        cfg.AppReleaseApi.Should().Be(ReleaseEndpointConfig.DefaultAppReleaseApi);
        cfg.AppDownloadBase.Should().Be(ReleaseEndpointConfig.DefaultAppDownloadBase);
        cfg.PluginManifestAsset.Should().Be(ReleaseEndpointConfig.DefaultPluginManifestAsset);
    }

    [Fact]
    public void BlankOrWhitespaceOverride_FallsBackToDefault()
    {
        var cfg = new ReleaseEndpointConfig(new EndpointConfig { AppReleaseApi = "   " });

        cfg.AppReleaseApi.Should().Be(ReleaseEndpointConfig.DefaultAppReleaseApi);
    }

    [Fact]
    public void Layers_ResolvePerField_HighestPriorityFirst()
    {
        var operatorOverride = new EndpointConfig { PluginReleaseApi = "https://data/api" };          // data/settings
        var shippedDefault = new EndpointConfig { PluginReleaseApi = "https://res/api", AppReleaseApi = "https://res/app" }; // res

        var cfg = new ReleaseEndpointConfig(operatorOverride, shippedDefault); // override first, then shipped

        cfg.PluginReleaseApi.Should().Be("https://data/api", "the operator override wins");
        cfg.AppReleaseApi.Should().Be("https://res/app", "the shipped res default applies where the override is silent");
        cfg.AppDownloadBase.Should().Be(ReleaseEndpointConfig.DefaultAppDownloadBase, "neither layer set it → code fallback");
    }

    [Fact]
    public void ResolvesThroughDI_ViaAnnotatedConstructor()
    {
        // The class has two ctors; [ActivatorUtilitiesConstructor] must make MS DI pick the
        // (IGlobalPathService, ILogHelper) one unambiguously. Mock paths are null → no files → constants.
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IGlobalPathService>());
        services.AddSingleton(Mock.Of<ILogHelper>());
        services.AddSingleton<IReleaseEndpointConfig, ReleaseEndpointConfig>();
        using var sp = services.BuildServiceProvider();

        var cfg = sp.GetRequiredService<IReleaseEndpointConfig>();
        cfg.AppReleaseApi.Should().Be(ReleaseEndpointConfig.DefaultAppReleaseApi);
        cfg.PluginReleaseApi.Should().Be(ReleaseEndpointConfig.DefaultPluginReleaseApi);
    }
}
