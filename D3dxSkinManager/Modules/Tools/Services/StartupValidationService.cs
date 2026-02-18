using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using D3dxSkinManager.Modules.Tools.Models;

namespace D3dxSkinManager.Modules.Tools.Services;

/// <summary>
/// Service for performing startup validation checks
/// Validates directories, 3DMigoto installation, configuration, database, and components
/// </summary>
public class StartupValidationService : IStartupValidationService
{
    private readonly string _dataPath;
    private readonly IConfigurationService _configService;

    public StartupValidationService(string dataPath, IConfigurationService configService)
    {
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<StartupValidationReport> ValidateStartupAsync()
    {
        Console.WriteLine("[StartupValidation] Starting validation checks...");

        var report = new StartupValidationReport
        {
            Results = new List<ValidationResult>()
        };

        // Run all validation checks
        report.Results.Add(await ValidateDirectoriesAsync());
        report.Results.Add(await ValidateDatabaseAsync());
        report.Results.Add(await ValidateConfigurationAsync());
        report.Results.Add(await Validate3DMigotoAsync());
        report.Results.Add(await ValidateComponentsAsync());

        // Count severity levels
        report.ErrorCount = report.Results.Count(r => r.Severity == ValidationSeverity.Error && !r.IsValid);
        report.WarningCount = report.Results.Count(r => r.Severity == ValidationSeverity.Warning && !r.IsValid);
        report.InfoCount = report.Results.Count(r => r.Severity == ValidationSeverity.Info);

        // Overall validity (no errors allowed, warnings are okay)
        report.IsValid = report.ErrorCount == 0;

        if (report.IsValid)
        {
            Console.WriteLine($"[StartupValidation] �?All checks passed ({report.WarningCount} warnings)");
        }
        else
        {
            Console.WriteLine($"[StartupValidation] �?Validation failed: {report.ErrorCount} errors, {report.WarningCount} warnings");
        }

        return report;
    }

    public async Task<ValidationResult> ValidateDirectoriesAsync()
    {
        var result = new ValidationResult
        {
            CheckName = "Required Directories",
            Severity = ValidationSeverity.Error
        };

        try
        {
            var requiredDirectories = new[]
            {
                Path.Combine(_dataPath, "mods"),
                Path.Combine(_dataPath, "work_mods"),
                Path.Combine(_dataPath, "thumbnails"),
                Path.Combine(_dataPath, "previews")
            };

            var missingDirectories = new List<string>();

            foreach (var dir in requiredDirectories)
            {
                if (!Directory.Exists(dir))
                {
                    // Attempt to create directory
                    try
                    {
                        Directory.CreateDirectory(dir);
                        Console.WriteLine($"[StartupValidation] Created directory: {dir}");
                    }
                    catch (Exception ex)
                    {
                        missingDirectories.Add($"{dir} ({ex.Message})");
                    }
                }
            }

            if (missingDirectories.Count > 0)
            {
                result.IsValid = false;
                result.Message = $"Failed to create required directories: {string.Join(", ", missingDirectories)}";
            }
            else
            {
                result.IsValid = true;
                result.Message = "All required directories exist and are accessible";
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"Directory validation failed: {ex.Message}";
        }

        return await Task.FromResult(result);
    }

    public async Task<ValidationResult> Validate3DMigotoAsync()
    {
        var result = new ValidationResult
        {
            CheckName = "3DMigoto Installation",
            Severity = ValidationSeverity.Warning // Warning, not error - app can work without 3DMigoto
        };

        try
        {
            var workDirectory = _configService.GetWorkDirectory();

            if (string.IsNullOrEmpty(workDirectory))
            {
                result.IsValid = false;
                result.Message = "3DMigoto work directory not configured. Please set it in Settings.";
                return result;
            }

            if (!Directory.Exists(workDirectory))
            {
                result.IsValid = false;
                result.Message = $"3DMigoto work directory does not exist: {workDirectory}";
                return result;
            }

            // Check for 3DMigoto loader executable
            var loaderNames = new[]
            {
                "3DMigotoLoader.exe",
                "3DMigoto Loader.exe",
                "d3dx.exe"
            };

            var foundLoader = loaderNames.FirstOrDefault(name =>
                File.Exists(Path.Combine(workDirectory, name)));

            if (foundLoader == null)
            {
                // Check for any .exe file
                var exeFiles = Directory.GetFiles(workDirectory, "*.exe");
                if (exeFiles.Length > 0)
                {
                    result.IsValid = true;
                    result.Message = $"3DMigoto work directory configured. Found executable: {Path.GetFileName(exeFiles[0])}";
                }
                else
                {
                    result.IsValid = false;
                    result.Message = "3DMigoto work directory does not contain a loader executable. Please configure the correct directory.";
                }
            }
            else
            {
                result.IsValid = true;
                result.Message = $"3DMigoto installation valid. Loader: {foundLoader}";
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"3DMigoto validation failed: {ex.Message}";
        }

        return await Task.FromResult(result);
    }

    public async Task<ValidationResult> ValidateConfigurationAsync()
    {
        var result = new ValidationResult
        {
            CheckName = "Configuration File",
            Severity = ValidationSeverity.Warning
        };

        try
        {
            var configPath = Path.Combine(_dataPath, "config.json");

            if (!File.Exists(configPath))
            {
                result.IsValid = true;
                result.Severity = ValidationSeverity.Info;
                result.Message = "Configuration file does not exist. Default configuration will be used.";
                return result;
            }

            // Try to parse configuration file
            var configJson = await File.ReadAllTextAsync(configPath);
            var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(configJson);

            if (config == null)
            {
                result.IsValid = false;
                result.Message = "Configuration file is invalid JSON";
                return result;
            }

            result.IsValid = true;
            result.Message = "Configuration file is valid";
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"Configuration validation failed: {ex.Message}";
        }

        return result;
    }

    public async Task<ValidationResult> ValidateDatabaseAsync()
    {
        var result = new ValidationResult
        {
            CheckName = "Database",
            Severity = ValidationSeverity.Error
        };

        try
        {
            var dbPath = Path.Combine(_dataPath, "mods.db");

            // Check if database directory is writable
            var dbDirectory = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory!);
            }

            // Try to open database connection
            var connectionString = $"Data Source={dbPath}";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            // Verify we can query the database
            var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Mods'";
            var tableExists = await command.ExecuteScalarAsync();

            if (tableExists == null)
            {
                // Database exists but Mods table doesn't - it will be created on first use
                result.IsValid = true;
                result.Severity = ValidationSeverity.Info;
                result.Message = "Database file exists. Mods table will be created on first use.";
            }
            else
            {
                // Check if we can query the table
                command.CommandText = "SELECT COUNT(*) FROM Mods";
                var count = await command.ExecuteScalarAsync();
                result.IsValid = true;
                result.Message = $"Database is valid and accessible ({count} mods)";
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"Database validation failed: {ex.Message}";
        }

        return result;
    }

    public async Task<ValidationResult> ValidateComponentsAsync()
    {
        var result = new ValidationResult
        {
            CheckName = "Required Components",
            Severity = ValidationSeverity.Info
        };

        try
        {
            var components = new List<string>();

            // Check SharpCompress (archive extraction)
            components.Add("�?SharpCompress (archive extraction): Built-in");

            // Check .NET runtime
            var runtimeVersion = Environment.Version;
            components.Add($"�?.NET Runtime: {runtimeVersion}");

            // Check Windows Forms (file dialogs)
            try
            {
                var _ = typeof(System.Windows.Forms.Form);
                components.Add("�?Windows Forms (file dialogs): Available");
            }
            catch
            {
                components.Add("�?Windows Forms: Not available");
            }

            result.IsValid = true;
            result.Message = string.Join("\n", components);
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Message = $"Component validation failed: {ex.Message}";
        }

        return await Task.FromResult(result);
    }
}
