using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using D3dxSkinManager.Modules.Migration.Services;
using D3dxSkinManager.Modules.Migration.Steps;
using D3dxSkinManager.Modules.Migration.Parsers;

namespace D3dxSkinManager.Modules.Migration;

/// <summary>
/// Service registration extensions for Migration module
/// Registers Python to React migration services, steps, and facade
/// </summary>
public static class MigrationServiceExtensions
{
    /// <summary>
    /// Register Migration module services, steps, and facade
    /// </summary>
    public static IServiceCollection AddMigrationServices(this IServiceCollection services)
    {
        Console.WriteLine("[MigrationFacade] Registering Migration services...");

        // Register parsers (parse Python files → return data structures)
        services.TryAddSingleton<IPythonConfigurationParser, PythonConfigurationParser>();
        services.TryAddSingleton<IPythonRedirectionFileParser, PythonRedirectionFileParser>();
        services.TryAddSingleton<IPythonCategoryFileParser, PythonCategoryFileParser>();
        services.TryAddSingleton<IPythonModIndexParser, PythonModIndexParser>();

        // Register migration steps (each step is a separate service)
        services.TryAddSingleton<MigrationStep1AnalyzeSource>();
        services.TryAddSingleton<MigrationStep2MigrateConfiguration>();
        services.TryAddSingleton<MigrationStep3MigrateCategories>();
        services.TryAddSingleton<MigrationStep4MigrateCategoryThumbnails>();
        services.TryAddSingleton<MigrationStep5MigrateModArchives>();
        services.TryAddSingleton<MigrationStep6MigrateModPreviews>();

        // Register migration service (step-based orchestrator)
        services.TryAddSingleton<IMigrationService, MigrationService>();

        // Register facade
        services.TryAddSingleton<IMigrationFacade, MigrationFacade>();
        services.TryAddSingleton<MigrationFacade>();

        Console.WriteLine("[MigrationFacade] Migration services registered");
        return services;
    }
}
