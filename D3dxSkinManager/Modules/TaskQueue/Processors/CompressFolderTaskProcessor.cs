using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Processors;

/// <summary>
/// Task processor for compressing folders to temp directory
/// This is phase 1 of folder import chain - pauses for user metadata input
/// </summary>
public class CompressFolderTaskProcessor : ITaskProcessor<CompressFolderTaskInput, CompressFolderTaskOutput>
{
    private readonly IArchiveHelper _archiveHelper;
    private readonly IProfilePathService _profilePathService;
    private readonly ILogHelper _logger;

    public string TaskType => TaskTypes.COMPRESS_FOLDER;

    public CompressFolderTaskProcessor(
        IArchiveHelper archiveHelper,
        IProfilePathService profilePathService,
        ILogHelper logger)
    {
        _archiveHelper = archiveHelper;
        _profilePathService = profilePathService;
        _logger = logger;
    }

    public Task<bool> ValidateInputAsync(CompressFolderTaskInput input)
    {
        if (string.IsNullOrEmpty(input.FolderPath))
            return Task.FromResult(false);

        return Task.FromResult(Directory.Exists(input.FolderPath));
    }

    public async Task<CompressFolderTaskOutput> ProcessAsync(
        CompressFolderTaskInput input,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        try
        {
            await progressReporter.ReportProgressAsync(5, "Preparing folder compression...").ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var folderName = Path.GetFileName(input.FolderPath.TrimEnd(Path.DirectorySeparatorChar));
            var tempArchivePath = Path.Combine(
                _profilePathService.TempDirectory,
                $"mod_import_{Guid.NewGuid():N}_{folderName}.zip"
            );

            _logger.Info($"Compressing folder to temp: {tempArchivePath}", "CompressFolderTaskProcessor");
            await progressReporter.ReportProgressAsync(10, "Compressing folder...").ConfigureAwait(false);

            var archivePath = await _archiveHelper.CompressFolderAsync(
                input.FolderPath,
                tempArchivePath,
                ArchiveFormat.Zip
            ).ConfigureAwait(false);

            await progressReporter.ReportProgressAsync(90, "Folder compressed").ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            await progressReporter.ReportProgressAsync(100, "Ready for metadata input").ConfigureAwait(false);

            _logger.Info($"Folder compression completed: {folderName}", "CompressFolderTaskProcessor");

            return new CompressFolderTaskOutput
            {
                TempArchivePath = archivePath,
                OriginalFolderPath = input.FolderPath,
                FolderName = folderName,
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            _logger.Warn("Folder compression cancelled", "CompressFolderTaskProcessor");
            await progressReporter.ReportCancellationAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Folder compression failed: {ex.Message}", "CompressFolderTaskProcessor", ex);
            await progressReporter.ReportFailureAsync(ex.Message).ConfigureAwait(false);

            return new CompressFolderTaskOutput
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
