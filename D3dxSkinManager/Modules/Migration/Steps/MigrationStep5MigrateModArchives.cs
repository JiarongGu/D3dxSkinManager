using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Parsers;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Category.Services;

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
    private readonly IModMetadataService _metadataService;
    private readonly ICategoryService _categoryService;
    private readonly IModRepository _modRepository;
    private readonly ILogHelper _logger;

    public int StepNumber => 5;
    public string StepName => "Migrate Mod Archives";

    public MigrationStep5MigrateModArchives(
        IProfilePathService profilePaths,
        IFileHelper fileService,
        IArchiveHelper archiveService,
        IPythonModIndexParser modIndexParser,
        IModMetadataService metadataService,
        ICategoryService categoryService,
        IModRepository modRepository,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _fileService = fileService;
        _archiveService = archiveService;
        _modIndexParser = modIndexParser;
        _metadataService = metadataService;
        _categoryService = categoryService;
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
        int processed = 0;

        foreach (var modEntry in modEntries)
        {
            try
            {
                // Check if mod already exists in database
                var existingMod = await _modRepository.GetByIdAsync(modEntry.Sha).ConfigureAwait(false);

                // Query database for Category ID by object name
                string? categoryId = null;
                if (!string.IsNullOrEmpty(modEntry.Object))
                {
                    var categoryInfo = await _categoryService.GetByNameAsync(modEntry.Object).ConfigureAwait(false);
                    if (categoryInfo != null)
                    {
                        categoryId = categoryInfo.Id;
                        _logger.Verbose($"Mapped '{modEntry.Object}' -> ID: {categoryId}", "Migration");
                    }
                    else
                    {
                        _logger.Warn($"No Category found for object '{modEntry.Object}', mod will be unclassified", "Migration");
                    }
                }

                if (existingMod != null)
                {
                    // Mod exists - update its category if it's different
                    // If no category found, use empty string (unclassified)
                    var newCategory = categoryId ?? string.Empty;
                    if (existingMod.Category != newCategory)
                    {
                        _logger.Verbose($"Updating category for existing mod: {modEntry.Name} ({modEntry.Sha}) from '{existingMod.Category}' to '{newCategory}'", "Migration");
                        existingMod.Category = newCategory;
                        await _modRepository.UpdateAsync(existingMod).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.Verbose($"Skipping existing mod (category unchanged): {modEntry.Name} ({modEntry.Sha})", "Migration");
                    }

                    processed++;

                    // Report progress even for skipped mods
                    progress?.Report(new MigrationProgress
                    {
                        Stage = MigrationStage.MigratingMetadata,
                        CurrentTask = $"Checked: {modEntry.Name}",
                        ProcessedItems = processed,
                        TotalItems = modEntries.Count,
                        PercentComplete = 40 + (20 * processed / Math.Max(1, modEntries.Count))
                    });

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

                // Create mod entry using service (categoryId was already looked up above)
                var mod = await _metadataService.GetOrCreateAsync(
                    modEntry.Sha,
                    new CreateModRequest
                    {
                        Id = modEntry.Sha,
                        Category = categoryId, // Uses Category ID from database lookup
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
                    _logger.Verbose($"Migrated mod: {modEntry.Name} ({modEntry.Sha})", "Migration");
                }

                processed++;

                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.MigratingMetadata,
                    CurrentTask = $"Migrating {modEntry.Name}...",
                    ProcessedItems = processed,
                    TotalItems = modEntries.Count,
                    PercentComplete = 40 + (20 * processed / Math.Max(1, modEntries.Count))
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"ERROR migrating mod {modEntry.Name}: {ex.Message}", "Migration", ex);

                // Add detailed error information
                context.Result.DetailedErrors.Add(new MigrationError
                {
                    Message = ex.Message,
                    MessageCode = "MOD_MIGRATION_FAILED",
                    ModName = modEntry.Name,
                    ModSha = modEntry.Sha,
                    StepCode = "MIGRATE_MOD_ARCHIVES",
                    CategoryCode = "MOD_MIGRATION",
                    Timestamp = DateTime.UtcNow
                });

                // Also add to simple errors list for backward compatibility
                context.Result.Errors.Add($"Mod '{modEntry.Name}': {ex.Message}");
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
