using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Result of archive extraction operation
/// </summary>
public class ArchiveExtractionResult
{
    public bool Success { get; set; }
    public string? DetectedType { get; set; }
    public int FileCount { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Service for mod archive file operations
/// Responsibility: Pure archive file operations (no business logic, no events)
/// </summary>
public interface IModArchiveService
{
    Task<ArchiveExtractionResult> ExtractAsync(string id, string targetDirectory);
    Task<bool> DeleteArchiveAsync(string id);
    Task<string> CopyArchiveAsync(string sourcePath, string id);
    Task<bool> CompressCacheToArchiveAsync(string id, string cacheDirectory);

    /// <summary>
    /// Fast path: replace a single file inside the mod's archive (no full recompress).
    /// <paramref name="entryPath"/> is the path inside the archive (relative, e.g. "sub/mod.ini").
    /// </summary>
    Task<bool> UpdateFileInArchiveAsync(string id, string sourceFilePath, string entryPath);

    bool ArchiveExists(string id);
    string GetArchivePath(string id);
}

/// <summary>
/// Service for mod archive file operations
/// Handles archive extraction, copying, deletion, and path management
/// Uses atomic file operation planner for all operations
/// </summary>
public class ModArchiveService : IModArchiveService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IFileOperationPlanner _operationPlanner;
    private readonly ILogHelper _logger;
    private readonly IProcessRegistry _processRegistry;

    public ModArchiveService(
        IProfilePathService profilePaths,
        IFileOperationPlanner operationPlanner,
        ILogHelper logger,
        IProcessRegistry processRegistry)
    {
        _profilePaths = profilePaths;
        _operationPlanner = operationPlanner;
        _logger = logger;
        _processRegistry = processRegistry;
    }

    /// <summary>
    /// Extract archive to target directory
    /// Returns extraction result with detected type and file count
    /// Uses atomic file operation planner
    /// </summary>
    public async Task<ArchiveExtractionResult> ExtractAsync(string id, string targetDirectory)
    {
        var result = new ArchiveExtractionResult { Success = false };

        try
        {
            var archivePath = GetArchivePath(id);
            if (!File.Exists(archivePath))
            {
                result.ErrorMessage = $"Archive not found: {archivePath}";
                _logger.Warn(result.ErrorMessage, "ModArchiveService");
                return result;
            }

            // Extract archive using file operation planner
            var extractOp = new FileSystemOperation
            {
                OperationType = FileSystemOperationType.ExtractArchive,
                SourcePath = archivePath,
                TargetPath = targetDirectory,
                Overwrite = true
            };

            var extractResult = await _operationPlanner.SubmitOperationAsync(extractOp).ConfigureAwait(false);

            if (!extractResult.Success)
            {
                result.ErrorMessage = extractResult.ErrorMessage ?? "Failed to extract mod archive";
                result.Exception = extractResult.Exception;
                _logger.Error($"Extraction failed for {id}: {result.ErrorMessage}", "ModArchiveService", result.Exception);
                return result;
            }

            // Extract detected type from result data
            if (extractResult.Data.TryGetValue("detectedType", out var detectedTypeObj))
            {
                if (detectedTypeObj is string detectedType && !string.IsNullOrEmpty(detectedType))
                {
                    result.DetectedType = detectedType;
                }
            }

            // Extract file count from result data
            if (extractResult.Data.TryGetValue("fileCount", out var fileCountObj) && fileCountObj is int fileCount)
            {
                result.FileCount = fileCount;
            }

            result.Success = true;
            _logger.Info($"Extracted archive: {id} ({result.FileCount} files)", "ModArchiveService");

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Error extracting archive: {ex.Message}";
            result.Exception = ex;
            _logger.Error($"Error extracting archive {id}: {ex.Message}", "ModArchiveService", ex);
            return result;
        }
    }

    /// <summary>
    /// Delete archive file permanently
    /// Uses atomic file operation planner
    /// </summary>
    public async Task<bool> DeleteArchiveAsync(string id)
    {
        try
        {
            var archivePath = GetArchivePath(id);
            if (!File.Exists(archivePath))
            {
                _logger.Warn($"Archive not found for deletion: {archivePath}", "ModArchiveService");
                return false;
            }

            var deleteFileOp = new FileSystemOperation
            {
                OperationType = FileSystemOperationType.DeleteFile,
                SourcePath = archivePath
            };

            var result = await _operationPlanner.SubmitOperationAsync(deleteFileOp).ConfigureAwait(false);

            if (result.Success)
            {
                _logger.Info($"Deleted archive: {archivePath}", "ModArchiveService");
                return true;
            }
            else
            {
                _logger.Error($"Failed to delete archive: {result.ErrorMessage}", "ModArchiveService", result.Exception);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error deleting archive {id}: {ex.Message}", "ModArchiveService", ex);
            return false;
        }
    }

    /// <summary>
    /// Copy archive file to mods directory
    /// Stores without extension (like Python version) - SharpCompress auto-detects format
    /// Uses atomic file operation planner
    /// </summary>
    public async Task<string> CopyArchiveAsync(string sourcePath, string id)
    {
        var targetPath = _profilePaths.GetModArchivePath(id, "");

        var copyOp = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.CopyFile,
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Overwrite = true
        };

        var result = await _operationPlanner.SubmitOperationAsync(copyOp).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to copy archive: {result.ErrorMessage}", result.Exception);
        }

        _logger.Info($"Copied archive to: {targetPath}", "ModArchiveService");
        return targetPath;
    }

    /// <summary>
    /// Compress cache directory back into the mod archive, replacing the existing archive.
    /// Uses atomic file operation planner with temp file for safe replacement.
    /// </summary>
    public async Task<bool> CompressCacheToArchiveAsync(string id, string cacheDirectory)
    {
        // Compression can take a while (large mod) — show it in the status bar / Activity panel.
        var procId = _processRegistry.Start(ProcessType.ArchiveUpdate, $"Updating archive: {id}",
            titleKey: "process.archiveUpdate", titleArg: id);
        try
        {
            if (!Directory.Exists(cacheDirectory))
            {
                _logger.Warn($"Cache directory not found for archive update: {cacheDirectory}", "ModArchiveService");
                _processRegistry.Fail(procId, "Cache directory not found");
                return false;
            }

            var archivePath = GetArchivePath(id);
            // Temp file in profile temp directory (same drive as archive, consistent with .mic pattern)
            var tempPath = Path.Combine(_profilePaths.TempDirectory, TempFileConstants.GetArchiveUpdateTempName(id));

            var compressOp = new FileSystemOperation
            {
                OperationType = FileSystemOperationType.CompressArchive,
                SourcePath = cacheDirectory,
                TargetPath = archivePath,
                TempPath = tempPath,
                Overwrite = true
            };

            var result = await _operationPlanner.SubmitOperationAsync(compressOp).ConfigureAwait(false);

            if (result.Success)
            {
                _logger.Info($"Updated archive from cache: {id}", "ModArchiveService");
                _processRegistry.Complete(procId);
                return true;
            }
            else
            {
                _logger.Error($"Failed to update archive from cache: {result.ErrorMessage}", "ModArchiveService", result.Exception);
                _processRegistry.Fail(procId, result.ErrorMessage ?? "Archive update failed");
                return false;
            }
        }
        catch (Exception ex)
        {
            _processRegistry.Fail(procId, ex.Message);
            _logger.Error($"Error updating archive from cache {id}: {ex.Message}", "ModArchiveService", ex);
            return false;
        }
    }

    /// <summary>
    /// Fast single-file archive update (append mode) — used for small edits like a keybinding change
    /// instead of recompressing the whole cache. Planner-serialized like all archive mutations.
    /// </summary>
    public async Task<bool> UpdateFileInArchiveAsync(string id, string sourceFilePath, string entryPath)
    {
        var archivePath = GetArchivePath(id);
        if (!File.Exists(archivePath))
        {
            _logger.Warn($"Archive not found for single-file update: {id}", "ModArchiveService");
            return false;
        }

        var op = new FileSystemOperation
        {
            OperationType = FileSystemOperationType.UpdateFileInArchive,
            SourcePath = sourceFilePath,
            TargetPath = archivePath,
            ArchiveEntryPath = entryPath,
            Overwrite = true,
        };

        var result = await _operationPlanner.SubmitOperationAsync(op).ConfigureAwait(false);
        if (result.Success)
        {
            _logger.Info($"Updated archive entry '{entryPath}' for mod {id}", "ModArchiveService");
            return true;
        }
        _logger.Error($"Failed to update archive entry for {id}: {result.ErrorMessage}", "ModArchiveService", result.Exception);
        return false;
    }

    /// <summary>
    /// Check if mod archive exists
    /// </summary>
    public bool ArchiveExists(string id)
    {
        return File.Exists(GetArchivePath(id));
    }

    /// <summary>
    /// Get the path to a mod's archive file
    /// Archives are stored without extensions (like Python version)
    /// </summary>
    public string GetArchivePath(string id)
    {
        return _profilePaths.GetModArchivePath(id, "");
    }
}
