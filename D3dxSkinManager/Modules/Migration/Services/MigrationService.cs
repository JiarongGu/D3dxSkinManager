using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Tools.Services;

using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Modules.Migration.Services;

/// <summary>
/// Service for migrating data from Python d3dxSkinManage to React version
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Analyze Python installation and return migration analysis
    /// </summary>
    Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath);

    /// <summary>
    /// Perform migration with specified options
    /// </summary>
    /// <param name="options">Migration options</param>
    /// <param name="progress">Progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<MigrationResult> MigrateAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate migration by comparing source and destination
    /// </summary>
    Task<bool> ValidateMigrationAsync(string pythonPath, string reactDataPath);

    /// <summary>
    /// Auto-detect Python installation path
    /// </summary>
    Task<string?> AutoDetectPythonInstallationAsync();
}

/// <summary>
/// Service for migrating data from Python d3dxSkinManage to React version
/// </summary>
public class MigrationService : IMigrationService
{
    private readonly IProfileContext _profileContext;
    private readonly IModRepository _repository;
    private readonly IClassificationRepository _classificationRepository;
    private readonly IClassificationThumbnailService _thumbnailService;
    private readonly IFileService _fileService;
    private readonly IImageService _imageService;
    private readonly IConfigurationService _configService;
    private readonly IModManagementService _modManagementService;

    public MigrationService(
        IProfileContext profileContext,
        IModRepository repository,
        IClassificationRepository classificationRepository,
        IClassificationThumbnailService thumbnailService,
        IFileService fileService,
        IImageService imageService,
        IConfigurationService configService,
        IModManagementService modManagementService)
    {
        _profileContext = profileContext;
        _repository = repository;
        _classificationRepository = classificationRepository;
        _thumbnailService = thumbnailService;
        _fileService = fileService;
        _imageService = imageService;
        _configService = configService;
        _modManagementService = modManagementService;
    }

    public async Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath)
    {
        var analysis = new MigrationAnalysis
        {
            SourcePath = pythonPath,
            IsValid = false
        };

        try
        {
            // Validate Python installation structure
            if (!Directory.Exists(pythonPath))
            {
                analysis.Errors.Add($"Directory not found: {pythonPath}");
                return analysis;
            }

            // Check for key directories
            var resourcesPath = Path.Combine(pythonPath, "resources");
            var homePath = Path.Combine(pythonPath, "home");

            if (!Directory.Exists(resourcesPath))
            {
                analysis.Errors.Add("'resources' directory not found - not a valid Python installation");
                return analysis;
            }

            // Detect environments
            List<string> environments = new List<string>();

            if (!Directory.Exists(homePath))
            {
                analysis.Warnings.Add("'home' directory not found - no user environments configured");
                // Use default environment name
                analysis.ActiveEnvironment = "Default";
            }
            else
            {
                environments = Directory.GetDirectories(homePath)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList()!;

                if (environments.Count == 0)
                {
                    analysis.Warnings.Add("No user environments found in 'home' directory");
                    analysis.ActiveEnvironment = "Default";
                }
                else
                {
                    analysis.ActiveEnvironment = environments[0]; // Default to first
                }
            }

            analysis.Environments = environments;

            // Count mods from resources/mods directory
            var modsPath = Path.Combine(resourcesPath, "mods");
            if (Directory.Exists(modsPath))
            {
                try
                {
                    var archiveFiles = Directory.GetFiles(modsPath);
                    analysis.TotalMods = archiveFiles.Length;
                    analysis.TotalArchiveSize = archiveFiles.Sum(f => new FileInfo(f).Length);
                }
                catch (Exception ex)
                {
                    analysis.Warnings.Add($"Could not fully scan mods directory: {ex.Message}");
                    Console.WriteLine($"[Migration] Mods scan warning: {ex.Message}");
                }
            }

            // Count preview images
            var previewPath = Path.Combine(resourcesPath, "preview");
            if (Directory.Exists(previewPath))
            {
                try
                {
                    // Count all image files, not just PNG
                    var imageExtensions = _imageService.GetSupportedImageExtensions();
                    var allPreviewFiles = new List<string>();
                    foreach (var ext in imageExtensions)
                    {
                        var files = Directory.GetFiles(previewPath, $"*{ext}", SearchOption.AllDirectories);
                        allPreviewFiles.AddRange(files);
                    }
                    analysis.TotalPreviewSize = allPreviewFiles.Sum(f => new FileInfo(f).Length);
                }
                catch (Exception ex)
                {
                    analysis.Warnings.Add($"Could not fully scan preview directory: {ex.Message}");
                    Console.WriteLine($"[Migration] Preview scan warning: {ex.Message}");
                }
            }

            // Count cache
            var cachePath = Path.Combine(resourcesPath, "cache");
            if (Directory.Exists(cachePath))
            {
                try
                {
                    var cacheFiles = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
                    analysis.TotalCacheSize = cacheFiles.Sum(f => new FileInfo(f).Length);
                }
                catch (Exception ex)
                {
                    analysis.Warnings.Add($"Could not fully scan cache directory: {ex.Message}");
                    Console.WriteLine($"[Migration] Cache scan warning: {ex.Message}");
                }
            }

            // Parse configuration if available
            analysis.Configuration = await ParseConfigurationAsync(pythonPath, analysis.ActiveEnvironment);

            // Format sizes for display
            analysis.TotalArchiveSizeFormatted = FormatBytes(analysis.TotalArchiveSize);
            analysis.TotalPreviewSizeFormatted = FormatBytes(analysis.TotalPreviewSize);
            analysis.TotalCacheSizeFormatted = FormatBytes(analysis.TotalCacheSize);

            analysis.IsValid = true;
            Console.WriteLine($"[Migration] Analysis complete: {analysis.TotalMods} mods, {analysis.Environments.Count} environments");
        }
        catch (Exception ex)
        {
            analysis.Errors.Add($"Analysis failed: {ex.Message}");
            Console.WriteLine($"[Migration] Analysis error: {ex.Message}");
        }

        return analysis;
    }

    public async Task<MigrationResult> MigrateAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new MigrationResult();
        var logPath = Path.Combine(_profileContext.ProfilePath, "logs", $"migration_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        try
        {
            Console.WriteLine("[Migration] Starting migration...");
            await LogAsync(logPath, $"Migration started: {DateTime.Now}");
            await LogAsync(logPath, $"Source: {options.SourcePath}");

            // Stage 1: Analyze
            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Analyzing,
                CurrentTask = "Analyzing source...",
                PercentComplete = 0
            });

            var analysis = await AnalyzeSourceAsync(options.SourcePath);
            if (!analysis.IsValid)
            {
                throw new Exception($"Invalid source: {string.Join(", ", analysis.Errors)}");
            }

            // Determine environment to migrate
            var envName = options.EnvironmentName ?? analysis.ActiveEnvironment;
            if (string.IsNullOrEmpty(envName))
            {
                envName = "Default";
            }

            var envPath = Path.Combine(options.SourcePath, "home", envName);

            // If home directory doesn't exist, use the root path for non-metadata operations
            if (!Directory.Exists(Path.Combine(options.SourcePath, "home")))
            {
                await LogAsync(logPath, "WARNING: 'home' directory not found, using root path for metadata");
                envPath = options.SourcePath; // Fallback to root
            }

            await LogAsync(logPath, $"Migrating environment: {envName}");

            // Stage 2: Migrate Metadata
            if (options.MigrateMetadata)
            {
                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.MigratingMetadata,
                    CurrentTask = "Migrating mod metadata...",
                    PercentComplete = 10
                });

                var mods = await MigrateMetadataAsync(envPath, options.SourcePath, logPath);
                result.ModsMigrated = mods.Count;
                await LogAsync(logPath, $"Migrated {mods.Count} mod metadata entries");
            }

            // Stage 3: Copy Archives
            if (options.MigrateArchives)
            {
                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.CopyingArchives,
                    CurrentTask = "Copying mod archives...",
                    PercentComplete = 30
                });

                var copied = await MigrateArchivesAsync(
                    options.SourcePath,
                    options.ArchiveMode,
                    progress,
                    cancellationToken,
                    logPath);
                result.ArchivesCopied = copied;
                await LogAsync(logPath, $"Copied {copied} archives");
            }

            // Stage 4: Convert Classifications (MUST be before thumbnails!)
            if (options.MigrateClassifications)
            {
                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.ConvertingClassifications,
                    CurrentTask = "Converting classification rules...",
                    PercentComplete = 60
                });

                var rules = await MigrateClassificationsAsync(envPath, logPath);
                result.ClassificationRulesCreated = rules;
                await LogAsync(logPath, $"Created {rules} classification rules");
            }

            // Stage 5: Copy Previews and Thumbnails (MUST be after classifications!)
            if (options.MigratePreviews)
            {
                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.CopyingPreviews,
                    CurrentTask = "Copying preview images...",
                    PercentComplete = 70
                });

                var copied = await MigratePreviewsAsync(options.SourcePath, logPath, progress);
                result.PreviewsCopied = copied;
                await LogAsync(logPath, $"Copied {copied} preview images");

                // Migrate thumbnails and associate with existing classification nodes
                var thumbnailsCopied = await MigrateClassificationFoldersAsync(envPath, logPath);
                await LogAsync(logPath, $"Migrated {thumbnailsCopied} classification folders with thumbnails");
            }

            // Stage 6: Convert Configuration
            if (options.MigrateConfiguration && analysis.Configuration != null)
            {
                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.ConvertingConfiguration,
                    CurrentTask = "Converting configuration...",
                    PercentComplete = 90
                });

                await MigrateConfigurationAsync(analysis.Configuration, logPath);
                await LogAsync(logPath, "Configuration migrated");
            }

            // Stage 7: Finalize
            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Finalizing,
                CurrentTask = "Finalizing migration...",
                PercentComplete = 95
            });

            result.Success = true;
            result.Duration = DateTime.Now - startTime;
            await LogAsync(logPath, $"Migration completed in {result.Duration.TotalSeconds:F1}s");

            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Complete,
                CurrentTask = "Migration complete!",
                PercentComplete = 100
            });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            await LogAsync(logPath, $"ERROR: {ex.Message}");
            Console.WriteLine($"[Migration] Error: {ex.Message}");

            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Error,
                ErrorMessage = ex.Message,
                PercentComplete = 0
            });
        }

        result.LogFilePath = logPath;
        return result;
    }

    private async Task<List<PythonModEntry>> MigrateMetadataAsync(
        string envPath,
        string sourcePath,
        string logPath)
    {
        var allMods = new List<PythonModEntry>();
        var modsIndexPath = Path.Combine(envPath, "modsIndex");

        if (!Directory.Exists(modsIndexPath))
        {
            await LogAsync(logPath, "WARNING: modsIndex directory not found");
            return allMods;
        }

        // Parse all JSON index files
        var indexFiles = Directory.GetFiles(modsIndexPath, "index_*.json");
        await LogAsync(logPath, $"Found {indexFiles.Length} index files");

        foreach (var indexFile in indexFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(indexFile);
                var doc = JObject.Parse(json);
                var modsObj = doc["mods"] as JObject;

                if (modsObj == null) continue;

                foreach (var prop in modsObj.Properties())
                {
                    var sha = prop.Name;
                    var modData = prop.Value;

                    var entry = new PythonModEntry
                    {
                        Sha = sha,
                        Object = modData["object"]?.ToString() ?? "Unknown",
                        Type = modData["type"]?.ToString() ?? "7z",
                        Name = modData["name"]?.ToString() ?? "Unknown",
                        Author = modData["author"]?.ToString() ?? "",
                        Grading = modData["grading"]?.ToString() ?? "G",
                        Explain = modData["explain"]?.ToString() ?? "",
                        Tags = modData["tags"]?.ToObject<List<string>>() ?? new List<string>()
                    };

                    // Deduplicate by SHA
                    if (!allMods.Any(m => m.Sha == sha))
                    {
                        allMods.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                await LogAsync(logPath, $"WARNING: Failed to parse {Path.GetFileName(indexFile)}: {ex.Message}");
            }
        }

        // Insert into database
        foreach (var entry in allMods)
        {
            try
            {
                // Use GetOrCreateModAsync for idempotent migration
                var createRequest = new CreateModRequest
                {
                    SHA = entry.Sha,
                    Category = entry.Object,
                    Name = entry.Name,
                    Author = entry.Author,
                    Description = entry.Explain,
                    Type = entry.Type,
                    Grading = entry.Grading,
                    Tags = entry.Tags,
                    IsLoaded = false, // User will load manually
                    IsAvailable = File.Exists(Path.Combine(sourcePath, "resources", "mods", entry.Sha)),
                    ThumbnailPath = null // Will be generated later
                    // Note: Preview paths are dynamically scanned from previews/{SHA}/ folder
                };

                var mod = await _modManagementService.GetOrCreateModAsync(entry.Sha, createRequest);
                if (mod == null)
                {
                    await LogAsync(logPath, $"Skipping duplicate: {entry.Name} ({entry.Sha})");
                    continue;
                }
            }
            catch (Exception ex)
            {
                await LogAsync(logPath, $"ERROR inserting mod {entry.Name}: {ex.Message}");
            }
        }

        return allMods;
    }

    private async Task<int> MigrateArchivesAsync(
        string sourcePath,
        ArchiveHandling mode,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken,
        string logPath)
    {
        var sourceDir = Path.Combine(sourcePath, "resources", "mods");
        var destDir = Path.Combine(_profileContext.ProfilePath, "mods");

        if (!Directory.Exists(sourceDir))
        {
            await LogAsync(logPath, "WARNING: Source mods directory not found");
            return 0;
        }

        Directory.CreateDirectory(destDir);

        var files = Directory.GetFiles(sourceDir);
        var copied = 0;
        var total = files.Length;

        foreach (var sourceFile in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(sourceFile);
            var destFile = Path.Combine(destDir, fileName);

            try
            {
                switch (mode)
                {
                    case ArchiveHandling.Copy:
                        if (!File.Exists(destFile))
                        {
                            File.Copy(sourceFile, destFile);
                            copied++;
                        }
                        break;

                    case ArchiveHandling.Move:
                        if (!File.Exists(destFile))
                        {
                            File.Move(sourceFile, destFile);
                            copied++;
                        }
                        break;

                    case ArchiveHandling.Link:
                        // TODO: Implement symbolic link creation
                        throw new NotImplementedException("Symbolic links not yet implemented");
                }

                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.CopyingArchives,
                    CurrentTask = $"Copying {fileName}...",
                    ProcessedItems = copied,
                    TotalItems = total,
                    PercentComplete = 30 + (30 * copied / total)
                });
            }
            catch (Exception ex)
            {
                await LogAsync(logPath, $"ERROR copying {fileName}: {ex.Message}");
            }
        }

        return copied;
    }

    private async Task<int> MigratePreviewsAsync(string sourcePath, string logPath, IProgress<MigrationProgress>? progress = null)
    {
        var sourceDir = Path.Combine(sourcePath, "resources", "preview");
        var destDir = Path.Combine(_profileContext.ProfilePath, "previews");

        if (!Directory.Exists(sourceDir))
        {
            await LogAsync(logPath, "WARNING: Source preview directory not found");
            return 0;
        }

        Directory.CreateDirectory(destDir);

        // Get all supported image extensions
        var imageExtensions = _imageService.GetSupportedImageExtensions();
        var allDirectFiles = new List<string>();
        var allSubfolderFiles = new List<string>();

        // Priority 1: Direct image files named by SHA (e.g., preview/ABC123.png)
        foreach (var ext in imageExtensions)
        {
            var pattern = $"*{ext}";
            var files = Directory.GetFiles(sourceDir, pattern, SearchOption.TopDirectoryOnly);
            allDirectFiles.AddRange(files);
        }

        // Priority 2: Image files in SHA-named folders (e.g., preview/ABC123/preview1.png)
        foreach (var ext in imageExtensions)
        {
            var pattern = $"*{ext}";
            var files = Directory.GetFiles(sourceDir, pattern, SearchOption.AllDirectories)
                .Except(allDirectFiles);
            allSubfolderFiles.AddRange(files);
        }

        var directFiles = allDirectFiles;
        var subfolderFiles = allSubfolderFiles;
        var totalFiles = directFiles.Count + subfolderFiles.Count;

        var copied = 0;

        // Process direct files first (higher priority)
        // These are files like preview/ABC123.png which should go to previews/ABC123/preview1.png
        foreach (var sourceFile in directFiles)
        {
            var fileName = Path.GetFileName(sourceFile);
            var sha = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);

            // Create per-mod preview folder
            var modPreviewFolder = Path.Combine(destDir, sha);
            Directory.CreateDirectory(modPreviewFolder);

            var destFile = Path.Combine(modPreviewFolder, $"preview1{ext}");

            try
            {
                if (!File.Exists(destFile))
                {
                    File.Copy(sourceFile, destFile);
                    copied++;
                }

                // Note: Preview paths are no longer stored in database
                // Previews are scanned dynamically from previews/{SHA}/ folder
                // Migration copies files to correct location and they will be discovered automatically

                // Report progress
                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.CopyingPreviews,
                    CurrentTask = $"Copying preview for {sha}...",
                    ProcessedItems = copied,
                    TotalItems = totalFiles,
                    PercentComplete = 60 + (20 * copied / Math.Max(1, totalFiles))
                });
            }
            catch (Exception ex)
            {
                await LogAsync(logPath, $"ERROR copying preview for {sha}: {ex.Message}");
            }
        }

        // Process subfolder files (e.g., ABC123/preview1.png -> previews/ABC123/preview1.png)
        foreach (var sourceFile in subfolderFiles)
        {
            try
            {
                var sha = Path.GetFileName(Path.GetDirectoryName(sourceFile));
                var fileName = Path.GetFileName(sourceFile);

                // Create per-mod preview folder
                var modPreviewFolder = Path.Combine(destDir, sha);
                Directory.CreateDirectory(modPreviewFolder);

                var destFile = Path.Combine(modPreviewFolder, fileName);

                if (!File.Exists(destFile))
                {
                    File.Copy(sourceFile, destFile);
                    copied++;
                }

                // Note: Preview paths are no longer stored in database
                // Previews are scanned dynamically from previews/{SHA}/ folder
                // Migration copies files to correct location and they will be discovered automatically

                // Report progress
                progress?.Report(new MigrationProgress
                {
                    Stage = MigrationStage.CopyingPreviews,
                    CurrentTask = $"Copying preview for {sha}...",
                    ProcessedItems = copied,
                    TotalItems = totalFiles,
                    PercentComplete = 60 + (20 * copied / Math.Max(1, totalFiles))
                });
            }
            catch (Exception ex)
            {
                await LogAsync(logPath, $"ERROR copying preview {Path.GetFileName(sourceFile)}: {ex.Message}");
            }
        }

        return copied;
    }

    private async Task<int> MigrateClassificationFoldersAsync(string envPath, string logPath)
    {
        var sourceThumbnailDir = Path.Combine(envPath, "thumbnail");

        if (!Directory.Exists(sourceThumbnailDir))
        {
            await LogAsync(logPath, "WARNING: Character thumbnail directory not found");
            return 0;
        }

        // Copy thumbnails to data/thumbnails/
        var destThumbnailsDir = Path.Combine(_profileContext.ProfilePath, "thumbnails");
        Directory.CreateDirectory(destThumbnailsDir);

        var copiedCount = await CopyDirectoryRecursiveAsync(sourceThumbnailDir, destThumbnailsDir, logPath);
        await LogAsync(logPath, $"Copied {copiedCount} classification thumbnail files");

        // Parse _redirection.ini and associate thumbnails with existing nodes
        var redirectionFile = Path.Combine(sourceThumbnailDir, "_redirection.ini");
        if (File.Exists(redirectionFile))
        {
            try
            {
                // Get statistics first
                var stats = await _thumbnailService.GetRedirectionStatisticsAsync(redirectionFile);
                await LogAsync(logPath, $"_redirection.ini statistics: {stats}");

                // Associate thumbnails with existing classification nodes
                var associatedCount = await _thumbnailService.AssociateThumbnailsAsync(redirectionFile, destThumbnailsDir);
                await LogAsync(logPath, $"Associated {associatedCount} thumbnails with classification nodes");
            }
            catch (Exception ex)
            {
                await LogAsync(logPath, $"ERROR processing _redirection.ini: {ex.Message}");
            }
        }
        else
        {
            await LogAsync(logPath, "WARNING: _redirection.ini not found, skipping thumbnail association");
        }

        return copiedCount;
    }

    private async Task<int> CopyDirectoryRecursiveAsync(string sourceDir, string destDir, string logPath)
    {
        var copiedCount = 0;

        try
        {
            Directory.CreateDirectory(destDir);

            // Copy all files in current directory
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);

                // Skip _redirection.ini as it's Python-specific
                if (fileName == "_redirection.ini")
                    continue;

                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
                copiedCount++;
            }

            // Recursively copy subdirectories
            foreach (var subdir in Directory.GetDirectories(sourceDir))
            {
                var subdirName = Path.GetFileName(subdir);
                var destSubdir = Path.Combine(destDir, subdirName);

                copiedCount += await CopyDirectoryRecursiveAsync(subdir, destSubdir, logPath);
            }
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR copying directory {sourceDir}: {ex.Message}");
        }

        return copiedCount;
    }

    private async Task MigrateConfigurationAsync(PythonConfiguration config, string logPath)
    {
        try
        {
            // Set work directory
            if (!string.IsNullOrEmpty(config.GamePath))
            {
                var workDir = Path.GetDirectoryName(config.GamePath);
                if (!string.IsNullOrEmpty(workDir))
                {
                    await _configService.SetWorkDirectoryAsync(workDir);
                }
            }

            // Store migration metadata
            await _configService.SetValueAsync("migratedFrom", "python");
            await _configService.SetValueAsync("migrationDate", DateTime.Now.ToString("O"));

            // Store UUID for tracking
            if (!string.IsNullOrEmpty(config.Uuid))
            {
                await _configService.SetValueAsync("uuid", config.Uuid);
            }

            // Store OCD settings
            if (config.Ocd != null)
            {
                await _configService.SetValueAsync("ocd.windowName", config.Ocd.WindowName);
                await _configService.SetValueAsync("ocd.width", config.Ocd.Width);
                await _configService.SetValueAsync("ocd.height", config.Ocd.Height);
            }

            await _configService.SaveAsync();
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR migrating configuration: {ex.Message}");
        }
    }

    private async Task<int> MigrateClassificationsAsync(string envPath, string logPath)
    {
        var classDir = Path.Combine(envPath, "classification");
        if (!Directory.Exists(classDir))
        {
            await LogAsync(logPath, "WARNING: classification directory not found");
            return 0;
        }

        var rulesPath = Path.Combine(_profileContext.ProfilePath, "auto_detection_rules.json");
        var rules = new List<ModAutoDetectionRule>();
        int totalNodesCreated = 0;

        try
        {
            var files = Directory.GetFiles(classDir);
            await LogAsync(logPath, $"Found {files.Length} classification files");

            // Process each classification file
            foreach (var file in files)
            {
                var categoryName = Path.GetFileName(file);
                var lines = await File.ReadAllLinesAsync(file);

                await LogAsync(logPath, $"Processing classification '{categoryName}' with {lines.Length} entries");

                // Create parent classification node for the category (file name)
                // Use simple ID instead of path-based ID for flexibility
                var parentNodeId = categoryName;
                var parentNode = new ClassificationNode
                {
                    Id = parentNodeId,
                    Name = categoryName,
                    ParentId = null, // Root level node
                    Thumbnail = null,
                    Priority = 100,
                    Description = $"Category: {categoryName}",
                    Children = new List<ClassificationNode>()
                };

                // Check if parent node already exists
                if (!await _classificationRepository.ExistsAsync(parentNodeId))
                {
                    await _classificationRepository.InsertAsync(parentNode);
                    totalNodesCreated++;
                    await LogAsync(logPath, $"Created parent classification node: {categoryName}");
                }

                // Create child classification nodes for each object in the file
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var category = line.Trim();

                    // Create child node for this object
                    // Use simple object name as ID for flexibility (node can be moved later)
                    var childNodeId = category;
                    var childNode = new ClassificationNode
                    {
                        Id = childNodeId,
                        Name = category,
                        ParentId = parentNodeId, // Link to parent category
                        Thumbnail = null,
                        Priority = 50,
                        Description = $"Object: {category}",
                        Children = new List<ClassificationNode>()
                    };

                    // Check if child node already exists
                    if (!await _classificationRepository.ExistsAsync(childNodeId))
                    {
                        await _classificationRepository.InsertAsync(childNode);
                        totalNodesCreated++;

                        // Verify mods exist for this category
                        await VerifyModsForCategoryAsync(category, logPath);
                    }

                    // Create auto-detection rules (legacy support)
                    rules.Add(new ModAutoDetectionRule
                    {
                        Name = $"{category} ({categoryName})",
                        Pattern = $"*{category}*",
                        Category = category,
                        Priority = 100
                    });
                }
            }

            // Save legacy auto-detection rules for backward compatibility
            var json = JsonConvert.SerializeObject(rules, Formatting.Indented);
            await File.WriteAllTextAsync(rulesPath, json);

            await LogAsync(logPath, $"Created {totalNodesCreated} classification nodes total");
            return totalNodesCreated;
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR migrating classifications: {ex.Message}");
            await LogAsync(logPath, $"Stack trace: {ex.StackTrace}");
            return 0;
        }
    }

    /// <summary>
    /// Verify mods exist for a specific category (object name)
    /// Mods are linked via their Category field, not through tags
    /// </summary>
    private async Task VerifyModsForCategoryAsync(
        string category,
        string logPath)
    {
        try
        {
            // Find all mods with this object name
            var mods = await _repository.GetByCategoryAsync(category);

            if (mods.Count == 0)
            {
                await LogAsync(logPath, $"INFO: No mods found for object '{category}'");
                return;
            }

            // Mods are already linked via their Category field
            // No need to add classification tags - Category field contains the object name
            await LogAsync(logPath, $"Found {mods.Count} mod(s) for object '{category}' (linked via Category field)");
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR linking mods for object '{category}': {ex.Message}");
        }
    }

    private async Task<PythonConfiguration?> ParseConfigurationAsync(string pythonPath, string envName)
    {
        try
        {
            var config = new PythonConfiguration();

            // Parse local configuration
            var localConfigPath = Path.Combine(pythonPath, "local", "configuration");
            if (File.Exists(localConfigPath))
            {
                var json = await File.ReadAllTextAsync(localConfigPath);
                var doc = JObject.Parse(json);

                config.StyleTheme = doc["style_theme"]?.ToString();
                config.Uuid = doc["uuid"]?.ToString();

                if (doc["main_window_position_x"] != null)
                {
                    config.WindowPosition = new WindowPosition
                    {
                        X = doc["main_window_position_x"]?.ToObject<int>() ?? 0,
                        Y = doc["main_window_position_y"]?.ToObject<int>() ?? 0,
                        Width = doc["main_window_position_width"]?.ToObject<int>() ?? 1200,
                        Height = doc["main_window_position_height"]?.ToObject<int>() ?? 800
                    };
                }

                if (doc["ocd_window_name"] != null)
                {
                    config.Ocd = new OcdSettings
                    {
                        WindowName = doc["ocd_window_name"]?.ToString(),
                        Width = doc["ocd_window_width"]?.ToObject<int>() ?? 1920,
                        Height = doc["ocd_window_height"]?.ToObject<int>() ?? 1080
                    };
                }
            }

            // Parse environment configuration
            if (!string.IsNullOrEmpty(envName))
            {
                var envConfigPath = Path.Combine(pythonPath, "home", envName, "configuration");
                if (File.Exists(envConfigPath))
                {
                    var json = await File.ReadAllTextAsync(envConfigPath);
                    var doc = JObject.Parse(json);

                    config.GamePath = doc["GamePath"]?.ToString();
                    config.GameLaunchArgument = doc["game_launch_argument"]?.ToString();
                }
            }

            return config;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ValidateMigrationAsync(string pythonPath, string reactDataPath)
    {
        // TODO: Implement validation logic
        await Task.CompletedTask;
        return true;
    }

    public async Task<string?> AutoDetectPythonInstallationAsync()
    {
        // Common installation locations
        var searchPaths = new[]
        {
            @"E:\Mods\Endfield MOD",
            @"D:\Mods\Endfield MOD",
            @"C:\Mods\Endfield MOD",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mods", "Endfield MOD")
        };

        foreach (var path in searchPaths)
        {
            if (Directory.Exists(path))
            {
                var analysis = await AnalyzeSourceAsync(path);
                if (analysis.IsValid)
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";

        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private async Task LogAsync(string logPath, string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            await File.AppendAllTextAsync(logPath, logMessage + Environment.NewLine);
            Console.WriteLine($"[Migration] {message}");
        }
        catch
        {
            // Ignore logging errors
        }
    }

}
