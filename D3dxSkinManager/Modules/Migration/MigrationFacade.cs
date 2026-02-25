using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Services;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;

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
    private readonly ICategoryService _categoryService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IEventEmitter _eventEmitter;

    public MigrationFacade(
        IMigrationService migrationService,
        IModFacade modFacade,
        ICategoryService categoryService,
        IPayloadHelper payloadHelper,
        IEventEmitter eventEmitter,
        ILogHelper logger) : base(logger)
    {
        _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
        _modFacade = modFacade ?? throw new ArgumentNullException(nameof(modFacade));
        _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
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
        return await _migrationService.AnalyzeSourceAsync(pythonPath).ConfigureAwait(false);
    }

    public async Task<MigrationResult> StartMigrationAsync(MigrationOptions options, IProgress<MigrationProgress>? progress = null)
    {
        var result = await _migrationService.MigrateAsync(options, progress, CancellationToken.None).ConfigureAwait(false);

        // Emit event so frontend knows to reload Category tree
        // Note: Cache will automatically invalidate and rebuild on next request
        await _eventEmitter.EmitAsync(ModuleNames.MOD, ModEvents.CATEGORIES_UPDATED).ConfigureAwait(false);

        // Emit migration completed event
        await _eventEmitter.EmitAsync(ModuleNames.MIGRATION, MigrationEvents.COMPLETED, result).ConfigureAwait(false);

        // Also emit ModsRefreshed to trigger mod list reload
        await _eventEmitter.EmitAsync(ModuleNames.MOD, ModEvents.REFRESHED).ConfigureAwait(false);

        return result;
    }

    public async Task<bool> ValidateMigrationAsync(string pythonPath, string reactDataPath)
    {
        return await _migrationService.ValidateMigrationAsync(pythonPath, reactDataPath).ConfigureAwait(false);
    }

    private async Task<MigrationAnalysis> AnalyzeSourceAsync(IpcRequest request)
    {
        var pythonPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "pythonPath");
        return await AnalyzeSourceAsync(pythonPath).ConfigureAwait(false);
    }

    private async Task<MigrationResult> StartMigrationAsync(IpcRequest request)
    {
        var sourcePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourcePath");
        var environmentName = _payloadHelper.GetRequiredValue<string>(request.Payload, "environmentName");
        var migrateArchives = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migrateArchives") ?? true;
        var migrateMetadata = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migrateMetadata") ?? true;
        var migratePreviews = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migratePreviews") ?? true;
        var migrateConfiguration = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migrateConfiguration") ?? true;
        var migrateCategories = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "migrateCategories") ?? true;
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
            MigrateCategories = migrateCategories,
            ArchiveMode = archiveMode,
            PostAction = postAction
        };

        return await StartMigrationAsync(options).ConfigureAwait(false);
    }

    private async Task<bool> ValidateMigrationAsync(IpcRequest request)
    {
        var pythonPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "pythonPath");
        var reactDataPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "reactDataPath");
        return await ValidateMigrationAsync(pythonPath, reactDataPath).ConfigureAwait(false);
    }
}
