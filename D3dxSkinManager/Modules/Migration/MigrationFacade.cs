using System;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Facades;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Services;
using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Mods;

namespace D3dxSkinManager.Modules.Migration;

/// <summary>
/// Interface for Migration facade
/// Handles: MIGRATION_ANALYZE, MIGRATION_START, MIGRATION_VALIDATE
/// Prefix: MIGRATION_*
/// </summary>
public interface IMigrationFacade : IModuleFacade
{
    Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath);
    Task<MigrationResult> StartMigrationAsync(MigrationOptions options, IProgress<MigrationProgress>? progress = null);
    Task<bool> ValidateMigrationAsync(string pythonPath, string reactDataPath);
}

/// <summary>
/// Facade for migration operations
/// Responsibility: Python to React migration functionality
/// IPC Prefix: MIGRATION_*
/// </summary>
public class MigrationFacade : BaseFacade, IMigrationFacade
{
    protected override string ModuleName => "MigrationFacade";

    private readonly IMigrationService _migrationService;
    private readonly IModFacade _modFacade;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IEventEmitterHelper _eventEmitter;

    public MigrationFacade(
        IMigrationService migrationService,
        IModFacade modFacade,
        IPayloadHelper payloadHelper,
        IEventEmitterHelper eventEmitter,
        ILogHelper logger) : base(logger)
    {
        _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
        _modFacade = modFacade ?? throw new ArgumentNullException(nameof(modFacade));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
    }

    protected override async Task<object?> RouteMessageAsync(MessageRequest request)
    {
        return request.Type switch
        {
            "ANALYZE" => await AnalyzeSourceAsync(request),
            "START" => await StartMigrationAsync(request),
            "MIGRATION_VALIDATE" => await ValidateMigrationAsync(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath)
    {
        return await _migrationService.AnalyzeSourceAsync(pythonPath);
    }

    public async Task<MigrationResult> StartMigrationAsync(MigrationOptions options, IProgress<MigrationProgress>? progress = null)
    {
        var result = await _migrationService.MigrateAsync(options, progress, CancellationToken.None);

        // Refresh classification tree cache after migration
        try
        {
            _logger.Info("Refreshing classification tree after migration", "MigrationFacade");
            await _modFacade.RefreshClassificationTreeAsync();
            _logger.Info("Classification tree refreshed successfully", "MigrationFacade");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to refresh classification tree: {ex.Message}", "MigrationFacade", ex);
        }

        await _eventEmitter.EmitAsync(PluginEventType.CustomEvent, "migration.completed", result);

        return result;
    }

    public async Task<bool> ValidateMigrationAsync(string pythonPath, string reactDataPath)
    {
        return await _migrationService.ValidateMigrationAsync(pythonPath, reactDataPath);
    }

    private async Task<MigrationAnalysis> AnalyzeSourceAsync(MessageRequest request)
    {
        var pythonPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "pythonPath");
        return await AnalyzeSourceAsync(pythonPath);
    }

    private async Task<MigrationResult> StartMigrationAsync(MessageRequest request)
    {
        var sourcePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourcePath");
        var environmentName = _payloadHelper.GetRequiredValue<string>(request.Payload, "environmentName");
        var migrateArchives = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migrateArchives") ?? true;
        var migrateMetadata = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migrateMetadata") ?? true;
        var migratePreviews = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migratePreviews") ?? true;
        var migrateConfiguration = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migrateConfiguration") ?? true;
        var migrateClassifications = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migrateClassifications") ?? true;
        var archiveModeString = _payloadHelper.GetOptionalValue<string>(request.Payload, "archiveMode") ?? "Copy";
        var postActionString = _payloadHelper.GetOptionalValue<string>(request.Payload, "postAction") ?? "Keep";

        if (!Enum.TryParse<ArchiveHandling>(archiveModeString, true, out var archiveMode))
        {
            throw new ArgumentException($"Invalid archive handling mode: {archiveModeString}");
        }

        if (!Enum.TryParse<PostMigrationAction>(postActionString, true, out var postAction))
        {
            throw new ArgumentException($"Invalid post migration action: {postActionString}");
        }

        var options = new MigrationOptions
        {
            SourcePath = sourcePath,
            EnvironmentName = environmentName,
            MigrateArchives = migrateArchives,
            MigrateMetadata = migrateMetadata,
            MigratePreviews = migratePreviews,
            MigrateConfiguration = migrateConfiguration,
            MigrateClassifications = migrateClassifications,
            ArchiveMode = archiveMode,
            PostAction = postAction
        };

        return await StartMigrationAsync(options);
    }

    private async Task<bool> ValidateMigrationAsync(MessageRequest request)
    {
        var pythonPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "pythonPath");
        var reactDataPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "reactDataPath");
        return await ValidateMigrationAsync(pythonPath, reactDataPath);
    }
}
