using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace D3dxSkinManager.Composition;


/// <summary>
/// Manages profile-scoped service providers and routes messages to the appropriate profile context
/// </summary>
public class ProfileServiceRouter : IDisposable
{
    private readonly IServiceProvider _globalServices;
    private readonly ILogHelper _logger;
    private readonly ConcurrentDictionary<string, IServiceProvider> _profileServiceCache = new();
    private readonly ConcurrentDictionary<string, Func<IServiceProvider, IModuleFacade?>> _facadeResolvers = new();
    private readonly ConcurrentBag<Action<IServiceCollection>> _serviceConfigurations = new();

    private bool _disposed;

    public ProfileServiceRouter(IServiceProvider globalServices, ILogHelper logger)
    {
        _globalServices = globalServices;
        _logger = logger;
    }

    /// <summary>
    /// Register a facade resolver for a specific module with service configuration
    /// </summary>
    public ProfileServiceRouter MapFacade<TFacade>(
        string moduleName,
        Action<IServiceCollection> configureServices) where TFacade : IModuleFacade
    {
        var moduleKey = moduleName.ToUpperInvariant();

        // Register the facade resolver
        _facadeResolvers.TryAdd(moduleKey, services => services.GetService<TFacade>());

        // Store the service configuration to be called when creating profile services
        _serviceConfigurations.Add(configureServices);

        _logger.Debug($"Mapped facade for module: {moduleName}", "ProfileServiceRouter");
        return this;
    }


    /// <summary>
    /// Determine if a module requires ProfileContext by checking if it's registered in the facade resolvers
    /// </summary>
    public bool RequiresProfileContext(string module)
    {
        var moduleKey = module?.ToUpperInvariant();
        return moduleKey != null && _facadeResolvers.ContainsKey(moduleKey);
    }

    /// <summary>
    /// Get or create profile-scoped services for a specific profile
    /// </summary>
    public IServiceProvider GetProfileServices(string profileId)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProfileServiceRouter));

        if (string.IsNullOrEmpty(profileId))
            throw new ArgumentException("Profile ID is required for profile-scoped services", nameof(profileId));

        // Cache service providers per profile for efficiency
        return _profileServiceCache.GetOrAdd(profileId, CreateProfileServices);
    }

    /// <summary>
    /// Handle message for profile-scoped modules
    /// This acts as a middleware that routes to the appropriate profile context
    /// </summary>
    public async Task<IpcResponse?> HandleProfileMessageAsync(IpcRequest message, Func<Task<IpcResponse?>> next)
    {
        // Check if this module requires ProfileContext
        if (!RequiresProfileContext(message.Module))
        {
            // Not a profile-scoped module, pass to next middleware
            return await next();
        }

        // Extract ProfileId from message
        if (string.IsNullOrEmpty(message.ProfileId))
        {
            return IpcResponse.CreateError(message.Id,
                $"Profile ID is required for module: {message.Module}");
        }

        try
        {
            // Get profile-specific services
            var profileServices = GetProfileServices(message.ProfileId);

            // Get the appropriate facade from profile services
            var facade = GetFacadeForModule(profileServices, message.Module);

            if (facade == null)
            {
                // No facade found, pass to next middleware
                return await next();
            }

            // Handle with profile-scoped facade
            _logger.Debug($"Routing {message.Module}/{message.Type} to profile: {message.ProfileId}", "ProfileServiceRouter");
            return await facade.HandleMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling profile message: {ex.Message}", "ProfileServiceRouter", ex);
            return IpcResponse.CreateError(message.Id, $"Profile routing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the appropriate facade for a module from profile services using registered resolvers
    /// </summary>
    private IModuleFacade? GetFacadeForModule(IServiceProvider services, string moduleName)
    {
        var moduleKey = moduleName?.ToUpperInvariant();
        if (moduleKey != null && _facadeResolvers.TryGetValue(moduleKey, out var resolver))
        {
            return resolver(services);
        }
        return null;
    }

    /// <summary>
    /// Create profile-scoped service provider
    /// </summary>
    private IServiceProvider CreateProfileServices(string profileId)
    {
        var services = new ServiceCollection();

        // Get profile from global services
        var profileService = _globalServices.GetRequiredService<IProfileService>();
        var profile = profileService.GetProfileByIdAsync(profileId).GetAwaiter().GetResult();

        if (profile == null)
            throw new InvalidOperationException($"Profile not found: {profileId}");

        // Register core services and context
        services.AddSingleton(_globalServices.GetRequiredService<IAppEnvironment>());
        services.AddCoreServices(_globalServices);
        services.AddContextServices(profileId);

        // Apply all registered module service configurations
        foreach (var configureServices in _serviceConfigurations)
        {
            configureServices(services);
        }

        var serviceProvider = services.BuildServiceProvider();

        // Initialize ProfilePathService cache directory asynchronously
        var profilePathService = serviceProvider.GetService<IProfilePathService>();
        if (profilePathService != null)
        {
            // Load cache directory configuration asynchronously without blocking
            _ = profilePathService.LoadCacheDirectoryAsync();
        }

        _logger.Debug($"Created profile-scoped services for: {profile.Name} ({profile.Id})", "ProfileServiceRouter");

        return serviceProvider;
    }


    /// <summary>
    /// Invalidate cached services for a profile (e.g., after deletion)
    /// </summary>
    public void InvalidateProfileServices(string profileId)
    {
        if (_profileServiceCache.TryRemove(profileId, out var provider))
        {
            if (provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _logger.Debug($"Invalidated services for profile: {profileId}", "ProfileServiceRouter");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Dispose all profile-scoped service providers
            foreach (var kvp in _profileServiceCache)
            {
                if (kvp.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _profileServiceCache.Clear();

            _disposed = true;
        }
    }
}

public static class ProfileServiceExtensions
{
    /// <summary>
    /// Add profile routing middleware to the message dispatcher
    /// This middleware routes messages to profile-scoped services based on the ProfileId in the message
    /// </summary>
    public static MessageDispatcher UseProfileRouter(this MessageDispatcher dispatcher, ProfileServiceRouter profileRouter)
    {
        return dispatcher.Use(async (message, next) =>
        {
            if (message.ProfileId == null)
            {
                // No ProfileId, pass to next middleware
                return await next();
            }

            // Let ProfileServiceRouter handle profile-scoped messages
            return await profileRouter.HandleProfileMessageAsync(message, next);
        });
    }
}