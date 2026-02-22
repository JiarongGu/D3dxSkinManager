using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Helpers;
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
    private readonly IConfigurationService _configService;  // âœ?Using service!
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

        await LogAsync(context.LogPath, "Step 2: Migrating configuration settings").ConfigureAwait(false);

        await MigrateConfigurationAsync(context.Analysis.Configuration, context.LogPath).ConfigureAwait(false);
        await LogAsync(context.LogPath, "Configuration migrated").ConfigureAwait(false);
        _logger.Info("Step 2 complete: Configuration migrated", "Migration");
    }

    private async Task MigrateConfigurationAsync(PythonConfiguration config, string logPath)
    {
        try
        {
            // âœ?Use ConfigurationService instead of manual file writes!

            // Set work directory
            if (!string.IsNullOrEmpty(config.GamePath))
            {
                var workDir = Path.GetDirectoryName(config.GamePath);
                if (!string.IsNullOrEmpty(workDir))
                {
                    await _configService.SetWorkDirectoryAsync(workDir).ConfigureAwait(false);
                    await LogAsync(logPath, $"Set work directory: {workDir}").ConfigureAwait(false);
                }
            }

            // Store migration metadata
            await _configService.SetValueAsync("migratedFrom", "python").ConfigureAwait(false);
            await _configService.SetValueAsync("migrationDate", DateTime.Now.ToString("O"));

            // Store UUID for tracking
            if (!string.IsNullOrEmpty(config.Uuid))
            {
                await _configService.SetValueAsync("uuid", config.Uuid).ConfigureAwait(false);
            }

            // Store OCD settings
            if (config.Ocd != null)
            {
                await _configService.SetValueAsync("ocd.windowName", config.Ocd.WindowName).ConfigureAwait(false);
                await _configService.SetValueAsync("ocd.width", config.Ocd.Width).ConfigureAwait(false);
                await _configService.SetValueAsync("ocd.height", config.Ocd.Height).ConfigureAwait(false);
            }

            // âœ?Save using service (handles JSON serialization, error handling, etc.)
            await _configService.SaveAsync().ConfigureAwait(false);
            await LogAsync(logPath, "Configuration saved successfully").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR migrating configuration: {ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task LogAsync(string logPath, string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            await File.AppendAllTextAsync(logPath, logMessage + Environment.NewLine).ConfigureAwait(false);
            _logger.Info(message, "Migration");
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
