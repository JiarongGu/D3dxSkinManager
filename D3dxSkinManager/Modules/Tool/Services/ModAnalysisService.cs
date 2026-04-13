using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Modules.Tool.Services;

public interface IModAnalysisService
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
}

public class ModAnalysisService : IModAnalysisService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IModRepository _modRepository;
    private readonly IModEnrichmentService _enrichmentService;
    private readonly IModArchiveService _archiveService;
    private readonly IModAnalysisRepository _analysisRepository;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;

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
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _modRepository = modRepository;
        _enrichmentService = enrichmentService;
        _archiveService = archiveService;
        _analysisRepository = analysisRepository;
        _eventBus = eventBus;
        _logger = logger;
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

        try
        {
            // Get all mods for this session's scope
            var enrichedMods = await GetAllEnrichedModsAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(session.CategoryId))
                enrichedMods = enrichedMods.Where(m => m.Category == session.CategoryId).ToList();

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

            return report;
        }
        finally
        {
            _isRunning = false;
            _currentSessionId = null;
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

        // Create session
        var enrichedMods = await GetAllEnrichedModsAsync().ConfigureAwait(false);
        if (!string.IsNullOrEmpty(categoryId))
            enrichedMods = enrichedMods.Where(m => m.Category == categoryId).ToList();

        string? categoryName = enrichedMods.FirstOrDefault()?.CategoryName;
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

            return report;
        }
        finally
        {
            _isRunning = false;
            _currentSessionId = null;
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
                    if (!File.Exists(Path.Combine(iniDir, refFile)) && !File.Exists(Path.Combine(modDir, refFile)))
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

        return new AnalysisFindingEntity
        {
            SessionId = sessionId, ModId = modId,
            TargetHashes = JsonSerializer.Serialize(allTargetHashes.ToList()),
            BufferHash = await ComputeCombinedHashAsync(bufferFiles).ConfigureAwait(false),
            TextureHash = await ComputeCombinedHashAsync(textureFiles).ConfigureAwait(false),
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
        var bufferGroups = results.Where(r => !string.IsNullOrEmpty(r.BufferHash)).GroupBy(r => r.BufferHash).Where(g => g.Select(m => m.ModId).Distinct().Count() > 1);
        foreach (var group in bufferGroups)
        {
            var mods = group.GroupBy(m => m.ModId).Select(g => g.First()).ToList();
            var textureGroups = mods.GroupBy(m => m.TextureHash).ToList();
            var type = textureGroups.Count == 1 ? DuplicateType.Identical : DuplicateType.TextureVariant;

            // Check if all mods target the exact same TextureOverride hashes
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

    private static List<string> DeserializeList(string json) { try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; } catch { return []; } }
    private static List<ModHealthIssue> DeserializeIssues(string json) { try { return JsonSerializer.Deserialize<List<ModHealthIssue>>(json) ?? []; } catch { return []; } }
}
