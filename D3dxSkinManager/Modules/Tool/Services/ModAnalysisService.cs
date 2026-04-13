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
    Task<FullAnalysisReport> StartAnalysisAsync(string? categoryId = null);
    void PauseAnalysis();
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
    private volatile bool _isRunning;
    private string? _currentSessionId;

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
        if (_isRunning) _pauseRequested = true;
    }

    // ===== Session Management =====

    public async Task<List<AnalysisSessionSummary>> GetSessionHistoryAsync()
    {
        var sessions = await _analysisRepository.GetAllSessionsAsync().ConfigureAwait(false);
        return sessions.Select(s => new AnalysisSessionSummary
        {
            Id = s.Id, CategoryId = s.CategoryId, CategoryName = s.CategoryName,
            Status = s.Status, TotalMods = s.TotalMods, AnalyzedCount = s.AnalyzedCount,
            HealthyCount = s.HealthyCount, WarningCount = s.WarningCount, ErrorCount = s.ErrorCount,
            IdenticalCount = s.IdenticalCount, TextureVariantCount = s.TextureVariantCount,
            ConflictCount = s.ConflictCount, StartedAt = s.StartedAt, CompletedAt = s.CompletedAt
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

            // Update session with final counts
            session.Status = _pauseRequested ? "paused" : "completed";
            session.AnalyzedCount = report.AnalyzedCount;
            session.HealthyCount = report.HealthyCount;
            session.WarningCount = report.WarningCount;
            session.ErrorCount = report.ErrorCount;
            session.IdenticalCount = report.IdenticalCount;
            session.TextureVariantCount = report.TextureVariantCount;
            session.ConflictCount = report.ConflictCount;
            session.CompletedAt = _pauseRequested ? null : DateTime.UtcNow.ToString("o");
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
        return await BuildReportFromSessionAsync(sessionId).ConfigureAwait(false);
    }

    // ===== Phase 1: Per-Mod Analysis =====

    private async Task RunPerModAnalysisAsync(AnalysisSessionEntity session, List<Mod.Models.ModInfo> mods)
    {
        var cacheModsDir = _profilePaths.CacheModsDirectory;
        int total = mods.Count;
        int processed = 0;
        int healthyCount = 0, warningCount = 0, errorCount = 0;

        foreach (var mod in mods)
        {
            if (_pauseRequested)
            {
                await EmitProgress(session.Id, "paused", processed, total, mod.Name, AnalysisStatus.Paused, healthyCount, warningCount, errorCount).ConfigureAwait(false);
                return;
            }

            processed++;
            await EmitProgress(session.Id, "analyzing", processed, total, mod.Name, AnalysisStatus.Running, healthyCount, warningCount, errorCount).ConfigureAwait(false);

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

                    // Track live counts
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
                    errorCount++;
                }
                finally
                {
                    if (needsCleanup) CleanupTempDir(modDir);
                }
            }
        }

        await EmitProgress(session.Id, "complete", total, total, "", AnalysisStatus.Completed, healthyCount, warningCount, errorCount).ConfigureAwait(false);
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
                issues.Add(new ModHealthIssue { Type = HealthIssueType.MissingPlugin, Severity = HealthIssueSeverity.Warning, Message = $"Plugin not found: {plugin}" });
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
            TotalMods = session?.TotalMods ?? 0,
            AnalyzedCount = findings.Count,
            SkippedCount = (session?.TotalMods ?? 0) - findings.Count,
            Status = session?.Status switch { "paused" => AnalysisStatus.Paused, "completed" => AnalysisStatus.Completed, "running" => AnalysisStatus.Running, _ => AnalysisStatus.Idle }
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
        var bufferGroups = results.Where(r => !string.IsNullOrEmpty(r.BufferHash)).GroupBy(r => r.BufferHash).Where(g => g.Count() > 1);
        foreach (var group in bufferGroups)
        {
            var mods = group.ToList();
            var textureGroups = mods.GroupBy(m => m.TextureHash).ToList();
            var type = textureGroups.Count == 1 ? DuplicateType.Identical : DuplicateType.TextureVariant;
            report.DuplicateGroups.Add(new DuplicateGroup { Type = type, GroupLabel = mods.First().CategoryName, SharedHashes = mods.First().TargetHashes, Mods = mods });
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

    private async Task EmitProgress(string sessionId, string stage, int current, int total, string modName, AnalysisStatus status, int healthyCount = 0, int warningCount = 0, int errorCount = 0)
    {
        await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_ANALYSIS_PROGRESS, new AnalysisProgress
        {
            SessionId = sessionId, Stage = stage, Current = current, Total = total,
            CurrentModName = modName, Status = status,
            HealthyCount = healthyCount, WarningCount = warningCount, ErrorCount = errorCount
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
