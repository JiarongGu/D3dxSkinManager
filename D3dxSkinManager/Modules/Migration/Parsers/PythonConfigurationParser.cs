using D3dxSkinManager.Modules.Migration.Models;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Migration.Parsers;

/// <summary>
/// Parser for Python d3dxSkinManage configuration files
/// Extracts configuration from local/configuration and home/{env}/configuration
/// </summary>
public interface IPythonConfigurationParser
{
    /// <summary>
    /// Parse Python configuration from installation directory
    /// </summary>
    /// <param name="pythonPath">Path to Python installation</param>
    /// <param name="envName">Environment name (e.g., "Default", "Endfield")</param>
    /// <returns>Parsed configuration or null if parsing fails</returns>
    Task<PythonConfiguration?> ParseAsync(string pythonPath, string envName);
}

/// <summary>
/// Implementation of Python configuration parser
/// Reads JSON configuration files from Python installation
/// </summary>
public class PythonConfigurationParser : IPythonConfigurationParser
{
    public async Task<PythonConfiguration?> ParseAsync(string pythonPath, string envName)
    {
        try
        {
            var config = new PythonConfiguration();

            // Parse local configuration (global settings)
            var localConfigPath = Path.Combine(pythonPath, "local", "configuration");
            if (File.Exists(localConfigPath))
            {
                var json = await File.ReadAllTextAsync(localConfigPath).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                config.StyleTheme = root.TryGetProperty("style_theme", out var styleTheme) ? styleTheme.GetString() : null;
                config.Uuid = root.TryGetProperty("uuid", out var uuid) ? uuid.GetString() : null;

                // Parse main window position
                if (root.TryGetProperty("main_window_position_x", out _))
                {
                    config.WindowPosition = new PythonWindowPosition
                    {
                        X = root.TryGetProperty("main_window_position_x", out var x) ? x.GetInt32() : 0,
                        Y = root.TryGetProperty("main_window_position_y", out var y) ? y.GetInt32() : 0,
                        Width = root.TryGetProperty("main_window_position_width", out var width) ? width.GetInt32() : 1200,
                        Height = root.TryGetProperty("main_window_position_height", out var height) ? height.GetInt32() : 1080
                    };
                }

                // Parse OCD (On-screen display) settings
                if (root.TryGetProperty("ocd_window_name", out _))
                {
                    config.Ocd = new PythonOcdSettings
                    {
                        WindowName = root.TryGetProperty("ocd_window_name", out var windowName) ? windowName.GetString() : null,
                        Width = root.TryGetProperty("ocd_window_width", out var ocdWidth) ? ocdWidth.GetInt32() : 1920,
                        Height = root.TryGetProperty("ocd_window_height", out var ocdHeight) ? ocdHeight.GetInt32() : 1080
                    };
                }
            }

            // Parse environment configuration (per-environment settings)
            if (!string.IsNullOrEmpty(envName))
            {
                var envConfigPath = Path.Combine(pythonPath, "home", envName, "configuration");
                if (File.Exists(envConfigPath))
                {
                    var json = await File.ReadAllTextAsync(envConfigPath).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    config.GamePath = root.TryGetProperty("GamePath", out var gamePath) ? gamePath.GetString() : null;
                    config.GameLaunchArgument = root.TryGetProperty("game_launch_argument", out var gameLaunchArg) ? gameLaunchArg.GetString() : null;
                }
            }

            return config;
        }
        catch
        {
            // Return null if parsing fails - non-critical
            return null;
        }
    }
}
