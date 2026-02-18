using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.D3DMigoto.Models;
using D3dxSkinManager.Modules.Tools.Services;

namespace D3dxSkinManager.Modules.D3DMigoto.Services;

/// <summary>
/// Service for managing 3DMigoto versions
/// Handles version listing, deployment, and launching
/// </summary>
public class D3DMigotoService : I3DMigotoService
{
    private readonly string _dataPath;
    private readonly string _versionsDirectory;
    private readonly IFileService _fileService;
    private readonly IConfigurationService _configService;
    private readonly IProcessService _processService;

    public D3DMigotoService(
        string dataPath,
        IFileService fileService,
        IConfigurationService configService,
        IProcessService processService)
    {
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));

        _versionsDirectory = Path.Combine(dataPath, "3dmigoto_versions");

        // Ensure versions directory exists
        if (!Directory.Exists(_versionsDirectory))
        {
            Directory.CreateDirectory(_versionsDirectory);
            Console.WriteLine($"[3DMigotoService] Created versions directory: {_versionsDirectory}");
        }
    }

    public async Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync()
    {
        var versions = new List<D3DMigotoVersion>();

        try
        {
            if (!Directory.Exists(_versionsDirectory))
            {
                return versions;
            }

            var currentVersion = await GetCurrentVersionAsync();

            // Get all archive files in the versions directory
            var archiveExtensions = new[] { ".zip", ".7z", ".rar" };
            var files = Directory.GetFiles(_versionsDirectory)
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
                    SizeFormatted = FormatBytes(fileInfo.Length),
                    IsDeployed = name.Equals(currentVersion, StringComparison.OrdinalIgnoreCase)
                });
            }

            Console.WriteLine($"[3DMigotoService] Found {versions.Count} available versions");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[3DMigotoService] Error getting versions: {ex.Message}");
        }

        return versions;
    }

    public async Task<string?> GetCurrentVersionAsync()
    {
        try
        {
            var versionFile = Path.Combine(_dataPath, "current_3dmigoto_version.txt");

            if (!File.Exists(versionFile))
            {
                return null;
            }

            var version = await File.ReadAllTextAsync(versionFile);
            return version.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[3DMigotoService] Error getting current version: {ex.Message}");
            return null;
        }
    }

    public async Task<DeploymentResult> DeployVersionAsync(string versionName)
    {
        try
        {
            Console.WriteLine($"[3DMigotoService] Deploying version: {versionName}");

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
                .Select(ext => Path.Combine(_versionsDirectory, $"{versionName}{ext}"))
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
                        Console.WriteLine($"[3DMigotoService] Warning: Could not delete {file}: {ex.Message}");
                    }
                }
            }

            // Extract the version archive
            Console.WriteLine($"[3DMigotoService] Extracting {archivePath} to {workDirectory}");
            var success = await _fileService.ExtractArchiveAsync(archivePath, workDirectory);

            if (!success)
            {
                return new DeploymentResult
                {
                    Success = false,
                    Error = "Failed to extract 3DMigoto archive"
                };
            }

            // Save current version
            var versionFile = Path.Combine(_dataPath, "current_3dmigoto_version.txt");
            await File.WriteAllTextAsync(versionFile, versionName);

            Console.WriteLine($"[3DMigotoService] Successfully deployed version: {versionName}");

            return new DeploymentResult
            {
                Success = true,
                Message = $"3DMigoto {versionName} deployed successfully"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[3DMigotoService] Error deploying version: {ex.Message}");
            return new DeploymentResult
            {
                Success = false,
                Error = $"Deployment failed: {ex.Message}"
            };
        }
    }

    public async Task<bool> LaunchAsync()
    {
        try
        {
            var workDirectory = _configService.GetWorkDirectory();
            if (string.IsNullOrEmpty(workDirectory) || !Directory.Exists(workDirectory))
            {
                Console.WriteLine("[3DMigotoService] Work directory not configured or does not exist");
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
                Console.WriteLine("[3DMigotoService] No 3DMigoto loader found in work directory");
                return false;
            }

            Console.WriteLine($"[3DMigotoService] Launching: {loaderPath}");
            await _processService.LaunchProcessAsync(loaderPath, "", Path.GetDirectoryName(loaderPath));

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[3DMigotoService] Error launching 3DMigoto: {ex.Message}");
            return false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
