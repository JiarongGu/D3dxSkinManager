using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Migration.Steps;

/// <summary>
/// Step 6: Migrate mod preview images
/// Copies mod preview images from Python environment to profile
/// Uses: FileService/ImageService for file operations
/// </summary>
public class MigrationStep6MigrateModPreviews : IMigrationStep
{
    private readonly IProfilePathService _profilePaths;
    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly ILogHelper _logger;

    public int StepNumber => 6;
    public string StepName => "Migrate Mod Previews";

    public MigrationStep6MigrateModPreviews(
        IProfilePathService profilePaths,
        IFileService fileService,
        IImageService imageService,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _fileService = fileService;
        _imageService = imageService;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.Options.MigratePreviews)
        {
            await LogAsync(context.LogPath, "Step 6: Skipping mod previews (disabled)");
            return;
        }

        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.CopyingPreviews,
            CurrentTask = "Copying mod preview images...",
            PercentComplete = 70
        });

        await LogAsync(context.LogPath, "Step 6: Migrating mod preview images");

        // Migrate preview images
        var copied = await MigrateModPreviewsAsync(context.Options.SourcePath, context.LogPath, progress);
        context.Result.PreviewsCopied = copied;
        await LogAsync(context.LogPath, $"Copied {copied} mod preview images");

        _logger.Info($"Step 6 complete: {copied} previews", "Migration");
    }

    private async Task<int> MigrateModPreviewsAsync(string sourcePath, string logPath, IProgress<MigrationProgress>? progress = null)
    {
        var sourceDir = Path.Combine(sourcePath, "resources", "preview");
        var destDir = _profilePaths.PreviewsDirectory;

        if (!Directory.Exists(sourceDir))
        {
            await LogAsync(logPath, "WARNING: Source preview directory not found");
            return 0;
        }

        Directory.CreateDirectory(destDir);

        // ✅ Use ImageService to get supported extensions
        var imageExtensions = _imageService.GetSupportedImageExtensions();
        var allFiles = new List<string>();
        foreach (var ext in imageExtensions)
        {
            allFiles.AddRange(Directory.GetFiles(sourceDir, $"*{ext}", SearchOption.AllDirectories));
        }

        var directFiles = allFiles.Where(f => Path.GetDirectoryName(f) == sourceDir).ToList();
        var subfolderFiles = allFiles.Except(directFiles).ToList();
        var totalFiles = allFiles.Count;
        var copied = 0;

        // Process direct files: preview/ABC123.png -> previews/ABC123/preview1.png
        foreach (var sourceFile in directFiles)
        {
            var sha = Path.GetFileNameWithoutExtension(sourceFile);
            var ext = Path.GetExtension(sourceFile);
            var destFile = Path.Combine(destDir, sha, $"preview1{ext}");

            if (await CopyPreviewFileAsync(sourceFile, destFile, sha, logPath))
            {
                copied++;
                ReportPreviewProgress(progress, sha, copied, totalFiles);
            }
        }

        // Process subfolder files: preview/ABC123/preview1.png -> previews/ABC123/preview1.png
        foreach (var sourceFile in subfolderFiles)
        {
            var sha = Path.GetFileName(Path.GetDirectoryName(sourceFile)) ?? string.Empty;
            var fileName = Path.GetFileName(sourceFile);
            var destFile = Path.Combine(destDir, sha, fileName);

            if (await CopyPreviewFileAsync(sourceFile, destFile, sha, logPath))
            {
                copied++;
                ReportPreviewProgress(progress, sha, copied, totalFiles);
            }
        }

        return copied;
    }

    private async Task<bool> CopyPreviewFileAsync(string sourceFile, string destFile, string sha, string logPath)
    {
        try
        {
            if (!File.Exists(destFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                // ✅ Use FileService instead of File.Copy!
                await _fileService.CopyFileAsync(sourceFile, destFile, overwrite: false);
                return true;
            }
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR copying preview for {sha}: {ex.Message}");
        }
        return false;
    }

    private void ReportPreviewProgress(IProgress<MigrationProgress>? progress, string sha, int copied, int total)
    {
        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.CopyingPreviews,
            CurrentTask = $"Copying preview for {sha}...",
            ProcessedItems = copied,
            TotalItems = total,
            PercentComplete = 60 + (20 * copied / Math.Max(1, total))
        });
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
