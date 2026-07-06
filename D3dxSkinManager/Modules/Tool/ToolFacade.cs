using D3dxSkinManager.Modules.Tool.ScreenCapture.Models;
using D3dxSkinManager.Modules.Tool.ScreenCapture.Services;
using D3dxSkinManager.Modules.Tool.ModPackage.Models;
using D3dxSkinManager.Modules.Tool.ModPackage.Services;
using D3dxSkinManager.Modules.Tool.Models;
using D3dxSkinManager.Modules.Tool.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mod.Services;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Tool;

/// <summary>
/// Interface for Tools facade
/// Module: TOOL
/// Handles: SCAN_CACHE, CLEAN_CACHE, screen capture, etc.
/// </summary>
public interface IToolFacade : IModuleFacade { }

/// <summary>
/// Facade for tools and utilities
/// Module: TOOL
/// Responsibility: Cache management, validation, diagnostics
/// </summary>
public class ToolFacade : BaseFacade, IToolFacade
{
    protected override string ModuleName => "ToolsFacade";

    private readonly IModCacheService _cacheService;
    private readonly IScreenCaptureProfileRepository _captureProfileRepository;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly IModPackageService _modPackageService;
    private readonly IFileCleanupService _fileCleanupService;
    private readonly IModAnalysisService _modAnalysisService;
    private readonly IModIdMigrationService _modIdMigrationService;
    private readonly IModFixService _modFixService;
    private readonly IModFixToolService _modFixToolService;
    private readonly IFixToolsWatcher _fixToolsWatcher;
    private readonly IAnalyzerWindowService _analyzerWindowService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IProfileEventBus _eventBus;

    public ToolFacade(
        IModCacheService cacheService,
        IScreenCaptureProfileRepository captureProfileRepository,
        IScreenCaptureService screenCaptureService,
        IModPackageService modPackageService,
        IFileCleanupService fileCleanupService,
        IModAnalysisService modAnalysisService,
        IModIdMigrationService modIdMigrationService,
        IModFixService modFixService,
        IModFixToolService modFixToolService,
        IFixToolsWatcher fixToolsWatcher,
        IAnalyzerWindowService analyzerWindowService,
        IPayloadHelper payloadHelper,
        IProfileEventBus eventBus,
        ILogHelper logger) : base(logger)
    {
        _analyzerWindowService = analyzerWindowService ?? throw new ArgumentNullException(nameof(analyzerWindowService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _captureProfileRepository = captureProfileRepository ?? throw new ArgumentNullException(nameof(captureProfileRepository));
        _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
        _modPackageService = modPackageService ?? throw new ArgumentNullException(nameof(modPackageService));
        _fileCleanupService = fileCleanupService ?? throw new ArgumentNullException(nameof(fileCleanupService));
        _modAnalysisService = modAnalysisService ?? throw new ArgumentNullException(nameof(modAnalysisService));
        _modIdMigrationService = modIdMigrationService ?? throw new ArgumentNullException(nameof(modIdMigrationService));
        _modFixService = modFixService ?? throw new ArgumentNullException(nameof(modFixService));
        _modFixToolService = modFixToolService ?? throw new ArgumentNullException(nameof(modFixToolService));
        _fixToolsWatcher = fixToolsWatcher ?? throw new ArgumentNullException(nameof(fixToolsWatcher));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _fixToolsWatcher.StartWatching();
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            // Cache Management
            "SCAN_CACHE" => await ScanCacheAsync(),
            "TOOLS_GET_CACHE_STATS" or "GET_CACHE_STATISTICS" => await GetCacheStatisticsAsync(),
            "CLEAN_CACHE" => await CleanCacheAsync(request),

            // Screen Capture - Profile Management
            "SCREEN_CAPTURE_GET_PROFILES" => await GetCaptureProfilesAsync(),
            "SCREEN_CAPTURE_GET_PROFILE" => await GetCaptureProfileAsync(request),
            "SCREEN_CAPTURE_SAVE_PROFILE" => await SaveCaptureProfileAsync(request),
            "SCREEN_CAPTURE_DELETE_PROFILE" => await DeleteCaptureProfileAsync(request),

            // Screen Capture - Capture Operations
            "SCREEN_CAPTURE_SCREEN" => await CaptureScreenAsync(request),

            // Screen Capture - Border Overlay
            "SCREEN_CAPTURE_SHOW_BORDER" => await ShowBorderOverlayAsync(request),
            "SCREEN_CAPTURE_HIDE_BORDER" => await HideBorderOverlayAsync(request),

            // Screen Capture - Control Panel
            "SCREEN_CAPTURE_TOGGLE_CONTROL_PANEL" => await ToggleCaptureControlPanelAsync(request),

            // Analyzer pop-out window
            "ANALYZER_TOGGLE_WINDOW" => await ToggleAnalyzerWindowAsync(request),
            // Cross-window: analyzer window asks the MAIN window to locate a mod in the list
            "ANALYZER_REQUEST_LOCATE" => await RequestLocateAsync(request),

            // Mod Package - Export/Import
            "MOD_PACKAGE_EXPORT" => await ExportModPackageAsync(request),
            "MOD_PACKAGE_ANALYZE" => await AnalyzeModPackageAsync(request),
            "MOD_PACKAGE_IMPORT" => await ImportModPackageAsync(request),

            // File Cleanup
            "SCAN_ORPHANS" => await ScanOrphansAsync(request),
            "SCAN_ALL_ORPHANS" => await _fileCleanupService.ScanAllOrphansAsync(),
            "CLEAN_ORPHANS" => await CleanOrphansAsync(request),

            // Mod ID Migration (fire-and-forget — results via events)
            "MOD_ID_MIGRATION_SCAN" => StartModIdMigrationScan(),
            "MOD_ID_MIGRATION_EXECUTE" => StartModIdMigrationExecute(),

            // Mod fix script runner (fire-and-forget — progress + result via events)
            "RUN_MOD_FIX" => StartModFix(request),

            // Per-profile fix-tool library (collection of named fix tools)
            "FIX_TOOLS_GET" => await _modFixToolService.GetAllAsync(),
            "FIX_TOOLS_IMPORT" => await ImportFixToolAsync(request),
            "FIX_TOOLS_DELETE" => await DeleteFixToolAsync(request),
            "FIX_TOOLS_RENAME" => await RenameFixToolAsync(request),
            "FIX_TOOLS_SET_ENTRIES" => await SetFixToolEntriesAsync(request),
            "FIX_TOOLS_SET_ENABLED" => await SetFixToolEnabledAsync(request),
            "FIX_TOOLS_SET_ENTRY_ALIAS" => await SetFixToolEntryAliasAsync(request),
            "FIX_TOOLS_DETECT_PYTHON" => new { python = _modFixService.DetectPython() },

            // Mod Analysis
            "ANALYSIS_START" => StartAnalysisAsync(request),
            "ANALYSIS_PAUSE" => PauseAnalysis(),
            "ANALYSIS_RESUME" => ResumeAnalysisAsync(request),
            "ANALYSIS_CANCEL" => await CancelAnalysisAsync(),
            "ANALYSIS_GET_REPORT" => await GetAnalysisReportAsync(request),
            "ANALYSIS_GET_HISTORY" => await _modAnalysisService.GetSessionHistoryAsync(),
            "ANALYSIS_GET_LATEST_HEALTH" => await _modAnalysisService.GetLatestHealthAsync(),
            "ANALYSIS_DELETE_SESSION" => await DeleteAnalysisSessionAsync(request),
            "ANALYSIS_CLEAR_ALL" => await ClearAnalysisAsync(),

            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<List<CacheItem>> ScanCacheAsync()
    {
        return await _cacheService.ScanCacheAsync().ConfigureAwait(false);
    }

    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        return await _cacheService.GetCacheStatisticsAsync().ConfigureAwait(false);
    }

    public async Task<int> CleanCacheAsync(CacheCategory category)
    {
        var deletedCount = await _cacheService.CleanCacheAsync(category).ConfigureAwait(false);

        await _eventBus.EmitAsync(
            ModuleNames.TOOL,
            ToolEvents.CACHE_CLEANED,
            new { category = category.ToString(), deletedCount });

        return deletedCount;
    }

    private async Task<int> CleanCacheAsync(IpcRequest request)
    {
        var categoryString = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");

        if (!Enum.TryParse<CacheCategory>(categoryString, true, out var category))
        {
            throw new ArgumentException($"Invalid cache category: {categoryString}");
        }

        return await CleanCacheAsync(category).ConfigureAwait(false);
    }

    // ===== Screen Capture - Profile Management =====

    public async Task<List<ScreenCaptureProfile>> GetCaptureProfilesAsync()
    {
        return await _captureProfileRepository.GetAllAsync().ConfigureAwait(false);
    }

    public async Task<ScreenCaptureProfile?> GetCaptureProfileAsync(string id)
    {
        return await _captureProfileRepository.GetByIdAsync(id).ConfigureAwait(false);
    }

    private async Task<ScreenCaptureProfile?> GetCaptureProfileAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await GetCaptureProfileAsync(id).ConfigureAwait(false);
    }

    public async Task<string> SaveCaptureProfileAsync(SaveScreenCaptureProfileRequest request)
    {
        var profile = new ScreenCaptureProfile
        {
            Id = request.Id ?? Guid.NewGuid().ToString(),
            Name = request.Name,
            X = request.X,
            Y = request.Y,
            Width = request.Width,
            Height = request.Height,
        };

        if (string.IsNullOrEmpty(request.Id))
        {
            // Insert new profile
            var id = await _captureProfileRepository.InsertAsync(profile).ConfigureAwait(false);
            await _eventBus.EmitAsync(
                ModuleNames.TOOL,
                ToolEvents.CAPTURE_PROFILE_CREATED,
                new { id, name = profile.Name });
            return id;
        }
        else
        {
            // Update existing profile
            await _captureProfileRepository.UpdateAsync(profile).ConfigureAwait(false);
            await _eventBus.EmitAsync(
                ModuleNames.TOOL,
                ToolEvents.CAPTURE_PROFILE_UPDATED,
                new { id = profile.Id, name = profile.Name });
            return profile.Id;
        }
    }

    private async Task<string> SaveCaptureProfileAsync(IpcRequest request)
    {
        var saveRequest = new SaveScreenCaptureProfileRequest
        {
            Id = _payloadHelper.GetOptionalValue<string>(request.Payload, "id"),
            Name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name"),
            X = _payloadHelper.GetRequiredValue<int>(request.Payload, "x"),
            Y = _payloadHelper.GetRequiredValue<int>(request.Payload, "y"),
            Width = _payloadHelper.GetRequiredValue<int>(request.Payload, "width"),
            Height = _payloadHelper.GetRequiredValue<int>(request.Payload, "height"),
        };

        return await SaveCaptureProfileAsync(saveRequest).ConfigureAwait(false);
    }

    public async Task DeleteCaptureProfileAsync(string id)
    {
        await _captureProfileRepository.DeleteAsync(id).ConfigureAwait(false);
        await _eventBus.EmitAsync(
            ModuleNames.TOOL,
            ToolEvents.CAPTURE_PROFILE_DELETED,
            new { id });
    }

    private async Task<object?> DeleteCaptureProfileAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        await DeleteCaptureProfileAsync(id).ConfigureAwait(false);
        return null;
    }

    // ===== Screen Capture - Capture Operations =====

    public async Task<ScreenCaptureResult> CaptureScreenAsync(ScreenCaptureConfig config)
    {
        return await _screenCaptureService.CaptureAsync(config).ConfigureAwait(false);
    }

    private async Task<ScreenCaptureResult> CaptureScreenAsync(IpcRequest request)
    {
        _logger.Info("[ToolFacade] CaptureScreenAsync called");
        var payloadJson = global::System.Text.Json.JsonSerializer.Serialize(request.Payload);
        _logger.Info($"[ToolFacade] Request payload: {payloadJson}");

        var config = new ScreenCaptureConfig
        {
            ProfileId = _payloadHelper.GetOptionalValue<string>(request.Payload, "profileId"),
            X = _payloadHelper.GetOptionalValue<int>(request.Payload, "x"),
            Y = _payloadHelper.GetOptionalValue<int>(request.Payload, "y"),
            Width = _payloadHelper.GetOptionalValue<int>(request.Payload, "width"),
            Height = _payloadHelper.GetOptionalValue<int>(request.Payload, "height"),
            ShowSelectionUI = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "showSelectionUI") ?? false,
            CopyToClipboard = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "copyToClipboard") ?? true,
            SaveToFile = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "saveToFile") ?? false,
            OutputPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "outputPath")
        };

        _logger.Info($"[ToolFacade] Capture config: X={config.X}, Y={config.Y}, W={config.Width}, H={config.Height}, Clipboard={config.CopyToClipboard}");
        _logger.Info("[ToolFacade] Calling ScreenCaptureService.CaptureAsync...");

        var result = await CaptureScreenAsync(config).ConfigureAwait(false);

        _logger.Info($"[ToolFacade] Capture result: Success={result.Success}, Error={result.ErrorMessage}");
        return result;
    }

    // ===== Screen Capture - Border Overlay =====

    public async Task ShowBorderOverlayAsync(int x, int y, int width, int height)
    {
        await _screenCaptureService.ShowBorderOverlayAsync(x, y, width, height, _eventBus).ConfigureAwait(false);
    }

    private async Task<object?> ShowBorderOverlayAsync(IpcRequest request)
    {
        _logger.Debug("[ToolFacade] ShowBorderOverlayAsync handler called");
        var x = _payloadHelper.GetRequiredValue<int>(request.Payload, "x");
        var y = _payloadHelper.GetRequiredValue<int>(request.Payload, "y");
        var width = _payloadHelper.GetRequiredValue<int>(request.Payload, "width");
        var height = _payloadHelper.GetRequiredValue<int>(request.Payload, "height");
        _logger.Debug($"[ToolFacade] Parsed values: x={x}, y={y}, width={width}, height={height}");
        _logger.Debug("[ToolFacade] Calling ScreenCaptureService.ShowBorderOverlayAsync...");
        await ShowBorderOverlayAsync(x, y, width, height).ConfigureAwait(false);
        _logger.Debug("[ToolFacade] ShowBorderOverlayAsync completed");
        return null;
    }

    public async Task HideBorderOverlayAsync()
    {
        await _screenCaptureService.HideBorderOverlayAsync().ConfigureAwait(false);
    }

    private async Task<object?> HideBorderOverlayAsync(IpcRequest request)
    {
        await HideBorderOverlayAsync().ConfigureAwait(false);
        return null;
    }

    public async Task ToggleCaptureControlPanelAsync(string profileId)
    {
        await _screenCaptureService.ToggleCaptureControlPanelAsync(profileId).ConfigureAwait(false);
    }

    private async Task<object?> ToggleCaptureControlPanelAsync(IpcRequest request)
    {
        var profileId = request.ProfileId ?? throw new InvalidOperationException("ProfileId is required");
        await ToggleCaptureControlPanelAsync(profileId).ConfigureAwait(false);
        return null;
    }

    private async Task<object?> ToggleAnalyzerWindowAsync(IpcRequest request)
    {
        var profileId = request.ProfileId ?? throw new InvalidOperationException("ProfileId is required");
        await _analyzerWindowService.ToggleAsync(profileId).ConfigureAwait(false);
        return new { toggled = true };
    }

    /// <summary>
    /// Relay a "locate these mods in the list" request from the analyzer pop-out window to the MAIN
    /// window. Separate WebView2 windows share only the backend event bus, so we emit a MOD event that
    /// the main window's ModProvider handles (the analyzer window has no ModProvider → no echo).
    /// </summary>
    private async Task<object?> RequestLocateAsync(IpcRequest request)
    {
        var modIds = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "modIds");
        var categoryId = _payloadHelper.GetOptionalValue<string>(request.Payload, "categoryId");
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOCATE_REQUESTED,
            new { modIds, categoryId }).ConfigureAwait(false);
        return new { requested = true };
    }

    // ===== Mod Package - Export/Import =====

    private async Task<object?> ExportModPackageAsync(IpcRequest request)
    {
        var config = new ExportConfig
        {
            PackageName = _payloadHelper.GetRequiredValue<string>(request.Payload, "packageName"),
            PackageDescription = _payloadHelper.GetOptionalValue<string>(request.Payload, "packageDescription") ?? "",
            OutputPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "outputPath"),
            ModIds = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "modIds"),
            IncludeArchives = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "includeArchives") ?? true,
            IncludePreviews = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "includePreviews") ?? true,
        };

        return await _modPackageService.ExportAsync(config).ConfigureAwait(false);
    }

    private async Task<object?> AnalyzeModPackageAsync(IpcRequest request)
    {
        var packagePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "packagePath");
        return await _modPackageService.AnalyzePackageAsync(packagePath).ConfigureAwait(false);
    }

    private async Task<object?> ImportModPackageAsync(IpcRequest request)
    {
        var config = new ImportConfig
        {
            PackagePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "packagePath"),
            SelectedModIds = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "selectedModIds"),
            UpdateExisting = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "updateExisting") ?? true,
            ImportPreviews = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "importPreviews") ?? true,
            CreateMissingCategories = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "createMissingCategories") ?? true,
        };

        return await _modPackageService.ImportAsync(config).ConfigureAwait(false);
    }

    // ===== File Cleanup =====

    private async Task<OrphanScanResult> ScanOrphansAsync(IpcRequest request)
    {
        var categoryString = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");
        if (!Enum.TryParse<OrphanCategory>(categoryString, true, out var category))
        {
            throw new ArgumentException($"Invalid orphan category: {categoryString}");
        }
        return await _fileCleanupService.ScanOrphansAsync(category).ConfigureAwait(false);
    }

    private async Task<CleanupResult> CleanOrphansAsync(IpcRequest request)
    {
        var categoryString = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");
        if (!Enum.TryParse<OrphanCategory>(categoryString, true, out var category))
        {
            throw new ArgumentException($"Invalid orphan category: {categoryString}");
        }

        var pathsElement = _payloadHelper.GetRequiredValue<JsonElement>(request.Payload, "paths");
        var paths = new List<string>();
        if (pathsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pathsElement.EnumerateArray())
            {
                var path = item.GetString();
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }
        }

        return await _fileCleanupService.CleanOrphansAsync(category, paths).ConfigureAwait(false);
    }

    // ===== Mod ID Migration =====

    private object? StartModIdMigrationScan()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _modIdMigrationService.ScanAsync().ConfigureAwait(false);
                await _eventBus.EmitAsync(
                    ModuleNames.TOOL,
                    ToolEvents.MOD_ID_MIGRATION_SCAN_COMPLETE,
                    result).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"[ToolFacade] Mod ID migration scan failed: {ex.Message}", "ToolFacade", ex);
            }
        });

        return null;
    }

    private object? StartModIdMigrationExecute()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _modIdMigrationService.MigrateAsync().ConfigureAwait(false);
                await _eventBus.EmitAsync(
                    ModuleNames.TOOL,
                    ToolEvents.MOD_ID_MIGRATION_COMPLETE,
                    result).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"[ToolFacade] Mod ID migration failed: {ex.Message}", "ToolFacade", ex);
            }
        });

        return null;
    }

    // ===== Mod Fix (hash-fix script runner) =====

    private object? StartModFix(IpcRequest request)
    {
        var fixRequest = new ModFixRequest
        {
            ScriptPath = _payloadHelper.GetRequiredValue<string>(request.Payload, "scriptPath"),
            ModIds = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "modIds") ?? new List<string>(),
            RecompressAfter = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "recompress") ?? true,
        };

        // Fire-and-forget: run in background, push progress + a final result event when done.
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _modFixService.RunFixAsync(fixRequest).ConfigureAwait(false);
                await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_FIX_COMPLETE, result).ConfigureAwait(false);
            }
            catch (OperationException ex)
            {
                // Surface a structured (i18n) failure to the UI via the completion event.
                await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_FIX_COMPLETE,
                    new ModFixResult { Total = 0, Failed = 1, Results = { new ModFixItemResult { Success = false, Error = ex.Code } } }).ConfigureAwait(false);
                _logger.Warn($"[ToolFacade] Mod fix failed: {ex.Code}");
            }
            catch (Exception ex)
            {
                await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_FIX_COMPLETE,
                    new ModFixResult { Total = 0, Failed = 1, Results = { new ModFixItemResult { Success = false, Error = ex.Message } } }).ConfigureAwait(false);
                _logger.Error($"[ToolFacade] Mod fix failed: {ex.Message}", "ToolFacade", ex);
            }
        });

        return null;
    }

    // Fix-tool library mutations write markers INSIDE a tool folder (entries/aliases) which the folder
    // watcher (IncludeSubdirectories=false) doesn't see — so emit FIX_TOOLS_CHANGED explicitly here so
    // BOTH the manager and the mod-list "Fix" submenu re-scan (this is what makes a renamed toolset /
    // renamed entry show in the context menu).
    private Task EmitFixToolsChangedAsync()
        => _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.FIX_TOOLS_CHANGED, new { });

    private async Task<object?> ImportFixToolAsync(IpcRequest request)
    {
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var sourcePath = _payloadHelper.GetRequiredValue<string>(request.Payload, "sourcePath");
        var isFolder = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "isFolder") ?? false;
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var tool = await _modFixToolService.ImportAsync(name, sourcePath, isFolder, description).ConfigureAwait(false);
        await EmitFixToolsChangedAsync().ConfigureAwait(false);
        return tool;
    }

    private async Task<object?> DeleteFixToolAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        await _modFixToolService.DeleteAsync(id).ConfigureAwait(false);
        await EmitFixToolsChangedAsync().ConfigureAwait(false);
        return null;
    }

    private async Task<object?> RenameFixToolAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var newName = _payloadHelper.GetRequiredValue<string>(request.Payload, "newName");
        var newId = await _modFixToolService.RenameAsync(id, newName).ConfigureAwait(false);
        await EmitFixToolsChangedAsync().ConfigureAwait(false);
        return new { id = newId };
    }

    private async Task<object?> SetFixToolEntriesAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var entries = _payloadHelper.GetOptionalValue<List<string>>(request.Payload, "entries") ?? new List<string>();
        await _modFixToolService.SetEntriesAsync(id, entries).ConfigureAwait(false);
        await EmitFixToolsChangedAsync().ConfigureAwait(false);
        return null;
    }

    private async Task<object?> SetFixToolEnabledAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var enabled = _payloadHelper.GetRequiredValue<bool>(request.Payload, "enabled");
        await _modFixToolService.SetEnabledAsync(id, enabled).ConfigureAwait(false);
        await EmitFixToolsChangedAsync().ConfigureAwait(false);
        return null;
    }

    private async Task<object?> SetFixToolEntryAliasAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var entryName = _payloadHelper.GetRequiredValue<string>(request.Payload, "entryName");
        var alias = _payloadHelper.GetOptionalValue<string>(request.Payload, "alias");
        await _modFixToolService.SetEntryAliasAsync(id, entryName, alias).ConfigureAwait(false);
        await EmitFixToolsChangedAsync().ConfigureAwait(false);
        return null;
    }

    // ===== Mod Analysis =====

    private object? StartAnalysisAsync(IpcRequest request)
    {
        var categoryId = _payloadHelper.GetOptionalValue<string>(request.Payload, "categoryId");

        // Fire-and-forget: start in background, emit completion event when done
        _ = Task.Run(async () =>
        {
            try
            {
                var report = await _modAnalysisService.StartAnalysisAsync(categoryId).ConfigureAwait(false);
                // Only emit COMPLETE for finished sessions (not if another scan was already running)
                if (report.Status != AnalysisStatus.Running)
                {
                    await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_ANALYSIS_COMPLETE, report).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ToolFacade] Analysis failed: {ex.Message}", "ToolFacade", ex);
            }
        });

        return null;
    }

    private object? PauseAnalysis()
    {
        _modAnalysisService.PauseAnalysis();
        return null;
    }

    private object? ResumeAnalysisAsync(IpcRequest request)
    {
        // Try in-memory resume first (active paused task)
        if (_modAnalysisService.IsPaused)
        {
            _modAnalysisService.ResumeAnalysis();
            return null;
        }

        // Stale session — restart from where it left off in background
        var sessionId = _payloadHelper.GetOptionalValue<string>(request.Payload, "sessionId");
        if (string.IsNullOrEmpty(sessionId)) return null;

        _ = Task.Run(async () =>
        {
            try
            {
                var report = await _modAnalysisService.ResumeSessionAsync(sessionId).ConfigureAwait(false);
                if (report.Status != AnalysisStatus.Running)
                {
                    await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_ANALYSIS_COMPLETE, report).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ToolFacade] Resume analysis failed: {ex.Message}", "ToolFacade", ex);
            }
        });

        return null;
    }

    private async Task<object?> CancelAnalysisAsync()
    {
        var report = await _modAnalysisService.CancelAnalysisAsync().ConfigureAwait(false);

        // For stale cancels (no active task), emit COMPLETE so frontend transitions to findings.
        // Active cancels emit COMPLETE naturally when their Task.Run exits.
        if (report != null)
        {
            await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_ANALYSIS_COMPLETE, report).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<object?> GetAnalysisReportAsync(IpcRequest request)
    {
        var sessionId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sessionId");
        return await _modAnalysisService.GetSessionReportAsync(sessionId).ConfigureAwait(false);
    }

    private async Task<object?> DeleteAnalysisSessionAsync(IpcRequest request)
    {
        var sessionId = _payloadHelper.GetRequiredValue<string>(request.Payload, "sessionId");
        await _modAnalysisService.DeleteSessionAsync(sessionId).ConfigureAwait(false);
        return null;
    }

    private async Task<object?> ClearAnalysisAsync()
    {
        await _modAnalysisService.ClearAllSessionsAsync().ConfigureAwait(false);
        return null;
    }
}
