using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

using System.Text.Json;

namespace D3dxSkinManager.Plugins.PreviewClearup;

/// <summary>
/// Plugin to clean up unused preview images.
/// Port of preview_clearup Python plugin.
///
/// Features:
/// - Categorizes previews as invalid, rarely used, or frequently used
/// - Invalid: No corresponding mod exists in any user's index
/// - Rarely used: Mod exists in other users' indexes but not current user
/// - Frequently used: Mod exists in current user's local index
/// - Separate cleanup operations for each category
/// - File size calculations
/// </summary>
public class PreviewClearupPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.previewclearup";
    public string Name => "Preview Clearup";
    public string Version => "1.0.0";
    public string Description => "Cleans up unused preview images (invalid, rarely used, frequently used)";
    public string Author => "D3dxSkinManager";

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _context.Log(LogLevel.Info, $"[{Name}] Initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "SCAN_PREVIEW", "CLEAN_INVALID_PREVIEW", "CLEAN_RARELY_PREVIEW", "CLEAN_ALL_PREVIEW" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "SCAN_PREVIEW" => await ScanPreviewAsync(request),
                "CLEAN_INVALID_PREVIEW" => await CleanPreviewByCategoryAsync(request, "invalid"),
                "CLEAN_RARELY_PREVIEW" => await CleanPreviewByCategoryAsync(request, "rarely"),
                "CLEAN_ALL_PREVIEW" => await CleanPreviewByCategoryAsync(request, "all"),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            _context?.Log(LogLevel.Error, $"[{Name}] Error handling message", ex);
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> ScanPreviewAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var homePath = Path.Combine(dataPath, "home");
            var previewPath = Path.Combine(dataPath, "resources", "preview");
            var previewScreenPath = Path.Combine(dataPath, "resources", "preview_screen");

            // Get current user (placeholder - would need to come from context or request)
            var currentUser = request.Payload?.ToString() ?? "";

            // Collect all SHAs from all users
            var allShas = new HashSet<string>();
            var currentUserShas = new HashSet<string>();

            if (Directory.Exists(homePath))
            {
                foreach (var userDir in Directory.GetDirectories(homePath))
                {
                    var userName = Path.GetFileName(userDir);
                    var modsIndexPath = Path.Combine(userDir, "modsIndex");

                    if (!Directory.Exists(modsIndexPath))
                        continue;

                    var jsonFiles = Directory.GetFiles(modsIndexPath, "*.json", SearchOption.AllDirectories);

                    foreach (var jsonFile in jsonFiles)
                    {
                        try
                        {
                            var jsonContent = await File.ReadAllTextAsync(jsonFile);
                            var indexData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);

                            if (indexData != null && indexData.ContainsKey("mods"))
                            {
                                var modsElement = indexData["mods"];
                                if (modsElement is JsonElement element && element.ValueKind == JsonValueKind.Object)
                                {
                                    foreach (var property in element.EnumerateObject())
                                    {
                                        var sha = property.Name;
                                        allShas.Add(sha);

                                        if (userName == currentUser)
                                            currentUserShas.Add(sha);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _context.Log(LogLevel.Warning, $"[{Name}] Error reading index file {jsonFile}: {ex.Message}");
                        }
                    }
                }
            }

            // Get all local mod SHAs
            var localMods = await _context.ModRepository.GetAllAsync();
            var localShas = new HashSet<string>(localMods.Select(m => m.SHA));

            // Categorize preview files
            var invalidPreviews = new List<string>();
            var rarelyPreviews = new List<string>();
            var frequentlyPreviews = new List<string>();
            long invalidSize = 0, rarelySize = 0, frequentlySize = 0;

            // Scan preview directories
            var previewDirs = new[] { previewPath, previewScreenPath };

            foreach (var dir in previewDirs)
            {
                if (!Directory.Exists(dir))
                    continue;

                foreach (var entry in Directory.GetFileSystemEntries(dir))
                {
                    var name = Path.GetFileName(entry);
                    var sha = Path.GetFileNameWithoutExtension(name);

                    var size = GetEntrySize(entry);

                    // Categorize based on SHA presence
                    if (localShas.Contains(sha))
                    {
                        frequentlyPreviews.Add(entry);
                        frequentlySize += size;
                    }
                    else if (currentUserShas.Contains(sha))
                    {
                        rarelyPreviews.Add(entry);
                        rarelySize += size;
                    }
                    else if (!allShas.Contains(sha))
                    {
                        invalidPreviews.Add(entry);
                        invalidSize += size;
                    }
                }
            }

            _context.Log(LogLevel.Info, $"[{Name}] Scanned previews: {invalidPreviews.Count} invalid, {rarelyPreviews.Count} rarely used, {frequentlyPreviews.Count} frequently used");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                invalidPreviews = invalidPreviews.ToArray(),
                invalidCount = invalidPreviews.Count,
                invalidSize,
                invalidSizeMB = invalidSize / (1024.0 * 1024.0),
                rarelyPreviews = rarelyPreviews.ToArray(),
                rarelyCount = rarelyPreviews.Count,
                rarelySize,
                rarelySizeMB = rarelySize / (1024.0 * 1024.0),
                frequentlyPreviews = frequentlyPreviews.ToArray(),
                frequentlyCount = frequentlyPreviews.Count,
                frequentlySize,
                frequentlySizeMB = frequentlySize / (1024.0 * 1024.0)
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error scanning previews", ex);
            return MessageResponse.CreateError(request.Id, $"Scan failed: {ex.Message}");
        }
    }

    private async Task<MessageResponse> CleanPreviewByCategoryAsync(MessageRequest request, string category)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // First scan to get categorized files
            var scanResponse = await ScanPreviewAsync(request);
            if (!scanResponse.Success)
                return scanResponse;

            var scanData = scanResponse.Data as dynamic;
            List<string> filesToDelete = new();

            if (category == "invalid")
                filesToDelete = (scanData?.invalidPreviews as IEnumerable<object>)?.Select(f => f.ToString() ?? "").ToList() ?? new List<string>();
            else if (category == "rarely")
                filesToDelete = (scanData?.rarelyPreviews as IEnumerable<object>)?.Select(f => f.ToString() ?? "").ToList() ?? new List<string>();
            else if (category == "all")
            {
                filesToDelete = new List<string>();
                filesToDelete.AddRange((scanData?.invalidPreviews as IEnumerable<object>)?.Select(f => f.ToString() ?? "") ?? Array.Empty<string>());
                filesToDelete.AddRange((scanData?.rarelyPreviews as IEnumerable<object>)?.Select(f => f.ToString() ?? "") ?? Array.Empty<string>());
                filesToDelete.AddRange((scanData?.frequentlyPreviews as IEnumerable<object>)?.Select(f => f.ToString() ?? "") ?? Array.Empty<string>());
            }

            int deletedCount = 0;
            long freedSpace = 0;
            var errors = new List<string>();

            foreach (var path in filesToDelete)
            {
                try
                {
                    var size = GetEntrySize(path);

                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                    else if (File.Exists(path))
                        File.Delete(path);

                    deletedCount++;
                    freedSpace += size;
                }
                catch (UnauthorizedAccessException)
                {
                    errors.Add($"Permission denied: {path}");
                }
                catch (IOException ex)
                {
                    errors.Add($"IO error on {path}: {ex.Message}");
                }
            }

            _context.Log(LogLevel.Info, $"[{Name}] Cleaned {deletedCount} {category} previews, freed {freedSpace / (1024 * 1024)}MB");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                category,
                deletedCount,
                freedSpace,
                freedSpaceMB = freedSpace / (1024.0 * 1024.0),
                errors = errors.ToArray()
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error cleaning previews", ex);
            return MessageResponse.CreateError(request.Id, $"Clean failed: {ex.Message}");
        }
    }

    private long GetEntrySize(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return new DirectoryInfo(path)
                    .GetFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            else if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }
        }
        catch
        {
            // Ignore errors
        }
        return 0;
    }
}
