using Microsoft.Extensions.DependencyInjection;
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
        // Register parsers (parse Python files → return data structures)
        services.AddSingleton<IPythonConfigurationParser, PythonConfigurationParser>();
        services.AddSingleton<IPythonRedirectionFileParser, PythonRedirectionFileParser>();
        services.AddSingleton<IPythonClassificationFileParser, PythonClassificationFileParser>();
        services.AddSingleton<IPythonModIndexParser, PythonModIndexParser>();

        // Register migration steps (each step is a separate service)
        services.AddSingleton<MigrationStep1AnalyzeSource>();
        services.AddSingleton<MigrationStep2MigrateConfiguration>();
        services.AddSingleton<MigrationStep3MigrateClassifications>();
        services.AddSingleton<MigrationStep4MigrateClassificationThumbnails>();
        services.AddSingleton<MigrationStep5MigrateModArchives>();
        services.AddSingleton<MigrationStep6MigrateModPreviews>();

        // Register migration service (step-based orchestrator)
        services.AddSingleton<IMigrationService, MigrationService>();

        // Register facade
        services.AddSingleton<IMigrationFacade, MigrationFacade>();

        return services;
    }
}
