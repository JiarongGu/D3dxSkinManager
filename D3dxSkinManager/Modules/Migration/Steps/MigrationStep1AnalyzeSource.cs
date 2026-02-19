using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Parsers;

namespace D3dxSkinManager.Modules.Migration.Steps;

/// <summary>
/// Step 1: Analyze Python installation source
/// Validates structure and counts mods, previews, cache
/// </summary>
public class MigrationStep1AnalyzeSource : IMigrationStep
{
    private readonly IImageService _imageService;
    private readonly IPythonConfigurationParser _configParser;
    private readonly ILogHelper _logger;

    public int StepNumber => 1;
    public string StepName => "Analyze Source";

    public MigrationStep1AnalyzeSource(
        IImageService imageService,
        IPythonConfigurationParser configParser,
        ILogHelper logger)
    {
        _imageService = imageService;
        _configParser = configParser;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.Analyzing,
            CurrentTask = "Analyzing source...",
            PercentComplete = 0
        });

        await LogAsync(context.LogPath, "Step 1: Analyzing Python installation source");

        var analysis = await AnalyzeSourceAsync(context.Options.SourcePath);

        if (!analysis.IsValid)
        {
            throw new Exception($"Invalid source: {string.Join(", ", analysis.Errors)}");
        }

        // Store analysis in context for other steps
        context.Analysis = analysis;

        // Determine environment to migrate
        var envName = context.Options.EnvironmentName ?? analysis.ActiveEnvironment;
        if (string.IsNullOrEmpty(envName))
        {
            envName = "Default";
        }
        context.EnvironmentName = envName;

        var envPath = Path.Combine(context.Options.SourcePath, "home", envName);
        if (!Directory.Exists(Path.Combine(context.Options.SourcePath, "home")))
        {
            await LogAsync(context.LogPath, "WARNING: 'home' directory not found, using root path for metadata");
            envPath = context.Options.SourcePath;
        }
        context.EnvironmentPath = envPath;

        await LogAsync(context.LogPath, $"Environment: {envName}");
        await LogAsync(context.LogPath, $"Found {analysis.TotalMods} mods, {analysis.Environments.Count} environments");

        _logger.Info($"Analysis complete: {analysis.TotalMods} mods", "Migration");
    }

    private async Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath)
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
                    _logger.Warning($"Mods scan warning: {ex.Message}", "Migration");
                }
            }

            // Count preview images
            var previewPath = Path.Combine(resourcesPath, "preview");
            if (Directory.Exists(previewPath))
            {
                try
                {
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
                    _logger.Warning($"Preview scan warning: {ex.Message}", "Migration");
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
                    _logger.Warning($"Cache scan warning: {ex.Message}", "Migration");
                }
            }

            // Parse configuration if available
            analysis.Configuration = await _configParser.ParseAsync(pythonPath, analysis.ActiveEnvironment);

            // Format sizes for display
            analysis.TotalArchiveSizeFormatted = FileUtilities.FormatBytes(analysis.TotalArchiveSize);
            analysis.TotalPreviewSizeFormatted = FileUtilities.FormatBytes(analysis.TotalPreviewSize);
            analysis.TotalCacheSizeFormatted = FileUtilities.FormatBytes(analysis.TotalCacheSize);

            analysis.IsValid = true;
        }
        catch (Exception ex)
        {
            analysis.Errors.Add($"Analysis failed: {ex.Message}");
            _logger.Error($"Analysis error: {ex.Message}", "Migration", ex);
        }

        return analysis;
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
