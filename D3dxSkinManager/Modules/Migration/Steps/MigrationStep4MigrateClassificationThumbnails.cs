using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Parsers;
using D3dxSkinManager.Modules.Mods.Services;

namespace D3dxSkinManager.Modules.Migration.Steps;

/// <summary>
/// Step 4: Migrate classification thumbnails
/// Copies thumbnail files from Python environment and associates them with classification nodes
/// Uses: PythonRedirectionFileParser to parse _redirection.ini
/// Uses: ClassificationService to associate thumbnails with nodes
/// Uses: FileService for file operations
/// </summary>
public class MigrationStep4MigrateClassificationThumbnails : IMigrationStep
{
    private readonly IProfilePathService _profilePaths;
    private readonly IFileHelper _fileService;
    private readonly IPythonRedirectionFileParser _redirectionParser;
    private readonly IClassificationService _classificationService;
    private readonly ILogHelper _logger;

    public int StepNumber => 4;
    public string StepName => "Migrate Classification Thumbnails";

    public MigrationStep4MigrateClassificationThumbnails(
        IProfilePathService profilePaths,
        IFileHelper fileService,
        IPythonRedirectionFileParser redirectionParser,
        IClassificationService classificationService,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _fileService = fileService;
        _redirectionParser = redirectionParser;
        _classificationService = classificationService;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.Options.MigratePreviews)
        {
            await LogAsync(context.LogPath, "Step 4: Skipping classification thumbnails (disabled)");
            return;
        }

        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.CopyingPreviews,
            CurrentTask = "Migrating classification thumbnails...",
            PercentComplete = 45
        });

        await LogAsync(context.LogPath, "Step 4: Migrating classification thumbnails").ConfigureAwait(false);

        var thumbnailsCopied = await MigrateClassificationThumbnailsAsync(context.EnvironmentPath!, context.LogPath).ConfigureAwait(false);
        await LogAsync(context.LogPath, $"Migrated {thumbnailsCopied} classification thumbnails").ConfigureAwait(false);

        _logger.Info($"Step 4 complete: {thumbnailsCopied} thumbnails", "Migration");
    }

    private async Task<int> MigrateClassificationThumbnailsAsync(string envPath, string logPath)
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
            // âœ?Use FileService to copy directory
            await _fileService.CopyDirectoryAsync(sourceThumbnailDir, destThumbnailsDir, overwrite: true).ConfigureAwait(false);

            // Delete _redirection.ini from destination (Python-specific)
            var destRedirectionFile = Path.Combine(destThumbnailsDir, "_redirection.ini");
            if (File.Exists(destRedirectionFile))
            {
                File.Delete(destRedirectionFile);
            }

            var copiedCount = Directory.GetFiles(destThumbnailsDir, "*", SearchOption.AllDirectories).Length;
            await LogAsync(logPath, $"Copied {copiedCount} thumbnail files").ConfigureAwait(false);

            // âœ?Parse _redirection.ini and associate thumbnails with classification nodes
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

                    // Associate thumbnails with classification nodes
                    foreach (var (characterName, thumbnailRelativePath) in mappings)
                    {
                        var thumbnailFullPath = Path.Combine(destThumbnailsDir, thumbnailRelativePath);

                        if (!File.Exists(thumbnailFullPath))
                            continue;

                        // Find node by character name and set thumbnail
                        var node = await _classificationService.GetNodeByNameAsync(characterName).ConfigureAwait(false);
                        if (node != null)
                        {
                            var updated = await _classificationService.SetNodeThumbnailAsync(node.Id, thumbnailFullPath).ConfigureAwait(false);
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
