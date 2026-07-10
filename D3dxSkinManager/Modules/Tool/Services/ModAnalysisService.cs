using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Modules.Tool.Services;

public interface IModAnalysisService : IDisposable
{
    bool IsPaused { get; }
    Task<FullAnalysisReport> StartAnalysisAsync(string? categoryId = null);
    void PauseAnalysis();
    void ResumeAnalysis();
    Task<FullAnalysisReport> ResumeSessionAsync(string sessionId);
    Task<FullAnalysisReport?> CancelAnalysisAsync();
    Task<FullAnalysisReport> GetSessionReportAsync(string sessionId);
    Task<List<AnalysisSessionSummary>> GetSessionHistoryAsync();
    Task DeleteSessionAsync(string sessionId);
    Task ClearAllSessionsAsync();
    Task RemoveModFromAnalysisAsync(string modId);
    Task<List<ModHealthSummary>> GetLatestHealthAsync();
}

public class ModAnalysisService : IModAnalysisService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IModRepository _modRepository;
    private readonly IModEnrichmentService _enrichmentService;
    private readonly IModArchiveService _archiveService;
    private readonly IModAnalysisRepository _analysisRepository;
    private readonly ICategoryService _categoryService;
    private readonly IProfileEventBus _eventBus;
    private readonly IProcessRegistry _processRegistry;
    private readonly IHashHelper _hashHelper;
    private readonly IProfileContext _profileContext;
    private readonly ILogHelper _logger;
    private readonly string? _modDeletedHandlerId;
    // The ProcessRegistry entry for the active scan (status bar + Activity panel). Null when idle.
    private string? _currentProcId;

    private volatile bool _pauseRequested;
    private volatile bool _cancelRequested;
    private volatile bool _isRunning;
    public bool IsPaused => _isRunning && _pauseRequested;
    private string? _currentSessionId;
    private readonly ManualResetEventSlim _resumeSignal = new(true); // starts signaled (not paused)

    private static readonly string[] BufferExtensions = [".buf", ".ib"];
    private static readonly string[] TextureExtensions = [".dds", ".png", ".jpg", ".jpeg", ".tga", ".bmp"];

    // pattern → the plugin NAME it implies. The old name extraction split on '\' and took the FIRST
    // segment, which produced bogus "CommandList"/"Resource" plugin refs on every namespaced call
    // (real-library audit 2026-07-05).
    private static readonly (string Pattern, string Plugin)[] PluginPatterns = [
        (@"\ZZMI\", "ZZMI"), (@"\SRMI\", "SRMI"), (@"\GIMI\", "GIMI"), (@"\WWMI\", "WWMI"),
        (@"\ShaderFixes\", "ShaderFixes"), (@"\RabbitFX\", "RabbitFX"),
        (@"CommandList\ZZMI", "ZZMI"), (@"CommandList\SRMI", "SRMI"), (@"CommandList\GIMI", "GIMI"), (@"CommandList\WWMI", "WWMI"),
        (@"Resource\ZZMI", "ZZMI"), (@"Resource\SRMI", "SRMI"), (@"Resource\GIMI", "GIMI"), (@"Resource\WWMI", "WWMI"),
        (@"Resource\ShaderFixes", "ShaderFixes"), (@"Resource\RabbitFX", "RabbitFX"),
    ];

    public ModAnalysisService(
        IProfilePathService profilePaths,
        IModRepository modRepository,
        IModEnrichmentService enrichmentService,
        IModArchiveService archiveService,
        IModAnalysisRepository analysisRepository,
        ICategoryService categoryService,
        IProfileEventBus eventBus,
        IProcessRegistry processRegistry,
        IHashHelper hashHelper,
        IProfileContext profileContext,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _modRepository = modRepository;
        _enrichmentService = enrichmentService;
        _archiveService = archiveService;
        _analysisRepository = analysisRepository;
        _categoryService = categoryService;
        _eventBus = eventBus;
        _processRegistry = processRegistry;
        _hashHelper = hashHelper;
        _profileContext = profileContext;
        _logger = logger;

        // The registry is in-memory only — announce THIS profile's crash-interrupted sessions from
        // their profile-DB checkpoint (sessions left "running" with no live task) so the Activity
        // panel offers resume without any global state file. Fire-and-forget: never blocks profile init.
        _ = Task.Run(AnnounceInterruptedSessionsAsync);

        // Subscribe to mod deletion events to keep analysis findings in sync
        _modDeletedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.DELETED,
            async (msg) =>
            {
                try
                {
                    var modId = ExtractModId(msg.Payload);
                    if (!string.IsNullOrEmpty(modId))
                        await RemoveModFromAnalysisAsync(modId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[ModAnalysis] Failed to clean up findings for deleted mod: {ex.Message}");
                }
            }
        );
    }

    public void Dispose()
    {
        if (_modDeletedHandlerId != null)
            _eventBus.Unsubscribe(_modDeletedHandlerId);
    }

    private static string? ExtractModId(object? payload)
    {
        if (payload == null) return null;
        // Payload is anonymous type { Id = "..." } serialized as JsonElement or dynamic
        if (payload is JsonElement je)
        {
            if (je.TryGetProperty("Id", out var idProp) || je.TryGetProperty("id", out idProp))
                return idProp.GetString();
        }
        // Try via reflection for anonymous types
        var idProperty = payload.GetType().GetProperty("Id") ?? payload.GetType().GetProperty("id");
        return idProperty?.GetValue(payload)?.ToString();
    }

    public void PauseAnalysis()
    {
        if (_isRunning && !_pauseRequested)
        {
            _pauseRequested = true;
            _resumeSignal.Reset(); // Block the analysis loop
        }
    }

    public void ResumeAnalysis()
    {
        if (_isRunning && _pauseRequested)
        {
            _pauseRequested = false;
            _resumeSignal.Set(); // Unblock the analysis loop
        }
    }

    /// <summary>
    /// Announce sessions this profile's DB left in "running" (an app crash mid-scan — at
    /// construction time no scan can actually be live) as Interrupted+resumable registry entries.
    /// This replaces the old global process-state.json: the profile DB is the checkpoint.
    /// </summary>
    private async Task AnnounceInterruptedSessionsAsync()
    {
        try
        {
            var sessions = await _analysisRepository.GetAllSessionsAsync().ConfigureAwait(false);
            foreach (var s in sessions.Where(s => s.Status == "running"))
            {
                var title = string.IsNullOrEmpty(s.CategoryName) ? "Analyzing mods" : $"Analyzing: {s.CategoryName}";
                _processRegistry.RegisterInterrupted(ProcessType.Analysis, title, s.Id,
                    titleKey: string.IsNullOrEmpty(s.CategoryName) ? "process.analysis" : "process.analysisCategory",
                    titleArg: string.IsNullOrEmpty(s.CategoryName) ? null : s.CategoryName,
                    profileId: _profileContext.ProfileId,
                    startedAtUtc: DateTime.TryParse(s.StartedAt, null, global::System.Globalization.DateTimeStyles.AdjustToUniversal, out var started) ? started : null);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to announce interrupted analysis sessions: {ex.Message}", "ModAnalysisService");
        }
    }

    /// <summary>
    /// Resume a stale "running" session that has no active background task.
    /// Continues analysis from where it left off (skips already-analyzed mods).
    /// </summary>
    public async Task<FullAnalysisReport> ResumeSessionAsync(string sessionId)
    {
        if (_isRunning)
        {
            // Already running — just unpause if needed
            ResumeAnalysis();
            if (_currentSessionId != null)
                return await GetSessionReportAsync(_currentSessionId).ConfigureAwait(false);
            return new FullAnalysisReport { Status = AnalysisStatus.Running };
        }

        var session = await _analysisRepository.GetSessionAsync(sessionId).ConfigureAwait(false);
        if (session == null || session.Status != "running")
            return await BuildReportFromSessionAsync(sessionId).ConfigureAwait(false);

        _isRunning = true;
        _pauseRequested = false;
        _cancelRequested = false;
        _resumeSignal.Set();
        _currentSessionId = session.Id;

        var resumeTitle = string.IsNullOrEmpty(session.CategoryName) ? "Analyzing mods" : $"Analyzing: {session.CategoryName}";
        _currentProcId = _processRegistry.Start(ProcessType.Analysis, resumeTitle, progress: 0,
            resumable: true, resumePayload: session.Id,
            titleKey: string.IsNullOrEmpty(session.CategoryName) ? "process.analysis" : "process.analysisCategory",
            titleArg: session.CategoryName);

        try
        {
            // Get all mods for this session's scope — include selected category + all descendants
            var enrichedMods = await GetAllEnrichedModsAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(session.CategoryId))
            {
                var categoryIds = await _categoryService.GetAllDescendantIdsAsync(session.CategoryId).ConfigureAwait(false);
                var categoryIdSet = new HashSet<string>(categoryIds, StringComparer.OrdinalIgnoreCase);
                enrichedMods = enrichedMods.Where(m => categoryIdSet.Contains(m.Category)).ToList();
            }

            // Skip already-analyzed mods and collect existing counts
            var existingFindings = await _analysisRepository.GetFindingsBySessionAsync(sessionId).ConfigureAwait(false);
            var analyzedModIds = existingFindings.Select(f => f.ModId).ToHashSet();
            var remainingMods = enrichedMods.Where(m => !analyzedModIds.Contains(m.Id)).ToList();

            int initHealthy = existingFindings.Count(f => f.HealthStatus == "healthy");
            int initWarning = existingFindings.Count(f => f.HealthStatus == "warning");
            int initError = existingFindings.Count(f => f.HealthStatus == "error");

            _logger.Info($"[ModAnalysis] Resuming session {sessionId}: {analyzedModIds.Count} done, {remainingMods.Count} remaining", "ModAnalysis");

            await RunPerModAnalysisAsync(session, remainingMods, analyzedModIds.Count, initHealthy, initWarning, initError).ConfigureAwait(false);
            var report = await BuildReportFromSessionAsync(session.Id).ConfigureAwait(false);

            // Set final status explicitly (DB still says "running" at this point)
            report.Status = _cancelRequested ? AnalysisStatus.Cancelled : AnalysisStatus.Completed;

            // Update session with final counts
            session.Status = _cancelRequested ? "cancelled" : "completed";
            session.AnalyzedCount = report.AnalyzedCount;
            session.HealthyCount = report.HealthyCount;
            session.WarningCount = report.WarningCount;
            session.ErrorCount = report.ErrorCount;
            session.IdenticalCount = report.IdenticalCount;
            session.TextureVariantCount = report.TextureVariantCount;
            session.ConflictCount = report.ConflictCount;
            session.CompletedAt = DateTime.UtcNow.ToString("o");
            await _analysisRepository.UpdateSessionAsync(session).ConfigureAwait(false);

            if (_currentProcId != null)
            {
                if (_cancelRequested) _processRegistry.Cancel(_currentProcId);
                else _processRegistry.Complete(_currentProcId);
            }

            return report;
        }
        catch (Exception ex)
        {
            if (_currentProcId != null) _processRegistry.Fail(_currentProcId, ex.Message);
            throw;
        }
        finally
        {
            _isRunning = false;
            _currentSessionId = null;
            _currentProcId = null;
        }
    }

    /// <summary>
    /// Cancel analysis. Returns a report for stale session cancels (no active task),
    /// or null for active cancels (COMPLETE event emitted by the running task).
    /// </summary>
    public async Task<FullAnalysisReport?> CancelAnalysisAsync()
    {
        if (_isRunning)
        {
            // Active task — signal it to stop. The Task.Run will emit COMPLETE when it exits.
            _cancelRequested = true;
            _pauseRequested = false;
            _resumeSignal.Set(); // Unblock if paused so loop can exit
            await Task.Delay(100).ConfigureAwait(false);
            return null;
        }

        // No active task — cancel stale "running" sessions directly in DB and return report
        var sessions = await _analysisRepository.GetAllSessionsAsync().ConfigureAwait(false);
        FullAnalysisReport? report = null;
        foreach (var s in sessions.Where(s => s.Status == "running"))
        {
            s.Status = "cancelled";
            s.CompletedAt = DateTime.UtcNow.ToString("o");
            await _analysisRepository.UpdateSessionAsync(s).ConfigureAwait(false);
            report ??= await BuildReportFromSessionAsync(s.Id).ConfigureAwait(false);
        }
        return report;
    }

    // ===== Session Management =====

    public async Task<List<AnalysisSessionSummary>> GetSessionHistoryAsync()
    {
        var sessions = await _analysisRepository.GetAllSessionsAsync().ConfigureAwait(false);
        return sessions.Select(s =>
        {
            // Stale "running" sessions (no active task, or actively paused) → show as "paused"
            var status = s.Status;
            if (status == "running" && (!_isRunning || (s.Id == _currentSessionId && IsPaused)))
                status = "paused";

            return new AnalysisSessionSummary
            {
                Id = s.Id, CategoryId = s.CategoryId, CategoryName = s.CategoryName,
                Status = status, TotalMods = s.TotalMods, AnalyzedCount = s.AnalyzedCount,
                HealthyCount = s.HealthyCount, WarningCount = s.WarningCount, ErrorCount = s.ErrorCount,
                IdenticalCount = s.IdenticalCount, TextureVariantCount = s.TextureVariantCount,
                ConflictCount = s.ConflictCount, StartedAt = s.StartedAt, CompletedAt = s.CompletedAt
            };
        }).ToList();
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        await _analysisRepository.DeleteSessionAsync(sessionId).ConfigureAwait(false);
    }

    public async Task ClearAllSessionsAsync()
    {
        await _analysisRepository.ClearAllSessionsAsync().ConfigureAwait(false);
    }

    // ===== Analysis =====

    public async Task<FullAnalysisReport> StartAnalysisAsync(string? categoryId = null)
    {
        if (_isRunning)
        {
            if (_currentSessionId != null)
                return await GetSessionReportAsync(_currentSessionId).ConfigureAwait(false);
            return new FullAnalysisReport { Status = AnalysisStatus.Running };
        }

        _isRunning = true;
        _pauseRequested = false;
        _cancelRequested = false;
        _resumeSignal.Set();

        // Create session — include selected category + all descendants
        var enrichedMods = await GetAllEnrichedModsAsync().ConfigureAwait(false);
        if (!string.IsNullOrEmpty(categoryId))
        {
            var categoryIds = await _categoryService.GetAllDescendantIdsAsync(categoryId).ConfigureAwait(false);
            var categoryIdSet = new HashSet<string>(categoryIds, StringComparer.OrdinalIgnoreCase);
            enrichedMods = enrichedMods.Where(m => categoryIdSet.Contains(m.Category)).ToList();
        }

        string? categoryName = !string.IsNullOrEmpty(categoryId)
            ? await _categoryService.GetCategoryNameAsync(categoryId).ConfigureAwait(false)
            : null;
        var session = new AnalysisSessionEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            CategoryId = categoryId,
            CategoryName = categoryName,
            Status = "running",
            TotalMods = enrichedMods.Count,
            StartedAt = DateTime.UtcNow.ToString("o")
        };
        await _analysisRepository.CreateSessionAsync(session).ConfigureAwait(false);
        _currentSessionId = session.Id;

        // Track in the status bar + Activity panel (resumable: the session can be resumed after a crash).
        var scanTitle = string.IsNullOrEmpty(categoryName) ? "Analyzing mods" : $"Analyzing: {categoryName}";
        _currentProcId = _processRegistry.Start(ProcessType.Analysis, scanTitle, progress: 0,
            resumable: true, resumePayload: session.Id,
            titleKey: string.IsNullOrEmpty(categoryName) ? "process.analysis" : "process.analysisCategory",
            titleArg: categoryName);

        try
        {
            await RunPerModAnalysisAsync(session, enrichedMods).ConfigureAwait(false);
            var report = await BuildReportFromSessionAsync(session.Id).ConfigureAwait(false);

            // Set final status explicitly (DB still says "running" at this point)
            report.Status = _cancelRequested ? AnalysisStatus.Cancelled : AnalysisStatus.Completed;

            // Update session with final counts
            session.Status = _cancelRequested ? "cancelled" : "completed";
            session.AnalyzedCount = report.AnalyzedCount;
            session.HealthyCount = report.HealthyCount;
            session.WarningCount = report.WarningCount;
            session.ErrorCount = report.ErrorCount;
            session.IdenticalCount = report.IdenticalCount;
            session.TextureVariantCount = report.TextureVariantCount;
            session.ConflictCount = report.ConflictCount;
            session.CompletedAt = DateTime.UtcNow.ToString("o");
            await _analysisRepository.UpdateSessionAsync(session).ConfigureAwait(false);

            if (_currentProcId != null)
            {
                if (_cancelRequested) _processRegistry.Cancel(_currentProcId);
                else _processRegistry.Complete(_currentProcId);
            }

            return report;
        }
        catch (Exception ex)
        {
            if (_currentProcId != null) _processRegistry.Fail(_currentProcId, ex.Message);
            throw;
        }
        finally
        {
            _isRunning = false;
            _currentSessionId = null;
            _currentProcId = null;
        }
    }

    public async Task<FullAnalysisReport> GetSessionReportAsync(string sessionId)
    {
        var report = await BuildReportFromSessionAsync(sessionId).ConfigureAwait(false);
        // If DB says "running" but no active task → treat as paused (stale session)
        if (report.Status == AnalysisStatus.Running)
        {
            if (!_isRunning || (sessionId == _currentSessionId && IsPaused))
                report.Status = AnalysisStatus.Paused;
        }
        return report;
    }

    // ===== Phase 1: Per-Mod Analysis =====

    private async Task RunPerModAnalysisAsync(AnalysisSessionEntity session, List<Mod.Models.ModInfo> mods, int processedOffset = 0, int initialHealthy = 0, int initialWarning = 0, int initialError = 0)
    {
        var cacheModsDir = _profilePaths.CacheModsDirectory;
        int total = session.TotalMods; // Use session total (not remaining mods count)
        int processed = processedOffset;
        int healthyCount = initialHealthy, warningCount = initialWarning, errorCount = initialError;
        string? lastModName = null;
        string? lastHealthStatus = null;

        foreach (var mod in mods)
        {
            // Cancel check — exit loop immediately
            if (_cancelRequested) return;

            // Pause check — emit paused status and wait for resume/cancel
            if (_pauseRequested)
            {
                await EmitProgress(session.Id, "paused", processed, total, mod.Name, AnalysisStatus.Paused, healthyCount, warningCount, errorCount, lastModName, lastHealthStatus).ConfigureAwait(false);
                _resumeSignal.Wait(); // Block until ResumeAnalysis() or CancelAnalysis()
                if (_cancelRequested) return;
                // Resumed — emit running status
                await EmitProgress(session.Id, "analyzing", processed, total, mod.Name, AnalysisStatus.Running, healthyCount, warningCount, errorCount, lastModName, lastHealthStatus).ConfigureAwait(false);
            }

            processed++;

            string? modDir = null;
            bool needsCleanup = false;

            if (mod.HasCache)
                modDir = GetModDirectory(cacheModsDir, mod.Id, mod.IsLoaded);

            if (modDir == null && mod.IsAvailable)
            {
                modDir = await ExtractToTempAsync(mod.Id).ConfigureAwait(false);
                if (modDir != null) needsCleanup = true;
            }

            if (modDir != null)
            {
                try
                {
                    var finding = await AnalyzeModAsync(session.Id, mod.Id, modDir).ConfigureAwait(false);
                    await _analysisRepository.InsertFindingAsync(finding).ConfigureAwait(false);

                    lastModName = mod.Name;
                    lastHealthStatus = finding.HealthStatus;

                    switch (finding.HealthStatus)
                    {
                        case "healthy": healthyCount++; break;
                        case "warning": warningCount++; break;
                        case "error": errorCount++; break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[ModAnalysis] Failed {mod.Id}: {ex.Message}");
                    await _analysisRepository.InsertFindingAsync(new AnalysisFindingEntity
                    {
                        SessionId = session.Id, ModId = mod.Id, HealthStatus = "error",
                        HealthIssues = JsonSerializer.Serialize(new[] { new { Type = "InvalidIniSyntax", Severity = "Error", Message = ex.Message } }),
                    }).ConfigureAwait(false);
                    lastModName = mod.Name;
                    lastHealthStatus = "error";
                    errorCount++;
                }
                finally
                {
                    if (needsCleanup) CleanupTempDir(modDir);
                }
            }

            // Emit after each mod so frontend gets live results
            await EmitProgress(session.Id, "analyzing", processed, total, mod.Name, AnalysisStatus.Running, healthyCount, warningCount, errorCount, lastModName, lastHealthStatus).ConfigureAwait(false);
        }

        await EmitProgress(session.Id, "complete", total, total, "", AnalysisStatus.Completed, healthyCount, warningCount, errorCount, lastModName, lastHealthStatus).ConfigureAwait(false);
    }

    private async Task<AnalysisFindingEntity> AnalyzeModAsync(string sessionId, string modId, string modDir)
    {
        var issues = new List<ModHealthIssue>();
        var allTargetHashes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var allPluginRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int textureOverrideCount = 0;

        // DISABLED-prefixed files/folders are skipped by the runtime (XXMI exclude_recursive /
        // GIMI-merge convention) — they must not contribute hashes/overrides or the analyzer
        // reports false conflicts and duplicates on merged mods.
        var allIniFiles = Directory.GetFiles(modDir, "*.ini", SearchOption.AllDirectories);
        var iniFiles = allIniFiles
            .Where(f => !IniParser.IsDisabledPath(Path.GetRelativePath(modDir, f)))
            // Deterministic order — the per-aspect ini fingerprints below concatenate file contents.
            .OrderBy(f => Path.GetRelativePath(modDir, f), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        int disabledIniCount = allIniFiles.Length - iniFiles.Length;

        // Per-aspect ini fingerprints — keys / constants / logic (hash= lines excluded from logic so
        // a pure hash-fix reads as "same logic, different hashes"). Lets duplicate grouping say WHAT
        // changed between two copies of the same mod (dedup taxonomy case 2).
        var aspects = new IniAspectAccumulator();

        if (iniFiles.Length == 0)
        {
            if (disabledIniCount > 0)
                issues.Add(new ModHealthIssue { Type = HealthIssueType.AllIniDisabled, Severity = HealthIssueSeverity.Warning, Message = $"All {disabledIniCount} .ini file(s) are disabled — the mod renders nothing" });
            else
                issues.Add(new ModHealthIssue { Type = HealthIssueType.NoIniFile, Severity = HealthIssueSeverity.Error, Message = "No .ini files found" });
        }

        var allFiles = Directory.GetFiles(modDir, "*", SearchOption.AllDirectories);
        int resourceFileCount = allFiles.Count(f =>
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            return BufferExtensions.Contains(ext) || TextureExtensions.Contains(ext);
        });

        if (allFiles.Length <= 1 && allIniFiles.Length == 0)
            issues.Add(new ModHealthIssue { Type = HealthIssueType.EmptyMod, Severity = HealthIssueSeverity.Error, Message = "Mod directory is empty" });

        foreach (var iniFile in iniFiles)
        {
            var iniName = Path.GetFileName(iniFile);
            try
            {
                var lines = await File.ReadAllLinesAsync(iniFile).ConfigureAwait(false);
                if (lines.Length == 0) { issues.Add(new ModHealthIssue { Type = HealthIssueType.EmptyIniFile, Severity = HealthIssueSeverity.Warning, Message = $"Empty: {iniName}" }); continue; }

                var structure = ParseIniStructure(lines, iniName, issues, aspects);
                foreach (var h in structure.TargetHashes) allTargetHashes.Add(h);
                foreach (var p in structure.PluginReferences) allPluginRefs.Add(p);
                textureOverrideCount += structure.TextureOverrideCount;

                var iniDir = Path.GetDirectoryName(iniFile)!;
                foreach (var refFile in structure.BufferFiles.Concat(structure.TextureFiles))
                {
                    if (!FileExistsInTree(modDir, iniDir, refFile))
                        issues.Add(new ModHealthIssue { Type = HealthIssueType.MissingResource, Severity = HealthIssueSeverity.Warning, Message = $"Missing: {refFile}", FilePath = iniName });
                }
            }
            catch (Exception ex)
            {
                issues.Add(new ModHealthIssue { Type = HealthIssueType.InvalidIniSyntax, Severity = HealthIssueSeverity.Error, Message = $"Parse error: {ex.Message}", FilePath = iniName });
            }
        }

        // Plugin presence check. In XXMI mode the importer ships its core plugin (e.g. ZZMI) under
        // <importer>/Core — the importer root is the parent of the cache Mods dir, so check there too
        // or every ZZMI-namespaced mod reports a false "plugin not found".
        var tdMigotoDir = _profilePaths.TdMigotoDirectory;
        var importerCoreDir = Path.Combine(Directory.GetParent(_profilePaths.CacheModsDirectory)?.FullName ?? _profilePaths.CacheModsDirectory, "Core");
        foreach (var plugin in allPluginRefs)
        {
            if (!Directory.Exists(Path.Combine(tdMigotoDir, plugin)) &&
                !Directory.Exists(Path.Combine(_profilePaths.CacheModsDirectory, plugin)) &&
                !Directory.Exists(Path.Combine(importerCoreDir, plugin)))
                issues.Add(new ModHealthIssue { Type = HealthIssueType.MissingPlugin, Severity = HealthIssueSeverity.Info, Message = $"Plugin not found: {plugin}" });
        }

        var bufferFiles = allFiles.Where(f => BufferExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();
        var textureFiles = allFiles.Where(f => TextureExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();

        string healthStatus = issues.Any(i => i.Severity == HealthIssueSeverity.Error) ? "error" : issues.Any(i => i.Severity == HealthIssueSeverity.Warning) ? "warning" : "healthy";

        var bufferFileHashes = await ComputePerFileHashesAsync(bufferFiles).ConfigureAwait(false);
        var textureFileHashes = await ComputePerFileHashesAsync(textureFiles).ConfigureAwait(false);

        return new AnalysisFindingEntity
        {
            SessionId = sessionId, ModId = modId,
            TargetHashes = JsonSerializer.Serialize(allTargetHashes.ToList()),
            BufferHash = await ComputeCombinedHashAsync(bufferFiles).ConfigureAwait(false),
            TextureHash = await ComputeCombinedHashAsync(textureFiles).ConfigureAwait(false),
            BufferFileHashes = JsonSerializer.Serialize(bufferFileHashes),
            TextureFileHashes = JsonSerializer.Serialize(textureFileHashes),
            HealthStatus = healthStatus,
            HealthIssues = JsonSerializer.Serialize(issues),
            PluginDependencies = JsonSerializer.Serialize(allPluginRefs.ToList()),
            IniFileCount = iniFiles.Length, ResourceFileCount = resourceFileCount,
            TextureOverrideCount = textureOverrideCount,
            BufferSizeBytes = bufferFiles.Sum(f => new FileInfo(f).Length),
            TextureSizeBytes = textureFiles.Sum(f => new FileInfo(f).Length),
            IniFingerprints = iniFiles.Length > 0 ? aspects.ToJson() : null,
        };
    }

    // ===== Phase 2: Build Report from Session =====

    private async Task<FullAnalysisReport> BuildReportFromSessionAsync(string sessionId)
    {
        var session = await _analysisRepository.GetSessionAsync(sessionId).ConfigureAwait(false);
        var findings = await _analysisRepository.GetFindingsBySessionAsync(sessionId).ConfigureAwait(false);
        var enrichedMods = await GetAllEnrichedModsAsync().ConfigureAwait(false);
        var modLookup = enrichedMods.ToDictionary(m => m.Id, m => m);
        var previewsDir = _profilePaths.PreviewsDirectory;

        var report = new FullAnalysisReport
        {
            SessionId = sessionId,
            CategoryId = session?.CategoryId,
            TotalMods = session?.TotalMods ?? 0,
            AnalyzedCount = findings.Count,
            SkippedCount = (session?.TotalMods ?? 0) - findings.Count,
            Status = session?.Status switch { "paused" => AnalysisStatus.Paused, "completed" => AnalysisStatus.Completed, "cancelled" => AnalysisStatus.Cancelled, "running" => AnalysisStatus.Running, _ => AnalysisStatus.Idle }
        };

        var allResults = new List<ModAnalysisResult>();
        var globalHashMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in findings)
        {
            var mod = modLookup.GetValueOrDefault(f.ModId);
            var targetHashes = DeserializeList(f.TargetHashes);
            foreach (var h in targetHashes) { globalHashMap.TryGetValue(h, out var c); globalHashMap[h] = c + 1; }

            string? previewPath = null;
            var previewDir = Path.Combine(previewsDir, f.ModId);
            if (Directory.Exists(previewDir))
            {
                var previews = Directory.GetFiles(previewDir).OrderBy(x => x).ToArray();
                if (previews.Length > 0) previewPath = previews[0];
            }

            allResults.Add(new ModAnalysisResult
            {
                ModId = f.ModId, ModName = mod?.Name ?? f.ModId, CategoryName = mod?.CategoryName ?? "",
                IsLoaded = mod?.IsLoaded ?? false, HasCache = mod?.HasCache ?? false, IsAvailable = mod?.IsAvailable ?? false,
                HealthStatus = f.HealthStatus, Issues = DeserializeIssues(f.HealthIssues),
                IniFileCount = f.IniFileCount, ResourceFileCount = f.ResourceFileCount,
                TextureOverrideCount = f.TextureOverrideCount,
                TargetHashes = targetHashes, BufferHash = f.BufferHash, TextureHash = f.TextureHash,
                BufferFileHashes = DeserializeList(f.BufferFileHashes),
                TextureFileHashes = DeserializeList(f.TextureFileHashes),
                BufferSizeBytes = f.BufferSizeBytes, TextureSizeBytes = f.TextureSizeBytes,
                PluginDependencies = DeserializeList(f.PluginDependencies), PreviewPath = previewPath,
                IniFingerprints = f.IniFingerprints
            });
        }

        // Stale hash detection
        var suspiciousSet = new HashSet<string>(globalHashMap.Where(kv => kv.Value == 1).Select(kv => kv.Key), StringComparer.OrdinalIgnoreCase);
        report.SuspiciousHashes = suspiciousSet.Select(h => new HashFrequency { Hash = h, ModCount = 1, IsSuspicious = true }).ToList();
        foreach (var r in allResults)
        {
            if (r.TargetHashes.Count > 0 && r.TargetHashes.All(h => suspiciousSet.Contains(h)))
                r.Issues.Add(new ModHealthIssue { Type = HealthIssueType.StaleHash, Severity = HealthIssueSeverity.Info, Message = "All target hashes are unique — may be outdated" });
        }

        report.HealthyCount = allResults.Count(r => r.HealthStatus == "healthy");
        report.WarningCount = allResults.Count(r => r.HealthStatus == "warning");
        report.ErrorCount = allResults.Count(r => r.HealthStatus == "error");
        report.Results = allResults;

        GroupDuplicates(allResults, report);
        BuildConflicts(allResults.Where(r => r.IsLoaded).ToList(), report);

        return report;
    }

    // ===== Grouping =====

    private static void GroupDuplicates(List<ModAnalysisResult> results, FullAnalysisReport report)
    {
        // Phase 1: Exact buffer hash match (fast path — catches identical buffer sets)
        var exactGroups = results.Where(r => !string.IsNullOrEmpty(r.BufferHash))
            .GroupBy(r => r.BufferHash)
            .Where(g => g.Select(m => m.ModId).Distinct().Count() > 1);

        var groupedModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in exactGroups)
        {
            var mods = group.GroupBy(m => m.ModId).Select(g => g.First()).ToList();
            AddDuplicateGroup(mods, report);
            foreach (var m in mods) groupedModIds.Add(m.ModId);
        }

        // Phase 2: SCORED SIMILARITY — replaces the old buffer-only ≥80%-subset check, which was
        // brittle: single hard cutoff, ignored textures and target hashes entirely, and gave the
        // user no signal of HOW similar a group is. The overlap coefficient (|∩| / min size)
        // handles both cases the old check aimed at (a merged mod CONTAINING another; edited
        // variants sharing most bytes) plus retexture-only variants it missed.
        var resultLookup = results.GroupBy(r => r.ModId).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        GroupSimilar(results, report, groupedModIds, resultLookup);
    }

    /// <summary>Minimum weighted similarity for two mods to land in one Similar group.</summary>
    private const double SimilarityThreshold = 0.70;

    /// <summary>
    /// Overlap coefficient: |A ∩ B| / min(|A|, |B|). 1.0 when one set contains the other — the
    /// right notion for "mod B is mod A plus extras" (merges, added variants). Empty sets score 0
    /// ("no evidence", never "identical") so ini-only mods can't match everything.
    /// </summary>
    private static double Overlap(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        int inter = a.Count < b.Count ? a.Count(b.Contains) : b.Count(a.Contains);
        return (double)inter / Math.Min(a.Count, b.Count);
    }

    /// <summary>
    /// Weighted similarity of two analyzed mods. Asset BYTES dominate: shared target hashes only
    /// say "same character/part", which every mod for that character satisfies — weighting it high
    /// grouped three DIFFERENT outfits that merely shared a base mesh (user report 2026-07-05).
    /// A pair must also clear a hard byte-overlap gate (≥60% of the smaller mod's buffers OR
    /// textures shared) before the weighted score even applies.
    /// Exact clones → 1.0; retexture variant (same buffers) → ~0.75; a merge containing the mod →
    /// ~1.0; different outfits off a common base (~half-shared buffers) → gated out or ~0.55.
    /// </summary>
    private static double SimilarityScore(ModAnalysisResult x, ModAnalysisResult y)
    {
        var tx = new HashSet<string>(x.TargetHashes, StringComparer.OrdinalIgnoreCase);
        var ty = new HashSet<string>(y.TargetHashes, StringComparer.OrdinalIgnoreCase);
        var bx = new HashSet<string>(x.BufferFileHashes, StringComparer.OrdinalIgnoreCase);
        var by = new HashSet<string>(y.BufferFileHashes, StringComparer.OrdinalIgnoreCase);
        var xx = new HashSet<string>(x.TextureFileHashes, StringComparer.OrdinalIgnoreCase);
        var xy = new HashSet<string>(y.TextureFileHashes, StringComparer.OrdinalIgnoreCase);

        var bufferOverlap = Overlap(bx, by);
        var textureOverlap = Overlap(xx, xy);
        if (Math.Max(bufferOverlap, textureOverlap) < 0.6) return 0; // byte-overlap gate

        return 0.2 * Overlap(tx, ty) + 0.55 * bufferOverlap + 0.25 * textureOverlap;
    }

    private static void GroupSimilar(
        List<ModAnalysisResult> results,
        FullAnalysisReport report,
        HashSet<string> groupedModIds,
        Dictionary<string, ModAnalysisResult> resultLookup)
    {
        // Candidates: ungrouped mods with enough signal (a target hash set AND some hashed assets).
        var candidates = results
            .Where(r => !groupedModIds.Contains(r.ModId) &&
                        r.TargetHashes.Count > 0 &&
                        (r.BufferFileHashes.Count > 0 || r.TextureFileHashes.Count > 0))
            .GroupBy(r => r.ModId).Select(g => g.First()).ToList();
        if (candidates.Count < 2) return;

        // Inverted index on target hashes — only pairs overriding at least one shared hash are scored.
        var hashToMods = new Dictionary<string, List<ModAnalysisResult>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in candidates)
            foreach (var h in mod.TargetHashes)
            {
                if (!hashToMods.TryGetValue(h, out var list)) { list = []; hashToMods[h] = list; }
                list.Add(mod);
            }

        var groups = new List<(HashSet<string> Ids, List<double> Scores)>();
        var scoredPairs = new HashSet<string>();

        foreach (var mod in candidates)
        {
            var partners = mod.TargetHashes
                .Where(hashToMods.ContainsKey)
                .SelectMany(h => hashToMods[h])
                .Where(c => !string.Equals(c.ModId, mod.ModId, StringComparison.OrdinalIgnoreCase))
                .GroupBy(c => c.ModId).Select(g => g.First());

            foreach (var partner in partners)
            {
                var pairKey = string.Compare(mod.ModId, partner.ModId, StringComparison.OrdinalIgnoreCase) < 0
                    ? $"{mod.ModId}|{partner.ModId}" : $"{partner.ModId}|{mod.ModId}";
                if (!scoredPairs.Add(pairKey)) continue;

                var score = SimilarityScore(mod, partner);
                if (score < SimilarityThreshold) continue;

                var existing = groups.FirstOrDefault(g => g.Ids.Contains(mod.ModId) || g.Ids.Contains(partner.ModId));
                if (existing.Ids != null) { existing.Ids.Add(mod.ModId); existing.Ids.Add(partner.ModId); existing.Scores.Add(score); }
                else groups.Add((new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mod.ModId, partner.ModId }, [score]));
            }
        }

        foreach (var (ids, scores) in groups)
        {
            var mods = ids.Where(resultLookup.ContainsKey).Select(id => resultLookup[id]).ToList();
            if (mods.Count < 2) continue;
            AddDuplicateGroup(mods, report, DuplicateType.Similar, scores.Average());
            foreach (var m in mods) groupedModIds.Add(m.ModId);
        }
    }

    private static void AddDuplicateGroup(List<ModAnalysisResult> mods, FullAnalysisReport report,
        DuplicateType? forcedType = null, double? similarityScore = null)
    {
        var textureGroups = mods.GroupBy(m => m.TextureHash).ToList();
        var firstHashes = string.Join(",", mods[0].TargetHashes.OrderBy(h => h, StringComparer.OrdinalIgnoreCase));
        var allHashesMatch = mods.Count > 1 && mods.Skip(1).All(m =>
            string.Join(",", m.TargetHashes.OrderBy(h => h, StringComparer.OrdinalIgnoreCase)) == firstHashes);

        // Same-asset groups split into the dedup taxonomy: exact clone (same ini too) vs INI
        // VARIANT — the hash-fixed / keybind-updated copy of the same mod. The diff tokens say
        // WHAT changed so the user can decide which copy to keep.
        DuplicateType type;
        var iniDifferences = new List<string>();
        if (forcedType != null)
        {
            type = forcedType.Value;
        }
        else if (textureGroups.Count > 1)
        {
            type = DuplicateType.TextureVariant;
        }
        else
        {
            if (!allHashesMatch) iniDifferences.Add("hashes");
            iniDifferences.AddRange(CompareIniAspects(mods));
            type = iniDifferences.Count == 0 ? DuplicateType.Identical : DuplicateType.IniVariant;
        }

        // Similar groups: show the hashes the mods actually have in COMMON (first mod's list is
        // misleading when the sets only mostly overlap).
        var sharedHashes = mods.First().TargetHashes;
        if (type == DuplicateType.Similar)
        {
            IEnumerable<string> common = mods.First().TargetHashes;
            foreach (var m in mods.Skip(1))
                common = common.Intersect(m.TargetHashes, StringComparer.OrdinalIgnoreCase);
            sharedHashes = common.ToList();
        }

        report.DuplicateGroups.Add(new DuplicateGroup
        {
            Type = type,
            GroupLabel = mods.First().CategoryName,
            SharedHashes = sharedHashes,
            Mods = mods,
            AllHashesMatch = allHashesMatch,
            SimilarityScore = similarityScore,
            IniDifferences = iniDifferences
        });
        switch (type)
        {
            case DuplicateType.Identical: report.IdenticalCount++; break;
            case DuplicateType.TextureVariant: report.TextureVariantCount++; break;
            case DuplicateType.IniVariant: report.IniVariantCount++; break;
            case DuplicateType.Similar: report.SimilarCount++; break;
        }
    }

    /// <summary>
    /// Which ini ASPECTS differ across a same-asset group: "keys" (bindings), "constants"
    /// (defaults), "logic" (command lists / overrides minus hash lines). Findings without
    /// fingerprints (pre-2026-07 scans) compare as unknown — no aspect tokens are reported.
    /// </summary>
    private static List<string> CompareIniAspects(List<ModAnalysisResult> mods)
    {
        var diffs = new List<string>();
        var parsed = mods.Select(m =>
        {
            try { return m.IniFingerprints == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(m.IniFingerprints); }
            catch { return null; }
        }).ToList();
        if (parsed.Any(p => p == null)) return diffs;

        foreach (var aspect in new[] { "key", "constants", "logic" })
        {
            var values = parsed.Select(p => p!.GetValueOrDefault(aspect, "")).Distinct().ToList();
            if (values.Count > 1)
                diffs.Add(aspect == "key" ? "keys" : aspect);
        }
        return diffs;
    }

    private static void BuildConflicts(List<ModAnalysisResult> loadedMods, FullAnalysisReport report)
    {
        var hashToMods = new Dictionary<string, List<ModAnalysisResult>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in loadedMods)
            foreach (var hash in mod.TargetHashes)
            {
                if (!hashToMods.ContainsKey(hash)) hashToMods[hash] = new List<ModAnalysisResult>();
                hashToMods[hash].Add(mod);
            }
        report.Conflicts = hashToMods.Where(kv => kv.Value.Count > 1).Select(kv => new ModConflict { Hash = kv.Key, Mods = kv.Value }).ToList();
        report.ConflictCount = report.Conflicts.Count;
        report.AffectedModCount = report.Conflicts.SelectMany(c => c.Mods.Select(m => m.ModId)).Distinct().Count();
    }

    // ===== INI Parsing =====
    // Grounded in the authoritative 3DMigoto INI docs (leotorrez.github.io/modding/docs — scraped
    // 2026-07-05; see .claude/rules/3dmigoto-ini-interface.md): hash is 8 hex chars on a
    // TextureOverride and 16 on a ShaderOverride; an *Override matches via hash OR match_*/
    // filter_index (neither → dead section); conditions are if / else if|elif / else / endif with
    // nesting; comments are ';' or fullwidth '；' (IniParser handles both).

    private static readonly Regex HexRegex = new(@"^[0-9a-fA-F]+$", RegexOptions.Compiled);

    /// <summary>Per-aspect ini content accumulator → sha256 fingerprints (key/constants/logic).</summary>
    private sealed class IniAspectAccumulator
    {
        public global::System.Text.StringBuilder Keys { get; } = new();
        public global::System.Text.StringBuilder Constants { get; } = new();
        public global::System.Text.StringBuilder Logic { get; } = new();

        public string ToJson()
        {
            static string Sha(global::System.Text.StringBuilder sb)
            {
                var bytes = SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
                return Convert.ToHexString(bytes);
            }
            return JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["key"] = Sha(Keys),
                ["constants"] = Sha(Constants),
                ["logic"] = Sha(Logic),
            });
        }
    }

    private ModIniStructure ParseIniStructure(string[] lines, string fileName, List<ModHealthIssue> issues, IniAspectAccumulator? aspects = null)
    {
        var structure = new ModIniStructure();
        var doc = IniParser.Parse(lines);
        var seenSectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in doc.Sections)
        {
            // Info, not Warning: real working mods commonly repeat [Constants] etc. in one file
            // (3DMigoto warns but merges them) — 13 hits in a 12-mod library sample.
            if (!seenSectionNames.Add(section.Name))
                issues.Add(new ModHealthIssue { Type = HealthIssueType.DuplicateSection, Severity = HealthIssueSeverity.Info, Message = $"Duplicate section [{section.Name}]", FilePath = fileName });

            // Feed the per-aspect fingerprints: [Key*] / [Constants] / everything else. `hash =`
            // lines are EXCLUDED from logic so a pure hash-fix compares as "same logic".
            if (aspects != null)
            {
                var sink = section.Name.StartsWith("Key", StringComparison.OrdinalIgnoreCase) ? aspects.Keys
                    : section.Name.StartsWith("Constants", StringComparison.OrdinalIgnoreCase) ? aspects.Constants
                    : aspects.Logic;
                sink.Append('[').Append(section.Name).Append("]\n");
                foreach (var entry in section.Entries)
                {
                    if (sink == aspects.Logic && string.Equals(entry.Key, "hash", StringComparison.OrdinalIgnoreCase)) continue;
                    sink.Append(entry.Raw).Append('\n');
                }
            }

            bool isTextureOverride = section.Name.StartsWith("TextureOverride", StringComparison.OrdinalIgnoreCase);
            bool isShaderOverride = section.Name.StartsWith("ShaderOverride", StringComparison.OrdinalIgnoreCase);
            if (isTextureOverride) structure.TextureOverrideCount++;
            if (section.Name.StartsWith("Resource", StringComparison.OrdinalIgnoreCase))
            {
                structure.ResourceCount++;
                foreach (var entry in section.Entries.Where(e => string.Equals(e.Key, "filename", StringComparison.OrdinalIgnoreCase) && e.Value != null))
                {
                    var fn = entry.Value!;
                    var ext = Path.GetExtension(fn).ToLowerInvariant();
                    if (BufferExtensions.Contains(ext)) structure.BufferFiles.Add(fn);
                    else if (TextureExtensions.Contains(ext)) structure.TextureFiles.Add(fn);
                }
            }

            if (isTextureOverride || isShaderOverride)
            {
                var hash = section.GetValue("hash");
                if (hash != null)
                {
                    int expectedLen = isTextureOverride ? 8 : 16;
                    if (!HexRegex.IsMatch(hash) || hash.Length != expectedLen)
                        issues.Add(new ModHealthIssue { Type = HealthIssueType.MalformedHash, Severity = HealthIssueSeverity.Warning, Message = $"[{section.Name}] hash \"{hash}\" is not a {expectedLen}-char hex hash — it will never match", FilePath = fileName });
                    else
                        structure.TargetHashes.Add(hash.ToLowerInvariant());
                }
                else if (!section.HasKeyStartingWith("match_") && !section.HasKey("filter_index"))
                {
                    issues.Add(new ModHealthIssue { Type = HealthIssueType.DeadOverride, Severity = HealthIssueSeverity.Warning, Message = $"[{section.Name}] has no hash and no match_* filter — it never triggers", FilePath = fileName });
                }
            }

            if (section.Name.StartsWith("Key", StringComparison.OrdinalIgnoreCase) &&
                !section.HasKey("key") && !section.HasKey("back"))
            {
                issues.Add(new ModHealthIssue { Type = HealthIssueType.KeyMissingBinding, Severity = HealthIssueSeverity.Warning, Message = $"[{section.Name}] has no key/back binding — it can never fire", FilePath = fileName });
            }

            // if / else if|elif / else / endif balance (nesting allowed; unresolved conditions
            // fail OPEN in 3DMigoto, so a broken block silently draws everything).
            int depth = 0;
            bool underflow = false;
            foreach (var entry in section.Entries)
            {
                var raw = entry.Raw;
                if (raw.StartsWith("if ", StringComparison.OrdinalIgnoreCase) || string.Equals(raw, "if", StringComparison.OrdinalIgnoreCase)) depth++;
                else if (string.Equals(raw, "endif", StringComparison.OrdinalIgnoreCase)) { depth--; if (depth < 0) { underflow = true; break; } }
            }
            // Warning, not Error: 3DMigoto tolerates these (auto-closes an unterminated `if` at
            // section end, ignores a stray `endif`, logging a warning) so the mod still works —
            // and the analyzer offers a one-click repair (ModIniService.RepairConditionBalanceAsync).
            if (depth != 0 || underflow)
                issues.Add(new ModHealthIssue { Type = HealthIssueType.UnbalancedCondition, Severity = HealthIssueSeverity.Warning, Message = $"[{section.Name}] has unbalanced if/endif (repairable)", FilePath = fileName });

            foreach (var entry in section.Entries)
                foreach (var (pattern, plugin) in PluginPatterns)
                    if (entry.Raw.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        structure.PluginReferences.Add(plugin);
        }

        return structure;
    }

    // ===== Mod Deletion Sync (Issue 2) =====

    /// <summary>
    /// Remove a deleted mod from all analysis sessions and update session counts.
    /// Rebuilds the full report per session so duplicate/conflict counts stay accurate.
    /// When a duplicate group dissolves to 1 mod, that mod is no longer a duplicate.
    /// </summary>
    public async Task RemoveModFromAnalysisAsync(string modId)
    {
        var sessions = await _analysisRepository.GetAllSessionsAsync().ConfigureAwait(false);
        await _analysisRepository.DeleteFindingsByModIdAsync(modId).ConfigureAwait(false);

        foreach (var session in sessions)
        {
            if (session.Status != "completed" && session.Status != "cancelled") continue;

            // Rebuild report to get accurate duplicate/conflict counts
            var report = await BuildReportFromSessionAsync(session.Id).ConfigureAwait(false);
            session.TotalMods = Math.Max(session.TotalMods - 1, 0);
            session.AnalyzedCount = report.AnalyzedCount;
            session.HealthyCount = report.HealthyCount;
            session.WarningCount = report.WarningCount;
            session.ErrorCount = report.ErrorCount;
            session.IdenticalCount = report.IdenticalCount;
            session.TextureVariantCount = report.TextureVariantCount;
            session.ConflictCount = report.ConflictCount;
            await _analysisRepository.UpdateSessionAsync(session).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Latest per-mod health from the most recent scan, for the mod-list "last scan" badge.
    /// Returns only non-healthy mods (error/warning) — the list shows no badge for healthy/unscanned.
    /// </summary>
    public async Task<List<ModHealthSummary>> GetLatestHealthAsync()
    {
        var findings = await _analysisRepository.GetLatestFindingPerModAsync().ConfigureAwait(false);
        return findings
            .Where(f => f.HealthStatus == "error" || f.HealthStatus == "warning")
            .Select(f => new ModHealthSummary
            {
                ModId = f.ModId,
                HealthStatus = f.HealthStatus,
                IssueCount = CountJsonArray(f.HealthIssues),
            })
            .ToList();
    }

    /// <summary>Count elements in a JSON array string (HealthIssues), robust to bad/empty JSON.</summary>
    private static int CountJsonArray(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
        }
        catch { return 0; }
    }

    // ===== Helpers =====

    private async Task<List<Mod.Models.ModInfo>> GetAllEnrichedModsAsync()
    {
        var entities = await _modRepository.GetAllAsync().ConfigureAwait(false);
        return await _enrichmentService.EnrichAllAsync(ModMapper.ToDomainList(entities)).ConfigureAwait(false);
    }

    private async Task<string?> ExtractToTempAsync(string modId)
    {
        try
        {
            var tempDir = Path.Combine(_profilePaths.TempDirectory, $"analysis_{modId}");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            var result = await _archiveService.ExtractAsync(modId, tempDir).ConfigureAwait(false);
            return result.Success ? tempDir : null;
        }
        catch (Exception ex) { _logger.Warn($"[ModAnalysis] Extract failed for {modId}: {ex.Message}"); return null; }
    }

    private static void CleanupTempDir(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    private async Task EmitProgress(string sessionId, string stage, int current, int total, string modName, AnalysisStatus status, int healthyCount = 0, int warningCount = 0, int errorCount = 0, string? lastModName = null, string? lastHealthStatus = null)
    {
        // Mirror progress to the status bar / Activity panel.
        if (_currentProcId != null)
        {
            var percent = total > 0 ? (int)((long)current * 100 / total) : (int?)null;
            var detail = stage == "paused" ? "Paused" : (string.IsNullOrEmpty(modName) ? null : modName);
            _processRegistry.Report(_currentProcId, percent, detail);
        }

        await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_ANALYSIS_PROGRESS, new AnalysisProgress
        {
            SessionId = sessionId, Stage = stage, Current = current, Total = total,
            CurrentModName = modName, Status = status,
            HealthyCount = healthyCount, WarningCount = warningCount, ErrorCount = errorCount,
            LastModName = lastModName, LastHealthStatus = lastHealthStatus
        }).ConfigureAwait(false);
    }

    /// <summary>Same digest as before the IHashHelper dedup (2026-07-10): SHA256 over the files'
    /// concatenated bytes, unreadable files skipped — DB rows from older scans still compare equal.</summary>
    private async Task<string> ComputeCombinedHashAsync(List<string> filePaths)
    {
        if (filePaths.Count == 0) return string.Empty;
        return await _hashHelper.CalculateCombinedSHA256Async(filePaths).ConfigureAwait(false);
    }

    private static string? GetModDirectory(string cacheModsDir, string modId, bool isLoaded)
    {
        var a = Path.Combine(cacheModsDir, modId); if (Directory.Exists(a)) return a;
        var d = Path.Combine(cacheModsDir, $"DISABLED-{modId}"); return Directory.Exists(d) ? d : null;
    }

    /// <summary>
    /// Hash each file individually (IHashHelper). Returns sorted list of hashes for set comparison.
    /// </summary>
    private async Task<List<string>> ComputePerFileHashesAsync(List<string> filePaths)
    {
        if (filePaths.Count == 0) return [];
        var hashes = new List<string>(filePaths.Count);
        foreach (var p in filePaths)
        {
            try { hashes.Add(await _hashHelper.CalculateFileSHA256Async(p).ConfigureAwait(false)); }
            catch { /* unreadable file skipped — best-effort set, same as before */ }
        }
        hashes.Sort(StringComparer.OrdinalIgnoreCase);
        return hashes;
    }

    /// <summary>
    /// Check if a referenced file exists anywhere in the mod directory tree.
    /// Checks: iniDir/refFile, modDir/refFile, then searches all subdirectories by filename.
    /// </summary>
    private static bool FileExistsInTree(string modDir, string iniDir, string refFile)
    {
        // Direct path checks (fast path)
        if (File.Exists(Path.Combine(iniDir, refFile))) return true;
        if (File.Exists(Path.Combine(modDir, refFile))) return true;

        // Fallback: search by filename in all subdirectories
        var fileName = Path.GetFileName(refFile);
        try
        {
            var found = Directory.GetFiles(modDir, fileName, SearchOption.AllDirectories);
            return found.Length > 0;
        }
        catch { return false; }
    }

    private static List<string> DeserializeList(string json) { try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; } catch { return []; } }
    private static List<ModHealthIssue> DeserializeIssues(string json) { try { return JsonSerializer.Deserialize<List<ModHealthIssue>>(json) ?? []; } catch { return []; } }
}
