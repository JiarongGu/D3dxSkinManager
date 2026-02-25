using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Parsers;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Category.Services;

namespace D3dxSkinManager.Modules.Migration.Steps;

/// <summary>
/// Step 4: Migrate Category thumbnails
/// Copies thumbnail files from Python environment and associates them with Category nodes
/// Uses: PythonRedirectionFileParser to parse _redirection.ini
/// Uses: CategoryService to associate thumbnails with nodes
/// Uses: FileService for file operations
/// </summary>
public class MigrationStep4MigrateCategoryThumbnails : IMigrationStep
{
    private readonly IProfilePathService _profilePaths;
    private readonly IFileHelper _fileService;
    private readonly IPythonRedirectionFileParser _redirectionParser;
    private readonly ICategoryService _categoryService;
    private readonly ILogHelper _logger;

    public int StepNumber => 4;
    public string StepName => "Migrate Category Thumbnails";

    public MigrationStep4MigrateCategoryThumbnails(
        IProfilePathService profilePaths,
        IFileHelper fileService,
        IPythonRedirectionFileParser redirectionParser,
        ICategoryService categoryService,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _fileService = fileService;
        _redirectionParser = redirectionParser;
        _categoryService = categoryService;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.Options.MigratePreviews)
        {
            await LogAsync(context.LogPath, "Step 4: Skipping Category thumbnails (disabled)");
            return;
        }

        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.CopyingPreviews,
            CurrentTask = "Migrating Category thumbnails...",
            PercentComplete = 45
        });

        await LogAsync(context.LogPath, "Step 4: Migrating Category thumbnails").ConfigureAwait(false);

        var thumbnailsCopied = await MigrateCategoryThumbnailsAsync(context.EnvironmentPath!, context.LogPath).ConfigureAwait(false);
        await LogAsync(context.LogPath, $"Migrated {thumbnailsCopied} Category thumbnails").ConfigureAwait(false);

        _logger.Info($"Step 4 complete: {thumbnailsCopied} thumbnails", "Migration");
    }

    private async Task<int> MigrateCategoryThumbnailsAsync(string envPath, string logPath)
    {
        var sourceThumbnailDir = Path.Combine(envPath, "thumbnail");

        if (!Directory.Exists(sourceThumbnailDir))
        {
            await LogAsync(logPath, "WARNING: Thumbnail directory not found").ConfigureAwait(false);
            return 0;
        }

        var destThumbnailsDir = _profilePaths.ThumbnailsDirectory;
        Directory.CreateDirectory(destThumbnailsDir);

        try
        {
            // ??Use FileService to copy directory
            await _fileService.CopyDirectoryAsync(sourceThumbnailDir, destThumbnailsDir, overwrite: true).ConfigureAwait(false);

            // Delete _redirection.ini from destination (Python-specific)
            var destRedirectionFile = Path.Combine(destThumbnailsDir, "_redirection.ini");
            if (File.Exists(destRedirectionFile))
            {
                File.Delete(destRedirectionFile);
            }

            var copiedCount = Directory.GetFiles(destThumbnailsDir, "*", SearchOption.AllDirectories).Length;
            await LogAsync(logPath, $"Copied {copiedCount} thumbnail files").ConfigureAwait(false);

            // ??Parse _redirection.ini and associate thumbnails with Category nodes
            var redirectionFile = Path.Combine(sourceThumbnailDir, "_redirection.ini");
            if (File.Exists(redirectionFile))
            {
                try
                {
                    // Get statistics for logging
                    var stats = await _redirectionParser.GetStatisticsAsync(redirectionFile).ConfigureAwait(false);
                    await LogAsync(logPath, $"_redirection.ini statistics: {stats}").ConfigureAwait(false);

                    // Parse redirection file to get character->thumbnail mappings
                    var mappings = await _redirectionParser.ParseAsync(redirectionFile).ConfigureAwait(false);
                    int associatedCount = 0;

                    // Associate thumbnails with Category nodes
                    foreach (var (characterName, thumbnailRelativePath) in mappings)
                    {
                        var thumbnailFullPath = Path.Combine(destThumbnailsDir, thumbnailRelativePath);

                        if (!File.Exists(thumbnailFullPath))
                            continue;

                        // Find node by character name and set thumbnail
                        var node = await _categoryService.GetByNameAsync(characterName).ConfigureAwait(false);
                        if (node != null)
                        {
                            var updated = await _categoryService.UpdateThumbnailAsync(node.Id, thumbnailFullPath).ConfigureAwait(false);
                            if (updated)
                                associatedCount++;
                        }
                    }

                    await LogAsync(logPath, $"Associated {associatedCount} thumbnails with nodes").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await LogAsync(logPath, $"ERROR processing _redirection.ini: {ex.Message}").ConfigureAwait(false);
                }
            }

            return copiedCount;
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR copying thumbnails: {ex.Message}").ConfigureAwait(false);
            return 0;
        }
    }

    private async Task LogAsync(string logPath, string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            await File.AppendAllTextAsync(logPath, logMessage + Environment.NewLine).ConfigureAwait(false);
            _logger.Info(message, "Migration");
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
