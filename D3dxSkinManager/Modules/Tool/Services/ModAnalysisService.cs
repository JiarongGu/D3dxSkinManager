using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
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
    private static readonly Regex SectionHeaderRegex = new(@"^\[(.+)\]$", RegexOptions.Compiled);
    private static readonly Regex HashRegex = new(@"^hash\s*=\s*([0-9a-fA-F]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FilenameRegex = new(@"^filename\s*=\s*(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] PluginPatterns = [
        @"\ZZMI\", @"\SRMI\", @"\GIMI\", @"\WWMI\",
        @"\ShaderFixes\", @"\RabbitFX\",
        @"CommandList\ZZMI", @"CommandList\SRMI", @"CommandList\GIMI", @"CommandList\WWMI",
        @"Resource\ZZMI", @"Resource\SRMI", @"Resource\GIMI", @"Resource\WWMI",
        @"Resource\ShaderFixes", @"Resource\RabbitFX",
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
        _logger = logger;

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
            resumable: true, resumePayload: session.Id);

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
            resumable: true, resumePayload: session.Id);

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

        var iniFiles = Directory.GetFiles(modDir, "*.ini", SearchOption.AllDirectories);
        if (iniFiles.Length == 0)
            issues.Add(new ModHealthIssue { Type = HealthIssueType.NoIniFile, Severity = HealthIssueSeverity.Error, Message = "No .ini files found" });

        var allFiles = Directory.GetFiles(modDir, "*", SearchOption.AllDirectories);
        int resourceFileCount = allFiles.Count(f =>
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            return BufferExtensions.Contains(ext) || TextureExtensions.Contains(ext);
        });

        if (allFiles.Length <= 1 && iniFiles.Length == 0)
            issues.Add(new ModHealthIssue { Type = HealthIssueType.EmptyMod, Severity = HealthIssueSeverity.Error, Message = "Mod directory is empty" });

        foreach (var iniFile in iniFiles)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(iniFile).ConfigureAwait(false);
                if (lines.Length == 0) { issues.Add(new ModHealthIssue { Type = HealthIssueType.EmptyIniFile, Severity = HealthIssueSeverity.Warning, Message = $"Empty: {Path.GetFileName(iniFile)}" }); continue; }

                var structure = ParseIniStructure(lines);
                foreach (var h in structure.TargetHashes) allTargetHashes.Add(h);
                foreach (var p in structure.PluginReferences) allPluginRefs.Add(p);
                textureOverrideCount += structure.TextureOverrideCount;

                var iniDir = Path.GetDirectoryName(iniFile)!;
                foreach (var refFile in structure.BufferFiles.Concat(structure.TextureFiles))
                {
                    if (!FileExistsInTree(modDir, iniDir, refFile))
                        issues.Add(new ModHealthIssue { Type = HealthIssueType.MissingResource, Severity = HealthIssueSeverity.Warning, Message = $"Missing: {refFile}" });
                }
            }
            catch (Exception ex)
            {
                issues.Add(new ModHealthIssue { Type = HealthIssueType.InvalidIniSyntax, Severity = HealthIssueSeverity.Error, Message = $"Parse error: {ex.Message}" });
            }
        }

        // Plugin presence check
        var tdMigotoDir = _profilePaths.TdMigotoDirectory;
        foreach (var plugin in allPluginRefs)
        {
            if (!Directory.Exists(Path.Combine(tdMigotoDir, plugin)) && !Directory.Exists(Path.Combine(_profilePaths.CacheModsDirectory, plugin)))
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
                PluginDependencies = DeserializeList(f.PluginDependencies), PreviewPath = previewPath
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

        // Phase 2: Per-file hash overlap (catches merged mods — one contains another's buffers)
        var ungroupedWithHashes = results
            .Where(r => !groupedModIds.Contains(r.ModId) && r.BufferFileHashes.Count > 0)
            .GroupBy(r => r.ModId).Select(g => g.First()).ToList();

        // Build inverted index: file hash → mod IDs
        var fileHashToMods = new Dictionary<string, List<ModAnalysisResult>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in ungroupedWithHashes)
            foreach (var fh in mod.BufferFileHashes)
            {
                if (!fileHashToMods.TryGetValue(fh, out var list)) { list = []; fileHashToMods[fh] = list; }
                list.Add(mod);
            }

        // Find mod pairs with significant buffer file overlap
        var overlapGroups = new List<HashSet<string>>();
        var checkedPairs = new HashSet<string>();

        foreach (var mod in ungroupedWithHashes)
        {
            if (groupedModIds.Contains(mod.ModId)) continue;
            var modHashSet = new HashSet<string>(mod.BufferFileHashes, StringComparer.OrdinalIgnoreCase);

            // Find candidate mods that share at least one buffer file hash
            var candidates = mod.BufferFileHashes
                .Where(fileHashToMods.ContainsKey)
                .SelectMany(fh => fileHashToMods[fh])
                .Where(c => c.ModId != mod.ModId && !groupedModIds.Contains(c.ModId))
                .GroupBy(c => c.ModId).Select(g => g.First());

            foreach (var candidate in candidates)
            {
                var pairKey = string.Compare(mod.ModId, candidate.ModId, StringComparison.OrdinalIgnoreCase) < 0
                    ? $"{mod.ModId}|{candidate.ModId}" : $"{candidate.ModId}|{mod.ModId}";
                if (!checkedPairs.Add(pairKey)) continue;

                var candidateHashSet = new HashSet<string>(candidate.BufferFileHashes, StringComparer.OrdinalIgnoreCase);
                int sharedCount = modHashSet.Count(h => candidateHashSet.Contains(h));
                int smallerSet = Math.Min(modHashSet.Count, candidateHashSet.Count);

                // One mod's buffer files are a subset of the other (or ≥80% overlap with smaller set)
                if (smallerSet > 0 && (double)sharedCount / smallerSet >= 0.8)
                {
                    // Merge into existing overlap group or create new one
                    var existingGroup = overlapGroups.FirstOrDefault(g => g.Contains(mod.ModId) || g.Contains(candidate.ModId));
                    if (existingGroup != null) { existingGroup.Add(mod.ModId); existingGroup.Add(candidate.ModId); }
                    else overlapGroups.Add(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mod.ModId, candidate.ModId });
                }
            }
        }

        // Convert overlap groups to DuplicateGroups
        var resultLookup = results.GroupBy(r => r.ModId).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var group in overlapGroups)
        {
            var mods = group.Where(resultLookup.ContainsKey).Select(id => resultLookup[id]).ToList();
            if (mods.Count > 1)
            {
                AddDuplicateGroup(mods, report);
                foreach (var m in mods) groupedModIds.Add(m.ModId);
            }
        }
    }

    private static void AddDuplicateGroup(List<ModAnalysisResult> mods, FullAnalysisReport report)
    {
        var textureGroups = mods.GroupBy(m => m.TextureHash).ToList();
        var type = textureGroups.Count == 1 ? DuplicateType.Identical : DuplicateType.TextureVariant;

        var allHashesMatch = false;
        if (type == DuplicateType.Identical && mods.Count > 1)
        {
            var firstHashes = string.Join(",", mods[0].TargetHashes.OrderBy(h => h, StringComparer.OrdinalIgnoreCase));
            allHashesMatch = mods.Skip(1).All(m =>
                string.Join(",", m.TargetHashes.OrderBy(h => h, StringComparer.OrdinalIgnoreCase)) == firstHashes);
        }

        report.DuplicateGroups.Add(new DuplicateGroup
        {
            Type = type,
            GroupLabel = mods.First().CategoryName,
            SharedHashes = mods.First().TargetHashes,
            Mods = mods,
            AllHashesMatch = allHashesMatch
        });
        if (type == DuplicateType.Identical) report.IdenticalCount++; else report.TextureVariantCount++;
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

    private ModIniStructure ParseIniStructure(string[] lines)
    {
        var structure = new ModIniStructure();
        bool inTextureOverride = false, inResource = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";")) continue;
            var ci = trimmed.IndexOf(';'); if (ci > 0) trimmed = trimmed[..ci].TrimEnd();

            var sm = SectionHeaderRegex.Match(trimmed);
            if (sm.Success) { var sn = sm.Groups[1].Value; inTextureOverride = sn.StartsWith("TextureOverride", StringComparison.OrdinalIgnoreCase); inResource = sn.StartsWith("Resource", StringComparison.OrdinalIgnoreCase); if (inTextureOverride) structure.TextureOverrideCount++; if (inResource) structure.ResourceCount++; continue; }

            if (inTextureOverride) { var hm = HashRegex.Match(trimmed); if (hm.Success) structure.TargetHashes.Add(hm.Groups[1].Value.ToLowerInvariant()); }
            if (inResource) { var fm = FilenameRegex.Match(trimmed); if (fm.Success) { var fn = fm.Groups[1].Value.Trim(); var ext = Path.GetExtension(fn).ToLowerInvariant(); if (BufferExtensions.Contains(ext)) structure.BufferFiles.Add(fn); else if (TextureExtensions.Contains(ext)) structure.TextureFiles.Add(fn); } }

            foreach (var pattern in PluginPatterns)
                if (trimmed.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    structure.PluginReferences.Add(pattern.Trim('\\').Split('\\')[0]);
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

    private static async Task<string> ComputeCombinedHashAsync(List<string> filePaths)
    {
        if (filePaths.Count == 0) return string.Empty;
        using var sha256 = SHA256.Create(); using var stream = new MemoryStream();
        foreach (var p in filePaths) { try { var b = await File.ReadAllBytesAsync(p).ConfigureAwait(false); stream.Write(b, 0, b.Length); } catch { } }
        stream.Position = 0; var hash = await sha256.ComputeHashAsync(stream).ConfigureAwait(false);
        return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
    }

    private static string? GetModDirectory(string cacheModsDir, string modId, bool isLoaded)
    {
        var a = Path.Combine(cacheModsDir, modId); if (Directory.Exists(a)) return a;
        var d = Path.Combine(cacheModsDir, $"DISABLED-{modId}"); return Directory.Exists(d) ? d : null;
    }

    /// <summary>
    /// Hash each file individually. Returns sorted list of hashes for set comparison.
    /// </summary>
    private static async Task<List<string>> ComputePerFileHashesAsync(List<string> filePaths)
    {
        if (filePaths.Count == 0) return [];
        var hashes = new List<string>(filePaths.Count);
        using var sha256 = SHA256.Create();
        foreach (var p in filePaths)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(p).ConfigureAwait(false);
                var hash = sha256.ComputeHash(bytes);
                hashes.Add(BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant());
            }
            catch { }
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
