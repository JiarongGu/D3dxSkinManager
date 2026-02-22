using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Launch.Models;
using D3dxSkinManager.Modules.System.Services;
using D3dxSkinManager.Modules.Tools.Services;

namespace D3dxSkinManager.Modules.Launch.Services;

/// <summary>
/// Interface for 3DMigoto version management service
/// </summary>
public interface I3DMigotoService
{
    /// <summary>
    /// Get list of available 3DMigoto versions
    /// </summary>
    Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync();

    /// <summary>
    /// Get the currently deployed version name
    /// </summary>
    Task<string?> GetCurrentVersionAsync();

    /// <summary>
    /// Deploy a 3DMigoto version to the work directory
    /// </summary>
    Task<DeploymentResult> DeployVersionAsync(string versionName);

    /// <summary>
    /// Launch 3DMigoto loader
    /// </summary>
    Task<bool> LaunchAsync();
}

/// <summary>
/// Service for managing 3DMigoto versions
/// Handles version listing, deployment, and launching
/// </summary>
public class D3DMigotoService : I3DMigotoService
{
    private readonly IProfilePathService _profilePathService;
    private readonly IFileHelper _fileService;
    private readonly IConfigurationService _configService;
    private readonly ISystemProcessService _processService;
    private readonly IArchiveHelper _archiveService;
    private readonly ILogHelper _logger;

    public D3DMigotoService(
        IProfilePathService profilePathSerivce,
        IFileHelper fileService,
        IConfigurationService configService,
        ISystemProcessService processService,
        IArchiveHelper archiveService,
        ILogHelper logger)
    {
        _profilePathService = profilePathSerivce;
        _fileService = fileService;
        _configService = configService;
        _processService = processService;
        _archiveService = archiveService;
        _logger = logger;

        // Ensure versions directory exists
        if (!Directory.Exists(_profilePathService.TdMigotoDirectory))
        {
            Directory.CreateDirectory(_profilePathService.TdMigotoDirectory);
            _logger.Info($"Created versions directory: {_profilePathService.TdMigotoDirectory}", "D3DMigotoService");
        }
    }

    public async Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync()
    {
        var versions = new List<D3DMigotoVersion>();

        try
        {
            if (!Directory.Exists(_profilePathService.TdMigotoDirectory))
            {
                return versions;
            }

            var currentVersion = await GetCurrentVersionAsync().ConfigureAwait(false);

            // Get all archive files in the versions directory
            var archiveExtensions = new[] { ".zip", ".7z", ".rar" };
            var files = Directory.GetFiles(_profilePathService.TdMigotoDirectory)
                .Where(f => archiveExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => Path.GetFileName(f));

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var name = Path.GetFileNameWithoutExtension(file);

                versions.Add(new D3DMigotoVersion
                {
                    Name = name,
                    FilePath = file,
                    SizeBytes = fileInfo.Length,
                    SizeFormatted = FileUtilities.FormatBytes(fileInfo.Length),
                    IsDeployed = name.Equals(currentVersion, StringComparison.OrdinalIgnoreCase)
                });
            }

            _logger.Info($"Found {versions.Count} available versions", "D3DMigotoService");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error getting versions: {ex.Message}", "D3DMigotoService", ex);
        }

        return versions;
    }

    public async Task<string?> GetCurrentVersionAsync()
    {
        try
        {
            if (!File.Exists(ActiveVersionPath))
            {
                return null;
            }

            var version = await File.ReadAllTextAsync(ActiveVersionPath).ConfigureAwait(false);
            return version.Trim();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error getting current version: {ex.Message}", "D3DMigotoService", ex);
            return null;
        }
    }

    public async Task<DeploymentResult> DeployVersionAsync(string versionName)
    {
        try
        {
            _logger.Info($"Deploying version: {versionName}", "D3DMigotoService");

            // Get work directory
            var workDirectory = _configService.GetWorkDirectory();
            if (string.IsNullOrEmpty(workDirectory))
            {
                return new DeploymentResult
                {
                    Success = false,
                    Error = "Work directory not configured. Please set it in Settings."
                };
            }

            // Find the version archive
            var archiveExtensions = new[] { ".zip", ".7z", ".rar" };
            var archivePath = archiveExtensions
                .Select(ext => Path.Combine(_profilePathService.TdMigotoDirectory, $"{versionName}{ext}"))
                .FirstOrDefault(File.Exists);

            if (archivePath == null)
            {
                return new DeploymentResult
                {
                    Success = false,
                    Error = $"Version archive not found: {versionName}"
                };
            }

            // Create work directory if it doesn't exist
            if (!Directory.Exists(workDirectory))
            {
                Directory.CreateDirectory(workDirectory);
            }

            // Clear existing files in work directory (but keep user data like .ini files)
            var filesToKeep = new[] { ".ini", ".txt" };
            foreach (var file in Directory.GetFiles(workDirectory))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!filesToKeep.Contains(ext))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Could not delete {file}: {ex.Message}", "D3DMigotoService");
                    }
                }
            }

            // Extract the version archive
            _logger.Info($"Extracting {archivePath} to {workDirectory}", "D3DMigotoService");
            var result = await _archiveService.ExtractArchiveAsync(archivePath, workDirectory).ConfigureAwait(false);

            if (!result.Success)
            {
                return new DeploymentResult
                {
                    Success = false,
                    Error = "Failed to extract 3DMigoto archive"
                };
            }

            // Save current version
            await File.WriteAllTextAsync(ActiveVersionPath, versionName).ConfigureAwait(false);

            _logger.Info($"Successfully deployed version: {versionName}", "D3DMigotoService");

            return new DeploymentResult
            {
                Success = true,
                Message = $"3DMigoto {versionName} deployed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Error deploying version: {ex.Message}", "D3DMigotoService", ex);
            return new DeploymentResult
            {
                Success = false,
                Error = $"Deployment failed: {ex.Message}"
            };
        }
    }

    private string ActiveVersionPath => Path.Combine(_profilePathService.ProfilePath, "current_version.txt");

    public async Task<bool> LaunchAsync()
    {
        try
        {
            var workDirectory = _configService.GetWorkDirectory();
            if (string.IsNullOrEmpty(workDirectory) || !Directory.Exists(workDirectory))
            {
                _logger.Warn("Work directory not configured or does not exist", "D3DMigotoService");
                return false;
            }

            // Look for 3DMigoto loader executable
            var loaderNames = new[]
            {
                "3DMigotoLoader.exe",
                "3DMigoto Loader.exe",
                "d3dx.exe"
            };

            string? loaderPath = null;
            foreach (var name in loaderNames)
            {
                var path = Path.Combine(workDirectory, name);
                if (File.Exists(path))
                {
                    loaderPath = path;
                    break;
                }
            }

            // If no known loader found, try any .exe file
            if (loaderPath == null)
            {
                var exeFiles = Directory.GetFiles(workDirectory, "*.exe");
                if (exeFiles.Length > 0)
                {
                    loaderPath = exeFiles[0];
                }
            }

            if (loaderPath == null)
            {
                _logger.Warn("No 3DMigoto loader found in work directory", "D3DMigotoService");
                return false;
            }

            _logger.Info($"Launching: {loaderPath}", "D3DMigotoService");
            await _processService.LaunchProcessAsync(loaderPath, "", Path.GetDirectoryName(loaderPath));

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error launching 3DMigoto: {ex.Message}", "D3DMigotoService", ex);
            return false;
        }
    }

}
