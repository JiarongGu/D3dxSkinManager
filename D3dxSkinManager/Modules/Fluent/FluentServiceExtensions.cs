using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Fluent.Services;

namespace D3dxSkinManager.Modules.Fluent;

/// <summary>
/// Service registration extensions for Fluent migration module
/// Registers database migration services for profile-scoped dependency injection
/// </summary>
public static class FluentServiceExtensions
{
    /// <summary>
    /// Register Fluent migration services (profile-scoped)
    /// These services are instantiated per profile and use ProfileContext
    /// </summary>
    public static IServiceCollection AddFluentMigrationServices(this IServiceCollection services)
    {
        // Register migration runner - discovers and executes migrations using FluentMigrator
        services.TryAddSingleton<IMigrationRunner, MigrationRunner>();

        // Register database migration service - coordinates startup migrations
        services.TryAddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();
        return services;
    }
}
