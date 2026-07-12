using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Service for enriching mod data with derived/computed values
/// Responsibility: Populate transient fields (status flags, category/library names, tag metadata)
/// </summary>
public interface IModEnrichmentService
{
    void PopulateStatusFlags(List<ModInfo> mods);
    Task PopulateCategoryNamesAsync(List<ModInfo> mods);
    Task PopulateTagMetadataAsync(List<ModInfo> mods);
    void PopulateLibraryNames(List<ModInfo> mods);
    Task<ModInfo> EnrichAsync(ModInfo mod);
    Task<List<ModInfo>> EnrichAllAsync(List<ModInfo> mods);
}

public class ModEnrichmentService : IModEnrichmentService
{
    private readonly IProfilePathService _profilePaths;
    private readonly ICategoryService _categoryService;
    private readonly ITagRepository _tagRepository;
    private readonly IModCacheService _cacheService;
    private readonly IRemoteLibraryStore _libraryStore;

    public ModEnrichmentService(
        IProfilePathService profilePaths,
        ICategoryService categoryService,
        ITagRepository tagRepository,
        IModCacheService cacheService,
        IRemoteLibraryStore libraryStore)
    {
        _profilePaths = profilePaths;
        _categoryService = categoryService;
        _tagRepository = tagRepository;
        _cacheService = cacheService;
        _libraryStore = libraryStore;
    }

    /// <summary>
    /// Populates status flags (IsLoaded, IsAvailable, HasPreviewFolder) for all mods in bulk by scanning directories once
    /// </summary>
    public void PopulateStatusFlags(List<ModInfo> mods)
    {
        var availableFiles = Directory.Exists(_profilePaths.ModsDirectory)
            ? Directory.GetFiles(_profilePaths.ModsDirectory)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => f!)
                .ToHashSet()
            : new HashSet<string>();

        var loadedDirectories = Directory.Exists(_profilePaths.CacheModsDirectory)
            ? Directory.GetDirectories(_profilePaths.CacheModsDirectory)
                .Select(Path.GetFileName)
                .Where(d => !string.IsNullOrEmpty(d) && !ModConventions.IsDisabledCacheName(d))
                .Select(d => d!)
                .ToHashSet()
            : new HashSet<string>();

        var allCacheDirectories = Directory.Exists(_profilePaths.CacheModsDirectory)
            ? Directory.GetDirectories(_profilePaths.CacheModsDirectory)
                .Select(Path.GetFileName)
                .Where(d => !string.IsNullOrEmpty(d))
                .Select(d => ModConventions.CacheNameToModId(d!))
                .ToHashSet()
            : new HashSet<string>();

        // Build a HashSet of mods that have preview folders with actual preview images
        var modsWithPreviews = new HashSet<string>();
        if (Directory.Exists(_profilePaths.PreviewsDirectory))
        {
            try
            {
                foreach (var previewDir in Directory.GetDirectories(_profilePaths.PreviewsDirectory))
                {
                    var id = Path.GetFileName(previewDir);
                    if (string.IsNullOrEmpty(id))
                        continue;

                    try
                    {
                        // Check if directory exists and contains any preview image files
                        if (Directory.Exists(previewDir))
                        {
                            var hasPreviewFiles = Directory.GetFiles(previewDir, "preview*.*")
                                .Any(f => Core.Constants.ImageConstants.IsImageExtension(Path.GetExtension(f)));

                            if (hasPreviewFiles)
                            {
                                modsWithPreviews.Add(id);
                            }
                        }
                    }
                    catch
                    {
                        // Silently skip preview folder if inaccessible or deleted - don't fail enrichment
                    }
                }
            }
            catch
            {
                // Silently skip if previews directory is inaccessible - don't fail enrichment
            }
        }

        foreach (var mod in mods)
        {
            mod.IsAvailable = availableFiles.Contains(mod.Id);
            mod.IsLoaded = loadedDirectories.Contains(mod.Id);
            mod.HasCache = allCacheDirectories.Contains(mod.Id);
            mod.HasPreviewFolder = modsWithPreviews.Contains(mod.Id);

            // Populate file paths using proper path resolution
            // GetCachePath handles both active ({ID}) and disabled (DISABLED-{ID}) cache directories
            if (mod.HasCache)
            {
                mod.CachePath = _cacheService.GetCachePath(mod.Id);
            }

            if (mod.HasPreviewFolder)
            {
                mod.PreviewFolderPath = _profilePaths.GetPreviewDirectoryPath(mod.Id);
            }

            // Always populate ArchiveFolderPath - this is where mod archives are stored
            mod.ArchiveFolderPath = _profilePaths.ModsDirectory;
        }
    }

    /// <summary>
    /// Populates CategoryName field for all mods based on their Category (Category ID)
    /// Uses CategoryService.GetCategoryNameAsync which has built-in caching
    /// </summary>
    public async Task PopulateCategoryNamesAsync(List<ModInfo> mods)
    {
        // Get distinct category IDs that need names
        var categoryIds = mods
            .Where(m => !string.IsNullOrEmpty(m.Category))
            .Select(m => m.Category)
            .Distinct()
            .ToList();

        if (!categoryIds.Any())
            return;

        // Populate category names using the cached service method
        // The first call will build and cache the map, subsequent calls use the cache
        foreach (var mod in mods)
        {
            if (!string.IsNullOrEmpty(mod.Category))
            {
                mod.CategoryName = await _categoryService.GetCategoryNameAsync(mod.Category).ConfigureAwait(false) ?? "";
            }
        }
    }

    /// <summary>
    /// Populates LibraryName for mods imported from a remote library, resolved LIVE from the
    /// RemoteLibraries table by RemoteLibraryId (a rename reflects on next load; a removed library →
    /// empty). One GetState() call builds an id→name map for the whole list. Mirrors category names.
    /// </summary>
    public void PopulateLibraryNames(List<ModInfo> mods)
    {
        if (mods.All(m => string.IsNullOrEmpty(m.RemoteLibraryId)))
            return; // nothing remote-imported — skip the store hit

        var byId = _libraryStore.GetState().Libraries
            .GroupBy(l => l.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        foreach (var mod in mods)
        {
            mod.LibraryName = !string.IsNullOrEmpty(mod.RemoteLibraryId) && byId.TryGetValue(mod.RemoteLibraryId, out var name)
                ? name
                : string.Empty;
        }
    }

    /// <summary>
    /// Populates tag metadata (colors) for all mods
    /// </summary>
    public async Task PopulateTagMetadataAsync(List<ModInfo> mods)
    {
        var allTagNames = mods
            .Where(m => m.Tags != null && m.Tags.Count > 0)
            .SelectMany(m => m.Tags!)
            .Distinct()
            .ToList();

        if (!allTagNames.Any())
            return;

        var allTags = await _tagRepository.GetAllAsync().ConfigureAwait(false);
        var tagMap = allTags.ToDictionary(t => t.Name, t => t);

        foreach (var mod in mods)
        {
            if (mod.Tags == null || mod.Tags.Count == 0)
                continue;

            mod.TagsWithMetadata = mod.Tags
                .Where(tagName => tagMap.ContainsKey(tagName))
                .Select(tagName => tagMap[tagName])
                .ToList();
        }
    }

    /// <summary>
    /// Enriches a single mod with all computed values
    /// </summary>
    public async Task<ModInfo> EnrichAsync(ModInfo mod)
    {
        var mods = new List<ModInfo> { mod };
        await EnrichAllAsync(mods);
        return mods[0];
    }

    /// <summary>
    /// Enriches all mods with status flags, category names, and tag metadata
    /// </summary>
    public async Task<List<ModInfo>> EnrichAllAsync(List<ModInfo> mods)
    {
        PopulateStatusFlags(mods);
        await PopulateCategoryNamesAsync(mods);
        PopulateLibraryNames(mods);
        await PopulateTagMetadataAsync(mods);
        return mods;
    }
}
