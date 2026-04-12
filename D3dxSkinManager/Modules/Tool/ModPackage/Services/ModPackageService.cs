using System.Text.Json;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Tool.ModPackage.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Mod.Mappers;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Tool.ModPackage.Services;

/// <summary>
/// Interface for mod package export/import operations
/// </summary>
public interface IModPackageService
{
    Task<ExportResult> ExportAsync(ExportConfig config);
    Task<PackageAnalysis> AnalyzePackageAsync(string packagePath);
    Task<ImportResult> ImportAsync(ImportConfig config);
}

/// <summary>
/// Service for mod package export/import operations.
/// Layer 2: Business logic + event emission.
/// Handles creating export folders with human-readable names and importing from them.
/// </summary>
public class ModPackageService : IModPackageService
{
    private readonly IModRepository _modRepository;
    private readonly IModArchiveService _archiveService;
    private readonly IModMetadataService _metadataService;
    private readonly IModImportService _modImportService;
    private readonly ICategoryService _categoryService;
    private readonly IProfilePathService _profilePaths;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // Characters illegal in Windows file names
    private static readonly Regex InvalidFileCharsRegex = new(
        $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]",
        RegexOptions.Compiled);

    public ModPackageService(
        IModRepository modRepository,
        IModArchiveService archiveService,
        IModMetadataService metadataService,
        IModImportService modImportService,
        ICategoryService categoryService,
        IProfilePathService profilePaths,
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _modRepository = modRepository;
        _archiveService = archiveService;
        _metadataService = metadataService;
        _modImportService = modImportService;
        _categoryService = categoryService;
        _profilePaths = profilePaths;
        _eventBus = eventBus;
        _logger = logger;
    }

    // ===== Export =====

    public async Task<ExportResult> ExportAsync(ExportConfig config)
    {
        var result = new ExportResult { OutputPath = config.OutputPath };

        try
        {
            // Check if target directory already exists and is not empty
            var packageDir = Path.Combine(config.OutputPath, SanitizeFileName(config.PackageName));
            if (Directory.Exists(packageDir) && Directory.EnumerateFileSystemEntries(packageDir).Any())
            {
                throw new OperationException(
                    "EXPORT_FOLDER_NOT_EMPTY",
                    new Dictionary<string, string> { { "path", packageDir } });
            }

            // Create the output directory
            Directory.CreateDirectory(packageDir);

            var modsDir = Path.Combine(packageDir, "mods");
            Directory.CreateDirectory(modsDir);

            // Get all mod entities
            var allEntities = await _modRepository.GetAllAsync().ConfigureAwait(false);
            var entityMap = allEntities.ToDictionary(e => e.Id);

            // Get category tree for path building
            var categoryTree = await _categoryService.GetCategoryTreeAsync().ConfigureAwait(false);
            var categoryPathMap = BuildCategoryPathMap(categoryTree);
            var categoryMap = BuildCategoryFlatMap(categoryTree);

            // Track used file names for deduplication
            var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedPreviewFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var manifestMods = new List<PackageModEntry>();
            var referencedCategoryIds = new HashSet<string>();
            var total = config.ModIds.Count;

            for (int i = 0; i < total; i++)
            {
                var modId = config.ModIds[i];
                if (!entityMap.TryGetValue(modId, out var entity))
                {
                    result.Errors.Add($"Mod not found: {modId}");
                    continue;
                }

                var mod = ModMapper.ToDomain(entity);

                // Emit progress
                await EmitProgress("export", i + 1, total, mod.Name, "copying").ConfigureAwait(false);

                // Build file-safe name
                var baseName = SanitizeFileName(mod.Name);
                if (string.IsNullOrWhiteSpace(baseName)) baseName = mod.Id;

                var extension = string.IsNullOrEmpty(mod.Type) ? "" : $".{mod.Type}";
                var fileName = GetUniqueFileName(baseName + extension, usedFileNames);
                usedFileNames.Add(fileName);

                // Copy archive if requested and exists
                var hasArchive = false;
                if (config.IncludeArchives)
                {
                    var archivePath = _archiveService.GetArchivePath(mod.Id);
                    if (File.Exists(archivePath))
                    {
                        var targetPath = Path.Combine(modsDir, fileName);
                        File.Copy(archivePath, targetPath, overwrite: true);
                        hasArchive = true;
                    }
                }

                // Copy previews if requested
                var hasPreviews = false;
                string? previewFolderName = null;
                if (config.IncludePreviews)
                {
                    var previewDir = _profilePaths.GetPreviewDirectoryPath(mod.Id);
                    if (Directory.Exists(previewDir))
                    {
                        var previewFiles = Directory.GetFiles(previewDir);
                        if (previewFiles.Length > 0)
                        {
                            var previewBase = SanitizeFileName(mod.Name);
                            if (string.IsNullOrWhiteSpace(previewBase)) previewBase = mod.Id;
                            previewFolderName = GetUniqueFolderName(previewBase, usedPreviewFolders);
                            usedPreviewFolders.Add(previewFolderName);

                            var previewsDir = Path.Combine(packageDir, "previews", previewFolderName);
                            Directory.CreateDirectory(previewsDir);

                            foreach (var file in previewFiles)
                            {
                                var destFile = Path.Combine(previewsDir, Path.GetFileName(file));
                                File.Copy(file, destFile, overwrite: true);
                            }
                            hasPreviews = true;
                        }
                    }
                }

                // Track category
                if (!string.IsNullOrEmpty(mod.Category))
                    referencedCategoryIds.Add(mod.Category);

                // Build category path
                var categoryPath = categoryPathMap.TryGetValue(mod.Category, out var path) ? path : "";

                manifestMods.Add(new PackageModEntry
                {
                    Id = mod.Id,
                    FileName = fileName,
                    PreviewFolder = previewFolderName,
                    Name = mod.Name,
                    Author = mod.Author,
                    Description = mod.Description,
                    CategoryId = mod.Category,
                    CategoryPath = categoryPath,
                    Tags = mod.Tags,
                    Grading = mod.Grading,
                    Type = mod.Type,
                    HasArchive = hasArchive,
                    HasPreviews = hasPreviews
                });

                result.ExportedCount++;
            }

            // Build category list for manifest (only referenced categories + their ancestors)
            var packageCategories = BuildPackageCategories(referencedCategoryIds, categoryMap);

            // Write manifest
            var manifest = new PackageManifest
            {
                Name = config.PackageName,
                Description = config.PackageDescription,
                ExportDate = DateTime.UtcNow,
                ModCount = result.ExportedCount,
                CategoryCount = packageCategories.Count,
                IncludesArchives = config.IncludeArchives,
                IncludesPreviews = config.IncludePreviews,
                Categories = packageCategories,
                Mods = manifestMods
            };

            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(packageDir, "manifest.json"), manifestJson).ConfigureAwait(false);

            // Calculate total size
            result.TotalSizeBytes = CalculateDirectorySize(packageDir);
            result.OutputPath = packageDir;
            result.Success = true;

            _logger.Info($"Export complete: {result.ExportedCount} mods to {packageDir}", "ModPackageService");
        }
        catch (OperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Export failed: {ex.Message}");
            _logger.Error($"Export failed: {ex.Message}", "ModPackageService", ex);
        }

        return result;
    }

    // ===== Analyze =====

    public async Task<PackageAnalysis> AnalyzePackageAsync(string packagePath)
    {
        var analysis = new PackageAnalysis();

        try
        {
            var manifestPath = Path.Combine(packagePath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                analysis.ErrorMessage = "No manifest.json found in the selected folder.";
                return analysis;
            }

            var manifestJson = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<PackageManifest>(manifestJson, JsonOptions);

            if (manifest == null)
            {
                analysis.ErrorMessage = "Failed to parse manifest.json.";
                return analysis;
            }

            analysis.PackageName = manifest.Name;
            analysis.PackageDescription = manifest.Description;
            analysis.ExportDate = manifest.ExportDate;
            analysis.TotalModCount = manifest.ModCount;
            analysis.HasArchives = manifest.IncludesArchives;
            analysis.HasPreviews = manifest.IncludesPreviews;
            analysis.Categories = manifest.Categories;

            // Check each mod against local database
            var allEntities = await _modRepository.GetAllAsync().ConfigureAwait(false);
            var localModMap = allEntities.ToDictionary(e => e.Id);

            foreach (var modEntry in manifest.Mods)
            {
                var analyzed = new AnalyzedModEntry
                {
                    Id = modEntry.Id,
                    Name = modEntry.Name,
                    Author = modEntry.Author,
                    Description = modEntry.Description,
                    CategoryPath = modEntry.CategoryPath,
                    Tags = modEntry.Tags,
                    Grading = modEntry.Grading,
                    HasArchive = modEntry.HasArchive && File.Exists(Path.Combine(packagePath, "mods", modEntry.FileName)),
                    HasPreviews = modEntry.HasPreviews && modEntry.PreviewFolder != null
                        && Directory.Exists(Path.Combine(packagePath, "previews", modEntry.PreviewFolder)),
                };

                // Collect preview image paths
                if (analyzed.HasPreviews && modEntry.PreviewFolder != null)
                {
                    var previewDir = Path.Combine(packagePath, "previews", modEntry.PreviewFolder);
                    if (Directory.Exists(previewDir))
                    {
                        analyzed.PreviewPaths = Directory.GetFiles(previewDir)
                            .Where(f => IsImageFile(f))
                            .OrderBy(f => f)
                            .ToList();
                    }
                }

                if (localModMap.TryGetValue(modEntry.Id, out var localEntity))
                {
                    var localMod = ModMapper.ToDomain(localEntity);
                    analyzed.LocalName = localMod.Name;
                    analyzed.LocalAuthor = localMod.Author;

                    analyzed.Status = "update";

                    // Detect what changed for display
                    var changes = new List<string>();
                    if (localMod.Name != modEntry.Name) changes.Add("name");
                    if (localMod.Author != modEntry.Author) changes.Add("author");
                    if (localMod.Description != modEntry.Description) changes.Add("description");
                    if (localMod.Grading != modEntry.Grading) changes.Add("grading");
                    if (!ListsEqual(localMod.Tags, modEntry.Tags)) changes.Add("tags");
                    if (localMod.Category != modEntry.CategoryId) changes.Add("category");

                    var localArchiveExists = _archiveService.ArchiveExists(modEntry.Id);
                    if (analyzed.HasArchive && !localArchiveExists) changes.Add("archive");

                    analyzed.ChangedFields = changes;
                }
                else
                {
                    analyzed.Status = "new";
                }

                analysis.Mods.Add(analyzed);
            }

            analysis.IsValid = true;
        }
        catch (Exception ex)
        {
            analysis.ErrorMessage = $"Failed to analyze package: {ex.Message}";
            _logger.Error($"Package analysis failed: {ex.Message}", "ModPackageService", ex);
        }

        return analysis;
    }

    // ===== Import =====

    public async Task<ImportResult> ImportAsync(ImportConfig config)
    {
        var result = new ImportResult();

        try
        {
            var manifestPath = Path.Combine(config.PackagePath, "manifest.json");
            var manifestJson = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<PackageManifest>(manifestJson, JsonOptions);

            if (manifest == null)
            {
                result.Errors.Add("Failed to parse manifest.json");
                return result;
            }

            // Build lookup for selected mods
            var selectedIds = new HashSet<string>(config.SelectedModIds);
            var modsToImport = manifest.Mods.Where(m => selectedIds.Contains(m.Id)).ToList();

            // Create missing categories if requested
            if (config.CreateMissingCategories)
            {
                await EnsureCategoriesExistAsync(manifest.Categories).ConfigureAwait(false);
            }

            var allEntities = await _modRepository.GetAllAsync().ConfigureAwait(false);
            var localModMap = allEntities.ToDictionary(e => e.Id);
            var total = modsToImport.Count;

            for (int i = 0; i < total; i++)
            {
                var modEntry = modsToImport[i];
                try
                {
                    var isUpdate = localModMap.ContainsKey(modEntry.Id);

                    await EmitProgress("import", i + 1, total, modEntry.Name,
                        isUpdate ? "updating" : "importing").ConfigureAwait(false);

                    if (isUpdate && config.UpdateExisting)
                    {
                        await UpdateExistingModAsync(config.PackagePath, modEntry, config.ImportPreviews).ConfigureAwait(false);
                        result.UpdatedCount++;
                        result.UpdatedModNames.Add(modEntry.Name);
                    }
                    else if (isUpdate)
                    {
                        result.SkippedCount++;
                    }
                    else
                    {
                        await ImportNewModAsync(config.PackagePath, modEntry, config.ImportPreviews).ConfigureAwait(false);
                        result.ImportedCount++;
                        result.ImportedModNames.Add(modEntry.Name);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"{modEntry.Name}: {ex.Message}");
                    _logger.Error($"Failed to import mod {modEntry.Name}: {ex.Message}", "ModPackageService", ex);
                }
            }

            // Emit events so frontend refreshes
            await _eventBus.EmitAsync(ModuleNames.MOD, "MOD_LIST_UPDATED", null).ConfigureAwait(false);
            _categoryService.InvalidateTreeCache();

            _logger.Info($"Import complete: {result.ImportedCount} new, {result.UpdatedCount} updated, {result.FailedCount} failed", "ModPackageService");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Import failed: {ex.Message}");
            _logger.Error($"Import failed: {ex.Message}", "ModPackageService", ex);
        }

        return result;
    }

    // ===== Private helpers =====

    private async Task ImportNewModAsync(string packagePath, PackageModEntry entry, bool importPreviews)
    {
        // Copy archive to mods directory with the original ID
        if (entry.HasArchive)
        {
            var archiveSource = Path.Combine(packagePath, "mods", entry.FileName);
            if (File.Exists(archiveSource))
            {
                await _archiveService.CopyArchiveAsync(archiveSource, entry.Id).ConfigureAwait(false);
            }
        }

        // Create mod record in database
        var createRequest = new CreateModRequest
        {
            Id = entry.Id,
            Category = entry.CategoryId,
            Name = entry.Name,
            Author = entry.Author,
            Description = entry.Description,
            Type = entry.Type,
            Grading = entry.Grading,
            Tags = entry.Tags
        };

        await _metadataService.CreateAsync(createRequest).ConfigureAwait(false);

        // Copy previews
        if (importPreviews && entry.HasPreviews && entry.PreviewFolder != null)
        {
            await CopyPreviewsAsync(packagePath, entry).ConfigureAwait(false);
        }
    }

    private async Task UpdateExistingModAsync(string packagePath, PackageModEntry entry, bool importPreviews)
    {
        // Update metadata
        var existingEntity = await _modRepository.GetByIdAsync(entry.Id).ConfigureAwait(false);
        if (existingEntity == null) return;

        existingEntity.Name = entry.Name;
        existingEntity.Author = entry.Author;
        existingEntity.Description = entry.Description;
        existingEntity.Category = entry.CategoryId;
        existingEntity.Tags = JsonSerializer.Serialize(entry.Tags);
        existingEntity.Grading = entry.Grading;
        existingEntity.UpdatedAt = DateTime.UtcNow;

        await _modRepository.UpdateAsync(existingEntity).ConfigureAwait(false);

        // Replace archive if package has one
        if (entry.HasArchive)
        {
            var archiveSource = Path.Combine(packagePath, "mods", entry.FileName);
            if (File.Exists(archiveSource))
            {
                // Delete existing archive first, then copy new one
                await _archiveService.DeleteArchiveAsync(entry.Id).ConfigureAwait(false);
                await _archiveService.CopyArchiveAsync(archiveSource, entry.Id).ConfigureAwait(false);
            }
        }

        // Replace previews
        if (importPreviews && entry.HasPreviews && entry.PreviewFolder != null)
        {
            await CopyPreviewsAsync(packagePath, entry).ConfigureAwait(false);
        }
    }

    private async Task CopyPreviewsAsync(string packagePath, PackageModEntry entry)
    {
        var sourceDir = Path.Combine(packagePath, "previews", entry.PreviewFolder!);
        if (!Directory.Exists(sourceDir)) return;

        var targetDir = _profilePaths.GetPreviewDirectoryPath(entry.Id);
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        await Task.CompletedTask;
    }

    private async Task EnsureCategoriesExistAsync(List<PackageCategory> categories)
    {
        if (categories.Count == 0) return;

        // Sort so parents come before children
        var sorted = TopologicalSortCategories(categories);

        foreach (var cat in sorted)
        {
            var exists = await _categoryService.ExistsAsync(cat.Id).ConfigureAwait(false);
            if (!exists)
            {
                await _categoryService.CreateAsync(
                    cat.Id, cat.Name, cat.ParentId, cat.Priority, cat.Description).ConfigureAwait(false);
                _logger.Info($"Created missing category: {cat.Name} ({cat.Id})", "ModPackageService");
            }
        }
    }

    private async Task EmitProgress(string operation, int current, int total, string modName, string stage)
    {
        await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.MOD_PACKAGE_PROGRESS, new PackageProgress
        {
            Operation = operation,
            Current = current,
            Total = total,
            CurrentModName = modName,
            Stage = stage
        }).ConfigureAwait(false);
    }

    // ===== Utility methods =====

    /// <summary>
    /// Sanitize a string for use as a file/folder name.
    /// Replaces illegal characters with underscore, trims, and limits length.
    /// </summary>
    internal static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "_";

        var sanitized = InvalidFileCharsRegex.Replace(name, "_").Trim();

        // Remove leading/trailing dots and spaces (Windows restriction)
        sanitized = sanitized.Trim('.', ' ');

        // Limit to reasonable length
        if (sanitized.Length > 200) sanitized = sanitized[..200];

        return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
    }

    /// <summary>
    /// Get a unique file name by appending " (2)", " (3)", etc. if needed.
    /// </summary>
    internal static string GetUniqueFileName(string desiredName, HashSet<string> usedNames)
    {
        if (!usedNames.Contains(desiredName)) return desiredName;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(desiredName);
        var ext = Path.GetExtension(desiredName);
        var counter = 2;

        while (true)
        {
            var candidate = $"{nameWithoutExt} ({counter}){ext}";
            if (!usedNames.Contains(candidate)) return candidate;
            counter++;
        }
    }

    /// <summary>
    /// Get a unique folder name by appending " (2)", " (3)", etc. if needed.
    /// </summary>
    internal static string GetUniqueFolderName(string desiredName, HashSet<string> usedNames)
    {
        if (!usedNames.Contains(desiredName)) return desiredName;

        var counter = 2;
        while (true)
        {
            var candidate = $"{desiredName} ({counter})";
            if (!usedNames.Contains(candidate)) return candidate;
            counter++;
        }
    }

    /// <summary>
    /// Build a map from category ID to human-readable path (e.g., "Characters > Avatars > Keqing").
    /// </summary>
    private Dictionary<string, string> BuildCategoryPathMap(List<CategoryInfo> tree)
    {
        var map = new Dictionary<string, string>();
        BuildCategoryPathMapRecursive(tree, "", map);
        return map;
    }

    private void BuildCategoryPathMapRecursive(List<CategoryInfo> nodes, string prefix, Dictionary<string, string> map)
    {
        foreach (var node in nodes)
        {
            var path = string.IsNullOrEmpty(prefix) ? node.Name : $"{prefix} > {node.Name}";
            map[node.Id] = path;
            BuildCategoryPathMapRecursive(node.Children, path, map);
        }
    }

    /// <summary>
    /// Flatten category tree to a dictionary of ID -> CategoryInfo.
    /// </summary>
    private Dictionary<string, CategoryInfo> BuildCategoryFlatMap(List<CategoryInfo> tree)
    {
        var map = new Dictionary<string, CategoryInfo>();
        FlattenTree(tree, map);
        return map;
    }

    private void FlattenTree(List<CategoryInfo> nodes, Dictionary<string, CategoryInfo> map)
    {
        foreach (var node in nodes)
        {
            map[node.Id] = node;
            FlattenTree(node.Children, map);
        }
    }

    /// <summary>
    /// Build the list of categories to include in manifest (only referenced + ancestors).
    /// </summary>
    private List<PackageCategory> BuildPackageCategories(
        HashSet<string> referencedIds, Dictionary<string, CategoryInfo> categoryMap)
    {
        // Collect referenced categories and all their ancestors
        var allNeededIds = new HashSet<string>();
        foreach (var id in referencedIds)
        {
            var currentId = id;
            while (!string.IsNullOrEmpty(currentId) && categoryMap.ContainsKey(currentId))
            {
                allNeededIds.Add(currentId);
                currentId = categoryMap[currentId].ParentId ?? "";
            }
        }

        return allNeededIds
            .Where(id => categoryMap.ContainsKey(id))
            .Select(id =>
            {
                var cat = categoryMap[id];
                return new PackageCategory
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    ParentId = cat.ParentId,
                    Priority = cat.Priority,
                    Description = cat.Description
                };
            })
            .ToList();
    }

    private static List<PackageCategory> TopologicalSortCategories(List<PackageCategory> categories)
    {
        var map = categories.ToDictionary(c => c.Id);
        var sorted = new List<PackageCategory>();
        var visited = new HashSet<string>();

        void Visit(PackageCategory cat)
        {
            if (visited.Contains(cat.Id)) return;
            if (cat.ParentId != null && map.ContainsKey(cat.ParentId))
                Visit(map[cat.ParentId]);
            visited.Add(cat.Id);
            sorted.Add(cat);
        }

        foreach (var cat in categories) Visit(cat);
        return sorted;
    }

    private static bool ListsEqual(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        var sortedA = a.OrderBy(x => x).ToList();
        var sortedB = b.OrderBy(x => x).ToList();
        return sortedA.SequenceEqual(sortedB);
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"
    };

    private static bool IsImageFile(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path));
    }

    private static long CalculateDirectorySize(string path)
    {
        return new DirectoryInfo(path)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }
}
