using Microsoft.Extensions.DependencyInjection;

namespace D3dxSkinManager.Modules.Plugins.Services;

/// <summary>
/// Plugin interface for registering custom services into the DI container.
/// Allows plugins to extend the service layer with custom implementations.
/// </summary>
public interface IServicePlugin : IPlugin
{
    /// <summary>
    /// Register plugin services into the DI container.
    /// Called before InitializeAsync during application startup.
    /// </summary>
    /// <param name="services">Service collection to register services into</param>
    void ConfigureServices(IServiceCollection services);
}
