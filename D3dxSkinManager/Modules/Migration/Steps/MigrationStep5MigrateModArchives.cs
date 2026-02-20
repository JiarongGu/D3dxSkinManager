using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Parsers;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Migration.Steps;

/// <summary>
/// Step 5: Migrate mod archives and metadata
/// Parses mod index files, copies mod archives, and creates mod entries
/// Uses: PythonModIndexParser to parse modsIndex/index_*.json
/// Uses: ModManagementService to create mods (not direct repository!)
/// Uses: FileService for copying archives
/// </summary>
public class MigrationStep5MigrateModArchives : IMigrationStep
{
    private readonly IProfilePathService _profilePaths;
    private readonly IFileService _fileService;
    private readonly IArchiveService _archiveService;
    private readonly IPythonModIndexParser _modIndexParser;
    private readonly IModManagementService _modManagementService;
    private readonly ILogHelper _logger;

    public int StepNumber => 5;
    public string StepName => "Migrate Mod Archives";

    public MigrationStep5MigrateModArchives(
        IProfilePathService profilePaths,
        IFileService fileService,
        IArchiveService archiveService,
        IPythonModIndexParser modIndexParser,
        IModManagementService modManagementService,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _fileService = fileService;
        _archiveService = archiveService;
        _modIndexParser = modIndexParser;
        _modManagementService = modManagementService;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.Options.MigrateMetadata && !context.Options.MigrateArchives)
        {
            await LogAsync(context.LogPath, "Step 5: Skipping mods (disabled)");
            return;
        }

        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.MigratingMetadata,
            CurrentTask = "Migrating mod archives...",
            PercentComplete = 50
        });

        await LogAsync(context.LogPath, "Step 5: Migrating mod archives and metadata");

        // Parse mod index files
        var modsIndexPath = Path.Combine(context.EnvironmentPath!, "modsIndex");
        if (!Directory.Exists(modsIndexPath))
        {
            await LogAsync(context.LogPath, "WARNING: modsIndex directory not found");
            return;
        }

        var modEntries = await _modIndexParser.ParseAsync(modsIndexPath);
        await LogAsync(context.LogPath, $"Found {modEntries.Count} mod entries in index files");

        // Migrate mod archives and create mod entries
        int copied = 0;
        int created = 0;

        foreach (var modEntry in modEntries)
        {
            try
            {
                // Copy mod archive
                // Python version stores archives in resources/mods/ without file extension
                // We maintain the same approach - SharpCompress auto-detects format
                var sourceArchivePath = Path.Combine(context.Options.SourcePath, "resources", "mods", modEntry.Sha);
                if (!File.Exists(sourceArchivePath))
                {
                    await LogAsync(context.LogPath, $"WARNING: Archive not found for {modEntry.Name} ({modEntry.Sha})");
                    continue;
                }

                // Detect archive type if not specified in index
                string archiveType;
                if (!string.IsNullOrEmpty(modEntry.Type))
                {
                    archiveType = modEntry.Type;
                }
                else
                {
                    // Try to detect from file header
                    archiveType = await DetectArchiveTypeAsync(sourceArchivePath);
                    await LogAsync(context.LogPath, $"INFO: Detected archive type '{archiveType}' for {modEntry.Name}");
                }

                // Store without extension (like Python version)
                var destArchivePath = _profilePaths.GetModArchivePath(modEntry.Sha, "");
                Directory.CreateDirectory(Path.GetDirectoryName(destArchivePath)!);

                await _fileService.CopyFileAsync(sourceArchivePath, destArchivePath, overwrite: false);
                copied++;

                // Create mod entry using service
                var mod = await _modManagementService.GetOrCreateModAsync(
                    modEntry.Sha,
                    new CreateModRequest
                    {
                        SHA = modEntry.Sha,
                        Category = modEntry.Object,
                        Name = modEntry.Name,
                        Author = modEntry.Author,
                        Description = modEntry.Explain,
                        Type = archiveType,
                        Grading = modEntry.Grading,
                        Tags = modEntry.Tags
                    }
                );

                if (mod != null)
                {
                    created++;
                    await LogAsync(context.LogPath, $"Migrated mod: {modEntry.Name} ({modEntry.Sha})");
                }

                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.MigratingMetadata,
                    CurrentTask = $"Migrating {modEntry.Name}...",
                    ProcessedItems = created,
                    TotalItems = modEntries.Count,
                    PercentComplete = 40 + (20 * created / Math.Max(1, modEntries.Count))
                });
            }
            catch (Exception ex)
            {
                await LogAsync(context.LogPath, $"ERROR migrating mod {modEntry.Name}: {ex.Message}");
            }
        }

        context.Result.ArchivesCopied = copied;
        context.Result.ModsMigrated = created;

        await LogAsync(context.LogPath, $"Copied {copied} archives, created {created} mod entries");
        _logger.Info($"Step 5 complete: {copied} archives, {created} mods", "Migration");
    }

    /// <summary>
    /// Detect archive type using ArchiveService
    /// </summary>
    private async Task<string> DetectArchiveTypeAsync(string filePath)
    {
        try
        {
            var detectedType = await _archiveService.DetectArchiveTypeAsync(filePath);
            return detectedType ?? "zip"; // Default fallback if detection fails
        }
        catch (Exception ex)
        {
            _logger.Error($"Error detecting archive type for {Path.GetFileName(filePath)}: {ex.Message}", "Migration", ex);
            return "zip"; // Default fallback
        }
    }

    private async Task LogAsync(string logPath, string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            await File.AppendAllTextAsync(logPath, logMessage + Environment.NewLine);
            _logger.Info(message, "Migration");
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
