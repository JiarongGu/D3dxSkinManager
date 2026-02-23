using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Parsers;
using D3dxSkinManager.Modules.Mods.Services;

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
    private readonly IFileHelper _fileService;
    private readonly IArchiveHelper _archiveService;
    private readonly IPythonModIndexParser _modIndexParser;
    private readonly IModManagementService _modManagementService;
    private readonly IClassificationService _classificationService;
    private readonly IModRepository _modRepository;
    private readonly ILogHelper _logger;

    public int StepNumber => 5;
    public string StepName => "Migrate Mod Archives";

    public MigrationStep5MigrateModArchives(
        IProfilePathService profilePaths,
        IFileHelper fileService,
        IArchiveHelper archiveService,
        IPythonModIndexParser modIndexParser,
        IModManagementService modManagementService,
        IClassificationService classificationService,
        IModRepository modRepository,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _fileService = fileService;
        _archiveService = archiveService;
        _modIndexParser = modIndexParser;
        _modManagementService = modManagementService;
        _classificationService = classificationService;
        _modRepository = modRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.Options.MigrateMetadata && !context.Options.MigrateArchives)
        {
            _logger.Info("Step 5: Skipping mods (disabled)", "Migration");
            return;
        }

        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.MigratingMetadata,
            CurrentTask = "Migrating mod archives...",
            PercentComplete = 50
        });

        _logger.Info("Step 5: Migrating mod archives and metadata", "Migration");

        // Parse mod index files
        var modsIndexPath = Path.Combine(context.EnvironmentPath!, "modsIndex");
        if (!Directory.Exists(modsIndexPath))
        {
            _logger.Warn("WARNING: modsIndex directory not found", "Migration");
            return;
        }

        var modEntries = await _modIndexParser.ParseAsync(modsIndexPath).ConfigureAwait(false);
        _logger.Info($"Found {modEntries.Count} mod entries in index files", "Migration");

        // Migrate mod archives and create mod entries
        int copied = 0;
        int created = 0;

        foreach (var modEntry in modEntries)
        {
            try
            {
                // Check if mod already exists in database (skip if exists)
                var existingMod = await _modRepository.GetByIdAsync(modEntry.Sha).ConfigureAwait(false);
                if (existingMod != null)
                {
                    _logger.Info($"Skipping existing mod: {modEntry.Name} ({modEntry.Sha})", "Migration");
                    continue;
                }

                // Copy mod archive
                // Python version stores archives in resources/mods/ without file extension
                // We maintain the same approach - SharpCompress auto-detects format
                var sourceArchivePath = Path.Combine(context.Options.SourcePath, "resources", "mods", modEntry.Sha);
                if (!File.Exists(sourceArchivePath))
                {
                    _logger.Warn($"Archive not found for {modEntry.Name} ({modEntry.Sha})", "Migration");
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
                    archiveType = await DetectArchiveTypeAsync(sourceArchivePath).ConfigureAwait(false);
                    _logger.Info($"Detected archive type '{archiveType}' for {modEntry.Name}", "Migration");
                }

                // Store without extension (like Python version)
                var destArchivePath = _profilePaths.GetModArchivePath(modEntry.Sha, "");
                Directory.CreateDirectory(Path.GetDirectoryName(destArchivePath)!);

                // Handle archive according to selected mode (Copy or Move)
                if (context.Options.ArchiveMode == ArchiveHandling.Move)
                {
                    await _fileService.MoveFileAsync(sourceArchivePath, destArchivePath).ConfigureAwait(false);
                }
                else
                {
                    await _fileService.CopyFileAsync(sourceArchivePath, destArchivePath, overwrite: false).ConfigureAwait(false);
                }
                copied++;

                // Query database for classification ID by object name
                string? classificationId = null;
                if (!string.IsNullOrEmpty(modEntry.Object))
                {
                    var classificationNode = await _classificationService.GetNodeByNameAsync(modEntry.Object).ConfigureAwait(false);
                    if (classificationNode != null)
                    {
                        classificationId = classificationNode.Id;
                        _logger.Info($"Mapped '{modEntry.Object}' → ID: {classificationId}", "Migration");
                    }
                    else
                    {
                        _logger.Warn($"No classification found for object '{modEntry.Object}', mod will be unclassified", "Migration");
                    }
                }

                // Create mod entry using service
                var mod = await _modManagementService.GetOrCreateModAsync(
                    modEntry.Sha,
                    new CreateModRequest
                    {
                        SHA = modEntry.Sha,
                        Category = classificationId, // Uses classification ID from database lookup
                        Name = modEntry.Name,
                        Author = modEntry.Author,
                        Description = modEntry.Explain,
                        Type = archiveType,
                        Grading = modEntry.Grading,
                        Tags = modEntry.Tags
                    }
                ).ConfigureAwait(false);

                if (mod != null)
                {
                    created++;
                    _logger.Info($"Migrated mod: {modEntry.Name} ({modEntry.Sha})", "Migration");
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
                _logger.Error($"ERROR migrating mod {modEntry.Name}: {ex.Message}", "Migration", ex);
            }
        }

        context.Result.ArchivesCopied = copied;
        context.Result.ModsMigrated = created;

        _logger.Info($"Copied {copied} archives, created {created} mod entries", "Migration");
        _logger.Info($"Step 5 complete: {copied} archives, {created} mods", "Migration");
    }

    /// <summary>
    /// Detect archive type using ArchiveService
    /// </summary>
    private async Task<string> DetectArchiveTypeAsync(string filePath)
    {
        try
        {
            var detectedType = await _archiveService.DetectArchiveTypeAsync(filePath).ConfigureAwait(false);
            return detectedType ?? "zip"; // Default fallback if detection fails
        }
        catch (Exception ex)
        {
            _logger.Error($"Error detecting archive type for {Path.GetFileName(filePath)}: {ex.Message}", "Migration", ex);
            return "zip"; // Default fallback
        }
    }
}
