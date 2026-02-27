using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.TaskQueue.Models;
using D3dxSkinManager.Modules.TaskQueue.Services;

namespace D3dxSkinManager.Modules.TaskQueue.Processors;

/// <summary>
/// Task processor for mod import operations from archive files
/// Note: Folder imports use chain-phase approach (compress_folder -> import_from_temp)
/// </summary>
public class ModImportTaskProcessor : ITaskProcessor<ModImportTaskInput, ModImportTaskOutput>
{
    private readonly IModImportService _importService;
    private readonly IModManagementService _modManagementService;
    private readonly ILogHelper _logger;

    public string TaskType => TaskTypes.MOD_IMPORT;

    public ModImportTaskProcessor(
        IModImportService importService,
        IModManagementService modManagementService,
        ILogHelper logger)
    {
        _importService = importService;
        _modManagementService = modManagementService;
        _logger = logger;
    }

    public Task<bool> ValidateInputAsync(ModImportTaskInput input)
    {
        if (string.IsNullOrEmpty(input.FilePath))
            return Task.FromResult(false);

        if (input.IsFolder)
        {
            return Task.FromResult(Directory.Exists(input.FilePath));
        }
        else
        {
            return Task.FromResult(File.Exists(input.FilePath));
        }
    }

    public async Task<ModImportTaskOutput> ProcessAsync(
        ModImportTaskInput input,
        IProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        try
        {
            ModInfo? mod = null;

            // Folder import should use chain-phase approach (compress_folder -> import_from_temp)
            // This direct import is only for archive files
            if (input.IsFolder)
            {
                throw new InvalidOperationException("Folder import should use chain-phase approach (compress_folder task)");
            }

            // Step 1: Validate archive (0-20%)
            await progressReporter.ReportProgressAsync(10, "Validating archive...").ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await progressReporter.ReportProgressAsync(20, "Archive validated").ConfigureAwait(false);

            // Step 2: Import mod (20-70%)
            await progressReporter.ReportProgressAsync(30, "Importing mod...").ConfigureAwait(false);

            mod = await _importService.ImportAsync(input.FilePath).ConfigureAwait(false);

            if (mod == null)
            {
                throw new InvalidOperationException("Import returned null - mod may already exist or import failed");
            }

            await progressReporter.ReportProgressAsync(70, "Mod imported successfully").ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Step 3: Update metadata if provided (70-90%)
            if (HasMetadataOverrides(input))
            {
                await progressReporter.ReportProgressAsync(75, "Updating metadata...").ConfigureAwait(false);

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

            // Step 4: Complete (100%)
            await progressReporter.ReportProgressAsync(100, "Import completed").ConfigureAwait(false);

            _logger.Info($"Mod import completed: {mod?.Name} ({mod?.SHA})", "ModImportTaskProcessor");

            return new ModImportTaskOutput
            {
                Sha = mod?.SHA ?? string.Empty,
                Name = mod?.Name ?? "Unknown",
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            _logger.Warn("Mod import cancelled", "ModImportTaskProcessor");
            await progressReporter.ReportCancellationAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Mod import failed: {ex.Message}", "ModImportTaskProcessor", ex);
            await progressReporter.ReportFailureAsync(ex.Message).ConfigureAwait(false);

            return new ModImportTaskOutput
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private bool HasMetadataOverrides(ModImportTaskInput input)
    {
        return !string.IsNullOrEmpty(input.Name) ||
               !string.IsNullOrEmpty(input.Author) ||
               !string.IsNullOrEmpty(input.Description) ||
               !string.IsNullOrEmpty(input.Grading) ||
               !string.IsNullOrEmpty(input.Category) ||
               (input.Tags != null && input.Tags.Count > 0);
    }
}
