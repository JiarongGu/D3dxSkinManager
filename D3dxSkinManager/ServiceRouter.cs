using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Tools;
using D3dxSkinManager.Modules.Settings;
using D3dxSkinManager.Modules.SystemUtils;
using D3dxSkinManager.Modules.Launch;
using D3dxSkinManager.Modules.Migration;
using D3dxSkinManager.Modules.Plugins;
using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules;

namespace D3dxSkinManager;

/// <summary>
/// Service router for handling stateless API requests and message routing
///
/// Responsibilities:
/// - Routes requests to appropriate service providers (global vs profile-scoped)
/// - Handles message routing to module facades
/// - Manages service lifecycle and caching
///
/// Architecture:
/// - Global Services: Services that don't require ProfileContext (Settings, ProfileService)
/// - Profile-Scoped Services: Services that require ProfileContext (Mods, Tools, etc.)
/// - Each request gets routed to appropriate service provider and facade based on module
/// </summary>
public class ServiceRouter : IDisposable
{
    private readonly string _baseDataPath;
    private readonly IServiceProvider _globalServices;
    private readonly ConcurrentDictionary<string, IServiceProvider> _profileServiceCache = new();
    private bool _disposed;

    public ServiceRouter(string baseDataPath)
    {
        _baseDataPath = baseDataPath;

        // Create global services that don't need ProfileContext
        _globalServices = CreateGlobalServices();
    }

    /// <summary>
    /// Get global services (no ProfileContext needed)
    /// Used for: Settings, ProfileService, etc.
    /// </summary>
    public IServiceProvider GetGlobalServices()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServiceRouter));

        return _globalServices;
    }

    /// <summary>
    /// Get or create profile-scoped services for a request
    /// This is stateless - profileId comes from the request
    /// </summary>
    /// <param name="profileId">Profile ID from the request</param>
    public IServiceProvider GetProfileScopedServices(string profileId)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ServiceRouter));

        if (string.IsNullOrEmpty(profileId))
            throw new ArgumentException("Profile ID is required for profile-scoped services", nameof(profileId));

        // Cache service providers per profile for efficiency
        // But this is still stateless - no "active" profile
        return _profileServiceCache.GetOrAdd(profileId, id => CreateProfileScopedServices(id));
    }

    /// <summary>
    /// Handle an incoming message request
    /// Routes to the appropriate service provider and facade
    /// Checks for plugin handlers first before routing to module facades
    /// </summary>
    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        // Validate request
        if (string.IsNullOrEmpty(request.Module))
        {
            throw new InvalidOperationException(
                $"Module field is required for message routing. Message type: {request.Type}");
        }

        // Get the appropriate service provider based on module and profile
        var serviceProvider = RouteToServiceProvider(request.Module, request.ProfileId);

        // Check for plugin handlers first (plugins can override normal routing)
        var pluginRegistry = serviceProvider.GetService<IPluginRegistry>();
        if (pluginRegistry != null && pluginRegistry.CanHandleMessage(request.Type))
        {
            var handler = pluginRegistry.GetMessageHandler(request.Type);
            if (handler != null)
            {
                Console.WriteLine($"[ServiceRouter] Routing to plugin: {handler.Name}");
                return await handler.HandleMessageAsync(request);
            }
        }

        // Fall back to normal module facade routing
        var facade = GetFacadeForModule(serviceProvider, request.Module);

        if (facade == null)
        {
            throw new InvalidOperationException($"Unknown module: {request.Module}");
        }

        // Handle the request with the appropriate facade
        return await facade.HandleMessageAsync(request);
    }

    /// <summary>
    /// Route a request to the appropriate service provider
    /// Determines whether the request needs ProfileContext or not
    /// </summary>
    public IServiceProvider RouteToServiceProvider(string module, string? profileId)
    {
        // Determine which modules need ProfileContext
        var globalModules = new[] { "SETTINGS", "PROFILE", "SYSTEM" };

        if (globalModules.Contains(module?.ToUpperInvariant()))
        {
            // Route to global services (no ProfileContext needed)
            return GetGlobalServices();
        }
        else if (!string.IsNullOrEmpty(profileId))
        {
            // Route to profile-scoped services (ProfileContext required)
            return GetProfileScopedServices(profileId);
        }
        else
        {
            // Profile-scoped module but no profile ID provided
            throw new InvalidOperationException($"Profile ID is required for module: {module}");
        }
    }

    /// <summary>
    /// Get the appropriate facade for a given module from the service provider
    /// </summary>
    private IModuleFacade? GetFacadeForModule(IServiceProvider serviceProvider, string moduleName)
    {
        return moduleName.ToUpperInvariant() switch
        {
            "MOD" or "MODS" => serviceProvider.GetService<IModFacade>(),
            "LAUNCH" => serviceProvider.GetService<ILaunchFacade>(),
            "TOOLS" or "TOOL" => serviceProvider.GetService<IToolsFacade>(),
            "PLUGINS" or "PLUGIN" => serviceProvider.GetService<IPluginsFacade>(),
            "SETTINGS" or "SETTING" => serviceProvider.GetService<ISettingsFacade>(),
            "SYSTEM" => serviceProvider.GetService<ISystemFacade>(),
            "MIGRATION" => serviceProvider.GetService<IMigrationFacade>(),
            "PROFILE" or "PROFILES" => serviceProvider.GetService<IProfileFacade>(),
            _ => null
        };
    }

    /// <summary>
    /// Create global service provider (no ProfileContext)
    /// </summary>
    private IServiceProvider CreateGlobalServices()
    {
        var services = new ServiceCollection();

        // Core services needed by Settings and Profile facades
        services.AddCoreServices(_baseDataPath);

        // Profile service (manages profiles but doesn't need ProfileContext)
        services.AddSingleton<IProfileServiceProvider, ProfileServiceProvider>();
        services.AddSingleton<IProfileService, ProfileService>();

        // Settings services
        services.AddSettingsServices();

        // System services (file system, dialogs, paths)
        services.AddSystemServices();

        // Register facades for global services
        services.AddSingleton<IProfileFacade, ProfileFacade>();

        // No need for ModuleRouter - ServiceRouter handles routing directly

        Console.WriteLine($"[ServiceRouter] Created global services (no ProfileContext)");

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Create profile-scoped service provider (with ProfileContext)
    /// </summary>
    private IServiceProvider CreateProfileScopedServices(string profileId)
    {
        var services = new ServiceCollection();

        // Get profile from global services
        var profileService = _globalServices.GetRequiredService<IProfileServiceProvider>();
        var profile = profileService.GetProfileByIdAsync(profileId).Result;

        if (profile == null)
            throw new InvalidOperationException($"Profile not found: {profileId}");

        // Core services
        var pathHelper = new PathHelper(_baseDataPath);
        services.AddSingleton<IPathHelper>(pathHelper);

        // Get GlobalPathService from global services and use it
        var globalPaths = _globalServices.GetRequiredService<IGlobalPathService>();
        var profileDataPath = globalPaths.GetProfileDirectoryPath(profile.Id);

        // Create ProfileContext for this profile
        var profileContext = new ProfileContext(profile.Id, profileDataPath, profileService);
        services.AddSingleton<IProfileContext>(profileContext);

        // Register profile service (shared from global)
        services.AddSingleton(profileService);
        services.AddSingleton<IProfileService>(profileService);

        // Core services that may use ProfileContext
        services.AddCoreServices(_baseDataPath);

        // Module services that require ProfileContext
        services.AddModsServices();        // Uses ProfileContext for mod paths
        services.AddToolsServices();       // Uses ProfileContext for tools config
        services.AddLaunchServices();      // Uses ProfileContext for launch config
        services.AddMigrationServices();   // Uses ProfileContext for migration paths
        services.AddPluginsServices();     // Uses ProfileContext for plugin data

        // Register all facades for profile-scoped services
        services.AddProfilesServices();    // Includes ProfileFacade and IProfilePathService
        services.AddSettingsServices();    // Includes SettingsFacade
        services.AddSystemServices();      // Includes SystemFacade for file system, dialogs, paths

        // Profile-specific server
        services.AddSingleton<IProfileServerService, ProfileServerService>();

        // No need for ModuleRouter - ServiceRouter handles routing directly

        var serviceProvider = services.BuildServiceProvider();

        // Start profile-specific services if needed
        serviceProvider.GetService<IProfileServerService>()?.StartAsync().Wait();

        Console.WriteLine($"[ServiceRouter] Created profile-scoped services for: {profile.Name} ({profile.Id})");

        return serviceProvider;
    }

    /// <summary>
    /// Invalidate cached services for a profile (e.g., after deletion)
    /// </summary>
    public void InvalidateProfileServices(string profileId)
    {
        if (_profileServiceCache.TryRemove(profileId, out var provider))
        {
            // Cleanup profile-specific services
            provider.GetService<IProfileServerService>()?.Dispose();
            if (provider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Console.WriteLine($"[ServiceRouter] Invalidated services for profile: {profileId}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Dispose all profile-scoped service providers
            foreach (var kvp in _profileServiceCache)
            {
                kvp.Value.GetService<IProfileServerService>()?.Dispose();
                if (kvp.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _profileServiceCache.Clear();

            // Dispose global services
            if (_globalServices is IDisposable globalDisposable)
            {
                globalDisposable.Dispose();
            }

            _disposed = true;
        }
    }
}