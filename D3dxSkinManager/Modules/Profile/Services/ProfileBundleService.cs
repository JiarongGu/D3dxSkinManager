using System.IO.Compression;
using System.Text.Json;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;
using Microsoft.Extensions.DependencyInjection;

namespace D3dxSkinManager.Modules.Profiles.Services;

/// <summary>
/// Exports / imports a PORTABLE slice of a profile as a <c>.zip</c> bundle (manifest <c>profile.json</c>
/// + thumbnails). Contents: profile metadata + config + profile thumbnail, the category tree + category
/// thumbnails, and remote libraries + tag-rules + tag-labels + customized source overlays. Deliberately
/// EXCLUDES mod archives / mod DB rows / previews and login credentials (online-accounts.json is global +
/// DPAPI-bound). Import always creates a NEW profile.
///
/// GLOBAL service (behind the global <see cref="ProfileFacade"/>). Profile metadata + config + thumbnail
/// go through the global <see cref="IProfileService"/>; the profile-scoped data (categories, remote) is
/// read/written via <see cref="IProfileServiceProvider"/> — the source profile's scope for export, the
/// freshly-created profile's scope for import — without switching the active profile. Mirrors
/// <c>ModPackageService</c> (manifest+files, path-traversal guard, ProcessRegistry).
/// </summary>
public interface IProfileBundleService
{
    /// <summary>Export a profile's settings into a <c>{name}.zip</c> under the export folder.</summary>
    Task<ProfileBundleExportResult> ExportAsync(ProfileBundleExportConfig config);

    /// <summary>Read-only preview of a bundle (folder OR .zip) for the import UI. Non-throwing.</summary>
    Task<ProfileBundleAnalysis> AnalyzeAsync(string bundlePath);

    /// <summary>Import a bundle (folder OR .zip) as a NEW profile.</summary>
    Task<ProfileBundleImportResult> ImportAsync(ProfileBundleImportConfig config);
}

public class ProfileBundleService : IProfileBundleService
{
    private const string ManifestFileName = "profile.json";
    private const string SupportedVersion = "1.0";
    private const string ProfileThumbnailEntry = "profile.png"; // under thumbnails/

    private readonly IProfileService _profileService;
    private readonly IGlobalPathService _globalPaths;
    private readonly IPathHelper _pathHelper;
    private readonly IProfileServiceProvider _profileServices;
    private readonly IPathValidator _pathValidator;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogHelper _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public ProfileBundleService(
        IProfileService profileService,
        IGlobalPathService globalPaths,
        IPathHelper pathHelper,
        IProfileServiceProvider profileServices,
        IPathValidator pathValidator,
        IProcessRegistry processRegistry,
        ILogHelper logger)
    {
        _profileService = profileService;
        _globalPaths = globalPaths;
        _pathHelper = pathHelper;
        _profileServices = profileServices;
        _pathValidator = pathValidator;
        _processRegistry = processRegistry;
        _logger = logger;
    }

    // ===== Export =====

    public async Task<ProfileBundleExportResult> ExportAsync(ProfileBundleExportConfig config)
    {
        var result = new ProfileBundleExportResult();
        var procId = _processRegistry.Start(ProcessType.Package, "Exporting profile settings",
            titleKey: "process.profileExport");

        var stagingDir = string.Empty;
        try
        {
            var profile = await _profileService.GetProfileByIdAsync(config.ProfileId).ConfigureAwait(false)
                ?? throw new OperationException("PROFILE_BUNDLE_PROFILE_NOT_FOUND",
                    new Dictionary<string, string> { { "profileId", config.ProfileId } },
                    $"Profile not found: {config.ProfileId}");

            result.ProfileName = profile.Name;

            var profileConfig = await _profileService.GetProfileConfigurationAsync(config.ProfileId).ConfigureAwait(false)
                ?? new ProfileConfiguration { ProfileId = config.ProfileId };

            stagingDir = Path.Combine(ProfileTempDir(config.ProfileId), $"bundle-export-{Guid.NewGuid():N}");
            var thumbsDir = Path.Combine(stagingDir, "thumbnails");
            Directory.CreateDirectory(thumbsDir);

            var manifest = new ProfileBundleManifest
            {
                ProfileName = profile.Name,
                Description = profile.Description,
                Color = profile.Color,
                GameName = profile.GameName,
                Configuration = SanitizeConfig(profileConfig),
            };

            // Profile thumbnail
            var profileThumbAbs = ResolveLocalImagePath(profile.Thumbnail);
            if (profileThumbAbs != null && File.Exists(profileThumbAbs))
            {
                File.Copy(profileThumbAbs, Path.Combine(thumbsDir, ProfileThumbnailEntry), overwrite: true);
                manifest.HasThumbnail = true;
            }

            // Category tree (+ thumbnails)
            if (config.IncludeCategories)
            {
                var categoryService = ResolveScoped<ICategoryService>(config.ProfileId);
                var tree = await categoryService.GetCategoryTreeAsync().ConfigureAwait(false);
                var categoryThumbsDir = Path.Combine(thumbsDir, "categories");
                foreach (var node in FlattenCategories(tree))
                {
                    var entry = new ProfileBundleCategory
                    {
                        Id = node.Id,
                        Name = node.Name,
                        ParentId = node.ParentId,
                        Priority = node.Priority,
                        Description = node.Description,
                    };

                    var catThumbAbs = ResolveLocalImagePath(node.Thumbnail);
                    if (catThumbAbs != null && File.Exists(catThumbAbs))
                    {
                        Directory.CreateDirectory(categoryThumbsDir);
                        var fileName = $"{node.Id}.png";
                        File.Copy(catThumbAbs, Path.Combine(categoryThumbsDir, fileName), overwrite: true);
                        entry.ThumbnailFile = $"categories/{fileName}";
                    }

                    manifest.Categories.Add(entry);
                }
                result.CategoryCount = manifest.Categories.Count;
            }

            // Remote libraries + tag-rules + tag-labels + customized source overlays
            if (config.IncludeRemote)
            {
                ExportRemote(config.ProfileId, manifest);
                result.LibraryCount = manifest.Libraries.Count;
            }

            // Write manifest
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(stagingDir, ManifestFileName), manifestJson).ConfigureAwait(false);

            // Zip it up next to the caller-chosen output folder
            Directory.CreateDirectory(config.OutputPath);
            var outputZip = Path.Combine(config.OutputPath, SanitizeFileName(profile.Name) + ".zip");
            if (File.Exists(outputZip)) File.Delete(outputZip);
            ZipFile.CreateFromDirectory(stagingDir, outputZip, CompressionLevel.Optimal, includeBaseDirectory: false);

            result.OutputPath = outputZip;
            result.TotalSizeBytes = new FileInfo(outputZip).Length;
            result.Success = true;
            _logger.Info($"Exported profile settings: {profile.Name} → {outputZip} " +
                $"({result.CategoryCount} categories, {result.LibraryCount} libraries)", "ProfileBundleService");
        }
        catch (OperationException ex)
        {
            _processRegistry.Fail(procId, ex.Message);
            _logger.Error($"Profile settings export failed: {ex.Message}", "ProfileBundleService", ex);
            result.Errors.Add(ex.Message);
            CleanupStaging(stagingDir);
            throw;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Export failed: {ex.Message}");
            _logger.Error($"Profile settings export failed: {ex.Message}", "ProfileBundleService", ex);
        }
        finally
        {
            CleanupStaging(stagingDir);
        }

        if (result.Success) _processRegistry.Complete(procId);
        else _processRegistry.Fail(procId, string.Join("; ", result.Errors));
        return result;
    }

    private void ExportRemote(string profileId, ProfileBundleManifest manifest)
    {
        var libraryStore = ResolveScoped<IRemoteLibraryStore>(profileId);
        var sourceStore = ResolveScoped<IRemoteSourceStore>(profileId);
        var tagLabelStore = ResolveScoped<IRemoteTagLabelStore>(profileId);

        var libraries = libraryStore.GetState().Libraries;
        foreach (var lib in libraries)
        {
            manifest.Libraries.Add(new ProfileBundleLibrary
            {
                Id = lib.Id,
                SourceId = lib.SourceId,
                ListId = lib.ListId,
                Name = lib.Name,
                PreferCache = lib.PreferCache,
                AddedAtUtc = lib.AddedAtUtc,
                ParamValues = new Dictionary<string, string>(lib.ParamValues),
                TagRules = lib.TagRules.Select(r => new ProfileBundleTagRule
                {
                    Name = r.Name,
                    Tags = new List<string>(r.Tags),
                    TitlePattern = r.TitlePattern,
                    CategoryId = r.CategoryId,
                }).ToList(),
            });
        }

        // Only the sources this profile's libraries actually use.
        var usedSourceIds = libraries.Select(l => l.SourceId).Distinct().ToList();
        var origins = sourceStore.GetOrigins();
        foreach (var sourceId in usedSourceIds)
        {
            RemoteSourceConfig? sourceConfig = null;
            try { sourceConfig = sourceStore.GetById(sourceId); }
            catch (Exception ex) { _logger.Warn($"Skipping unknown remote source '{sourceId}' during export: {ex.Message}", "ProfileBundleService"); }
            if (sourceConfig == null) continue;

            // Tag labels for this source (effective per-language labels).
            var labels = tagLabelStore.GetForSource(sourceId, sourceConfig.TagLabels);
            if (labels.Count > 0)
            {
                manifest.TagLabels.Add(new ProfileBundleTagLabelSet { SourceId = sourceId, Labels = labels });
            }

            // Customized/custom source overlays only (a shipped-default source needs no overlay — the
            // recipient already ships it).
            if (origins.TryGetValue(sourceId, out var origin) && (origin == "customized" || origin == "custom"))
            {
                manifest.SourceOverlays.Add(new ProfileBundleSourceOverlay
                {
                    SourceId = sourceId,
                    ConfigJson = JsonSerializer.Serialize(sourceConfig, JsonOptions),
                });
            }
        }
    }

    // ===== Analyze =====

    public async Task<ProfileBundleAnalysis> AnalyzeAsync(string bundlePath)
    {
        var analysis = new ProfileBundleAnalysis();
        try
        {
            var manifest = await ReadManifestAsync(bundlePath).ConfigureAwait(false);
            if (manifest == null)
            {
                analysis.ErrorMessage = "No profile.json manifest found in the selected bundle.";
                return analysis;
            }

            analysis.Version = manifest.Version;
            analysis.ProfileName = manifest.ProfileName;
            analysis.Description = manifest.Description;
            analysis.Color = manifest.Color;
            analysis.GameName = manifest.GameName;
            analysis.ExportDate = manifest.ExportDate;
            analysis.HasThumbnail = manifest.HasThumbnail;
            analysis.CategoryCount = manifest.Categories.Count;
            analysis.LibraryCount = manifest.Libraries.Count;
            analysis.TagLabelSourceCount = manifest.TagLabels.Count;
            analysis.SourceOverlayCount = manifest.SourceOverlays.Count;

            if (manifest.Version != SupportedVersion)
            {
                analysis.ErrorMessage = $"Unsupported bundle version: {manifest.Version} (expected {SupportedVersion}).";
                return analysis;
            }

            analysis.IsValid = true;
        }
        catch (Exception ex)
        {
            analysis.ErrorMessage = $"Failed to analyze bundle: {ex.Message}";
            _logger.Error($"Profile bundle analysis failed: {ex.Message}", "ProfileBundleService", ex);
        }
        return analysis;
    }

    // ===== Import =====

    public async Task<ProfileBundleImportResult> ImportAsync(ProfileBundleImportConfig config)
    {
        var result = new ProfileBundleImportResult();
        var procId = _processRegistry.Start(ProcessType.Package, "Importing profile settings",
            titleKey: "process.profileImport");

        var stagingDir = string.Empty;
        try
        {
            var manifest = await ReadManifestAsync(config.BundlePath).ConfigureAwait(false)
                ?? throw new OperationException("PROFILE_BUNDLE_INVALID",
                    new Dictionary<string, string> { { "path", config.BundlePath } },
                    "The selected bundle has no valid profile.json manifest.");

            if (manifest.Version != SupportedVersion)
            {
                throw new OperationException("PROFILE_BUNDLE_VERSION_UNSUPPORTED",
                    new Dictionary<string, string> { { "version", manifest.Version } },
                    $"Unsupported bundle version: {manifest.Version}");
            }

            var name = !string.IsNullOrWhiteSpace(config.NewProfileName)
                ? config.NewProfileName!.Trim()
                : (!string.IsNullOrWhiteSpace(manifest.ProfileName) ? manifest.ProfileName : "Imported profile");

            var created = await _profileService.CreateProfileAsync(new CreateProfileRequest
            {
                Name = name,
                Description = manifest.Description,
                Color = manifest.Color,
                GameName = manifest.GameName,
            }).ConfigureAwait(false);

            result.NewProfileId = created.Id;
            result.ProfileName = created.Name;

            // Root the bundle files: a folder is used in place; a .zip is extracted (guarded) into the
            // new profile's temp (same volume) before use.
            var (root, extracted) = await PrepareBundleRootAsync(config.BundlePath, created.Id).ConfigureAwait(false);
            stagingDir = extracted ? root : string.Empty;

            // Configuration (already sanitized on export; re-sanitize defensively for an older/hand-made bundle).
            if (manifest.Configuration != null)
            {
                var cfg = SanitizeConfig(manifest.Configuration);
                cfg.ProfileId = created.Id;
                await _profileService.UpdateProfileConfigurationAsync(cfg).ConfigureAwait(false);
            }

            // Profile thumbnail
            if (manifest.HasThumbnail &&
                TryResolveEntryPath(root, "thumbnails", ProfileThumbnailEntry, out var profileThumb) &&
                File.Exists(profileThumb))
            {
                await _profileService.UpdateProfileAsync(new UpdateProfileRequest
                {
                    ProfileId = created.Id,
                    ThumbnailPath = profileThumb,
                }).ConfigureAwait(false);
            }

            if (config.ImportCategories && manifest.Categories.Count > 0)
            {
                result.ImportedCategoryCount = await ImportCategoriesAsync(created.Id, manifest, root, result).ConfigureAwait(false);
            }

            if (config.ImportRemote)
            {
                ImportRemote(created.Id, manifest, result);
            }

            result.Success = true;
            _logger.Info($"Imported profile settings as new profile '{name}' ({created.Id}): " +
                $"{result.ImportedCategoryCount} categories, {result.ImportedLibraryCount} libraries", "ProfileBundleService");
        }
        catch (OperationException ex)
        {
            result.Errors.Add(ex.Message);
            _processRegistry.Fail(procId, ex.Message);
            _logger.Error($"Profile settings import failed: {ex.Message}", "ProfileBundleService", ex);
            CleanupStaging(stagingDir);
            throw;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Import failed: {ex.Message}");
            _logger.Error($"Profile settings import failed: {ex.Message}", "ProfileBundleService", ex);
            _processRegistry.Fail(procId, ex.Message);
        }
        finally
        {
            CleanupStaging(stagingDir);
        }

        if (result.Success) _processRegistry.Complete(procId);
        return result;
    }

    private async Task<int> ImportCategoriesAsync(string profileId, ProfileBundleManifest manifest, string root,
        ProfileBundleImportResult result)
    {
        var categoryService = ResolveScoped<ICategoryService>(profileId);
        var count = 0;
        foreach (var cat in TopologicalSortCategories(manifest.Categories))
        {
            try
            {
                if (await categoryService.ExistsAsync(cat.Id).ConfigureAwait(false)) continue;

                string? thumbnailPath = null;
                if (!string.IsNullOrEmpty(cat.ThumbnailFile) &&
                    TryResolveEntryPath(root, "thumbnails", cat.ThumbnailFile, out var resolved) &&
                    File.Exists(resolved))
                {
                    thumbnailPath = resolved;
                }

                var createdCat = await categoryService.CreateAsync(
                    cat.Id, cat.Name, cat.ParentId, cat.Priority, cat.Description, thumbnailPath).ConfigureAwait(false);
                if (createdCat != null) count++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Category '{cat.Name}': {ex.Message}");
                _logger.Warn($"Failed to import category '{cat.Name}': {ex.Message}", "ProfileBundleService");
            }
        }
        categoryService.InvalidateTreeCache();
        return count;
    }

    private void ImportRemote(string profileId, ProfileBundleManifest manifest, ProfileBundleImportResult result)
    {
        var sourceStore = ResolveScoped<IRemoteSourceStore>(profileId);
        var libraryStore = ResolveScoped<IRemoteLibraryStore>(profileId);
        var tagLabelStore = ResolveScoped<IRemoteTagLabelStore>(profileId);

        // Source overlays FIRST (a library needs its source to exist). ADD-MISSING-ONLY: never overwrite
        // a source the target machine already customized — those overlays are GLOBAL + shared across
        // every profile, so an import must not silently change another profile's remote behavior.
        var origins = sourceStore.GetOrigins();
        foreach (var overlay in manifest.SourceOverlays)
        {
            var alreadyCustomized = origins.TryGetValue(overlay.SourceId, out var origin)
                && (origin == "customized" || origin == "custom");
            if (alreadyCustomized) continue;
            try
            {
                var sourceConfig = JsonSerializer.Deserialize<RemoteSourceConfig>(overlay.ConfigJson, JsonOptions);
                if (sourceConfig == null) continue;
                sourceStore.Save(sourceConfig);
                result.ImportedSourceOverlayCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Source overlay '{overlay.SourceId}': {ex.Message}");
                _logger.Warn($"Failed to import source overlay '{overlay.SourceId}': {ex.Message}", "ProfileBundleService");
            }
        }

        // Libraries (fresh ids in the new profile; tag-rule CategoryIds match the imported categories).
        foreach (var lib in manifest.Libraries)
        {
            try
            {
                var tagRules = lib.TagRules.Select(r => new RemoteTagRule
                {
                    Name = r.Name,
                    Tags = new List<string>(r.Tags),
                    TitlePattern = r.TitlePattern,
                    CategoryId = r.CategoryId,
                }).ToList();
                libraryStore.Add(lib.SourceId, lib.ListId, lib.Name, tagRules, new Dictionary<string, string>(lib.ParamValues));
                result.ImportedLibraryCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Library '{lib.Name}': {ex.Message}");
                _logger.Warn($"Failed to import library '{lib.Name}': {ex.Message}", "ProfileBundleService");
            }
        }

        // Tag labels (per source, per language).
        foreach (var set in manifest.TagLabels)
        {
            RemoteSourceConfig? sourceConfig = null;
            try { sourceConfig = sourceStore.GetById(set.SourceId); }
            catch { /* source not present locally — skip its labels */ }
            if (sourceConfig == null) continue;
            foreach (var (lang, labels) in set.Labels)
            {
                try
                {
                    tagLabelStore.SetLangLabels(set.SourceId, lang, labels, sourceConfig.TagLabels);
                    result.ImportedTagLabelCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Tag labels '{set.SourceId}/{lang}': {ex.Message}");
                    _logger.Warn($"Failed to import tag labels '{set.SourceId}/{lang}': {ex.Message}", "ProfileBundleService");
                }
            }
        }
    }

    // ===== Bundle IO helpers =====

    /// <summary>Read the manifest from a folder (reads <c>profile.json</c>) or a .zip (reads the entry
    /// without a full extract). Returns null when there is no readable manifest.</summary>
    private async Task<ProfileBundleManifest?> ReadManifestAsync(string bundlePath)
    {
        string json;
        if (Directory.Exists(bundlePath))
        {
            var manifestPath = Path.Combine(bundlePath, ManifestFileName);
            if (!File.Exists(manifestPath)) return null;
            json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
        }
        else if (File.Exists(bundlePath))
        {
            using var archive = ZipFile.OpenRead(bundlePath);
            var entry = archive.GetEntry(ManifestFileName);
            if (entry == null) return null;
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            json = await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        else
        {
            return null;
        }

        return JsonSerializer.Deserialize<ProfileBundleManifest>(json, JsonOptions);
    }

    /// <summary>Give an on-disk root for the bundle's files: a folder is used in place (extracted=false);
    /// a .zip is extracted (per-entry path-traversal guarded) into the new profile's temp (extracted=true).</summary>
    private async Task<(string root, bool extracted)> PrepareBundleRootAsync(string bundlePath, string newProfileId)
    {
        if (Directory.Exists(bundlePath)) return (bundlePath, false);

        var target = Path.Combine(ProfileTempDir(newProfileId), $"bundle-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(target);

        using var archive = ZipFile.OpenRead(bundlePath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
            if (!TryResolveEntryPath(target, string.Empty, entry.FullName, out var dest))
            {
                throw new OperationException("PROFILE_BUNDLE_UNSAFE_ENTRY",
                    new Dictionary<string, string> { { "name", entry.FullName } },
                    $"Refused unsafe bundle entry: {entry.FullName}");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
        }
        await Task.CompletedTask;
        return (target, true);
    }

    // ===== Shared helpers =====

    private string ProfileTempDir(string profileId) =>
        Path.Combine(_globalPaths.GetProfileDirectoryPath(profileId), "temp");

    private T ResolveScoped<T>(string profileId) where T : notnull =>
        _profileServices.GetProfileServices(profileId).GetRequiredService<T>();

    /// <summary>Strip machine-specific / path-leaking fields so the bundle is portable and shareable
    /// (no absolute paths, no Windows username): reset the work mode to internal, drop the launch command,
    /// external/xxmi work dir, and fix-tool interpreter. The import UI lets the user re-pick the work mode
    /// (and wire XXMI launch). Everything else (compression, fix-tool timeout/extensions, UI/tab prefs,
    /// window positions, game-updated watermark) survives.</summary>
    private static ProfileConfiguration SanitizeConfig(ProfileConfiguration source)
    {
        return new ProfileConfiguration
        {
            ProfileId = source.ProfileId,
            ModWork = new ModWorkConfiguration
            {
                Mode = "internal",
                Directory = null,
                InternalDirectory = null,
                CleanupEnabled = source.ModWork.CleanupEnabled,
                CleanupMaxCaches = source.ModWork.CleanupMaxCaches,
            },
            Windows = source.Windows,
            Tabs = source.Tabs,
            ModImport = source.ModImport,
            Launch = new LaunchConfiguration(),
            FixTools = new ModFixConfiguration
            {
                PythonPath = string.Empty,
                TimeoutMinutes = source.FixTools.TimeoutMinutes,
                SupportedExtensions = source.FixTools.SupportedExtensions,
                AutoConfirm = source.FixTools.AutoConfirm,
            },
            GameUpdatedUtc = source.GameUpdatedUtc,
        };
    }

    /// <summary>Resolve a stored image reference (a <c>file://</c> URL as category thumbnails carry, or a
    /// relative profile path) to an absolute local path. Null/unparseable → null.</summary>
    private string? ResolveLocalImagePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                return new Uri(value).LocalPath;
            if (Path.IsPathRooted(value)) return value;
            return _pathHelper.ToAbsolutePath(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Confine an untrusted manifest-supplied entry name under a bundle subdirectory, rejecting
    /// <c>..</c> traversal or a rooted value. Mirrors ModPackageService.TryResolvePackageEntryPath.</summary>
    private bool TryResolveEntryPath(string root, string subDir, string? entryName, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(entryName)) return false;

        var baseDir = string.IsNullOrEmpty(subDir) ? root : Path.Combine(root, subDir);
        var candidate = Path.Combine(baseDir, entryName);
        if (!_pathValidator.IsPathWithin(root, candidate))
        {
            _logger.Warn($"[ProfileBundleService] Rejected unsafe bundle entry '{entryName}' under '{subDir}'", "ProfileBundleService");
            return false;
        }
        fullPath = candidate;
        return true;
    }

    private static IEnumerable<CategoryInfo> FlattenCategories(List<CategoryInfo> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenCategories(node.Children))
                yield return child;
        }
    }

    /// <summary>Order categories so a parent always precedes its children (import needs the parent first).</summary>
    private static List<ProfileBundleCategory> TopologicalSortCategories(List<ProfileBundleCategory> categories)
    {
        var map = categories.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        var sorted = new List<ProfileBundleCategory>();
        var visited = new HashSet<string>();

        void Visit(ProfileBundleCategory cat)
        {
            if (!visited.Add(cat.Id)) return;
            if (cat.ParentId != null && map.TryGetValue(cat.ParentId, out var parent))
                Visit(parent);
            sorted.Add(cat);
        }

        foreach (var cat in categories) Visit(cat);
        return sorted;
    }

    private void CleanupStaging(string stagingDir)
    {
        if (string.IsNullOrEmpty(stagingDir)) return;
        try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true); }
        catch (Exception ex) { _logger.Warn($"Failed to clean bundle staging '{stagingDir}': {ex.Message}", "ProfileBundleService"); }
    }

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>Sanitize a profile name into a safe zip file name.</summary>
    internal static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "profile";
        var sanitized = new string(name.Select(c => InvalidFileNameChars.Contains(c) ? '_' : c).ToArray())
            .Trim().Trim('.', ' ');
        if (sanitized.Length > 100) sanitized = sanitized[..100];
        return string.IsNullOrWhiteSpace(sanitized) ? "profile" : sanitized;
    }
}
