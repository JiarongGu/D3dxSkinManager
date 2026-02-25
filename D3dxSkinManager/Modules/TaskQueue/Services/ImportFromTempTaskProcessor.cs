using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Services;

/// <summary>
/// Task processor for importing mods from temp archive with metadata
/// This is phase 2 of folder import chain - imports and applies user metadata
/// </summary>
public class ImportFromTempTaskProcessor : ITaskProcessor<ImportFromTempTaskInput, ModImportTaskOutput>
{
    private readonly IModImportService _importService;
    private readonly IModManagementService _modManagementService;
    private readonly ILogHelper _logger;

    public string TaskType => "import_from_temp";

    public ImportFromTempTaskProcessor(
        IModImportService importService,
        IModManagementService modManagementService,
        ILogHelper logger)
    {
        _importService = importService;
        _modManagementService = modManagementService;
        _logger = logger;
    }

    public Task<bool> ValidateInputAsync(ImportFromTempTaskInput input)
    {
        if (string.IsNullOrEmpty(input.TempArchivePath))
            return Task.FromResult(false);

        return Task.FromResult(File.Exists(input.TempArchivePath));
    }

    public async Task<ModImportTaskOutput> ProcessAsync(
        ImportFromTempTaskInput input,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        string? tempArchivePath = input.TempArchivePath;

        try
        {
            // Step 1: Import mod from temp archive (0-60%)
            await progressReporter.ReportProgressAsync(10, "Importing mod from archive...").ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var mod = await _importService.ImportAsync(input.TempArchivePath).ConfigureAwait(false);

            if (mod == null)
            {
                throw new InvalidOperationException("Import returned null - mod may already exist or import failed");
            }

            await progressReporter.ReportProgressAsync(60, "Mod imported successfully").ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // Step 2: Update metadata if provided (60-90%)
            if (HasMetadataOverrides(input))
            {
                await progressReporter.ReportProgressAsync(70, "Updating metadata...").ConfigureAwait(false);

                var updateRequest = new UpdateModRequest
                {
                    Name = input.Name,
                    Author = input.Author,
                    Description = input.Description,
                    Grading = input.Grading,
                    Tags = input.Tags,
                    Category = input.Category
                };

                mod = await _modManagementService.UpdateModAsync(mod.SHA, updateRequest).ConfigureAwait(false);

                await progressReporter.ReportProgressAsync(90, "Metadata updated").ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Step 3: Complete (100%)
            await progressReporter.ReportProgressAsync(100, "Import completed").ConfigureAwait(false);

            _logger.Info($"Mod import from temp completed: {mod?.Name} ({mod?.SHA})", "ImportFromTempTaskProcessor");

            return new ModImportTaskOutput
            {
                Sha = mod?.SHA ?? string.Empty,
                Name = mod?.Name ?? "Unknown",
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            _logger.Warn("Import from temp cancelled", "ImportFromTempTaskProcessor");
            await progressReporter.ReportCancellationAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Import from temp failed: {ex.Message}", "ImportFromTempTaskProcessor", ex);
            await progressReporter.ReportFailureAsync(ex.Message).ConfigureAwait(false);

            return new ModImportTaskOutput
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            // Cleanup temporary archive
            if (tempArchivePath != null && File.Exists(tempArchivePath))
            {
                try
                {
                    File.Delete(tempArchivePath);
                    _logger.Debug($"Cleaned up temp archive: {Path.GetFileName(tempArchivePath)}", "ImportFromTempTaskProcessor");
                }
                catch (Exception cleanupEx)
                {
                    _logger.Warn($"Failed to cleanup temp file: {cleanupEx.Message}", "ImportFromTempTaskProcessor");
                }
            }
        }
    }

    private bool HasMetadataOverrides(ImportFromTempTaskInput input)
    {
        return !string.IsNullOrEmpty(input.Name) ||
               !string.IsNullOrEmpty(input.Author) ||
               !string.IsNullOrEmpty(input.Description) ||
               !string.IsNullOrEmpty(input.Grading) ||
               !string.IsNullOrEmpty(input.Category) ||
               (input.Tags != null && input.Tags.Count > 0);
    }
}
