using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Tools.Services;

namespace D3dxSkinManager.Modules.Migration.Steps;

/// <summary>
/// Step 2: Migrate configuration settings
/// Updates current profile configuration from Python installation
/// Uses ConfigurationService for all settings operations (not direct File.WriteAllText!)
/// </summary>
public class MigrationStep2MigrateConfiguration : IMigrationStep
{
    private readonly IConfigurationService _configService;  // ✅ Using service!
    private readonly ILogHelper _logger;

    public int StepNumber => 2;
    public string StepName => "Migrate Configuration";

    public MigrationStep2MigrateConfiguration(
        IConfigurationService configService,
        ILogHelper logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.Options.MigrateConfiguration || context.Analysis?.Configuration == null)
        {
            await LogAsync(context.LogPath, "Step 2: Skipping configuration (disabled or no config found)");
            return;
        }

        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.ConvertingConfiguration,
            CurrentTask = "Migrating configuration...",
            PercentComplete = 15
        });

        await LogAsync(context.LogPath, "Step 2: Migrating configuration settings");

        await MigrateConfigurationAsync(context.Analysis.Configuration, context.LogPath);
        await LogAsync(context.LogPath, "Configuration migrated");
        _logger.Info("Step 2 complete: Configuration migrated", "Migration");
    }

    private async Task MigrateConfigurationAsync(PythonConfiguration config, string logPath)
    {
        try
        {
            // ✅ Use ConfigurationService instead of manual file writes!

            // Set work directory
            if (!string.IsNullOrEmpty(config.GamePath))
            {
                var workDir = Path.GetDirectoryName(config.GamePath);
                if (!string.IsNullOrEmpty(workDir))
                {
                    await _configService.SetWorkDirectoryAsync(workDir);
                    await LogAsync(logPath, $"Set work directory: {workDir}");
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

            // ✅ Save using service (handles JSON serialization, error handling, etc.)
            await _configService.SaveAsync();
            await LogAsync(logPath, "Configuration saved successfully");
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR migrating configuration: {ex.Message}");
        }
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
