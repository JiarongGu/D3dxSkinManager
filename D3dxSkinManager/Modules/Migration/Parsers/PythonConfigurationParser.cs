using System;
using System.IO;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Migration.Models;
using Newtonsoft.Json.Linq;

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
                var json = await File.ReadAllTextAsync(localConfigPath);
                var doc = JObject.Parse(json);

                config.StyleTheme = doc["style_theme"]?.ToString();
                config.Uuid = doc["uuid"]?.ToString();

                // Parse main window position
                if (doc["main_window_position_x"] != null)
                {
                    config.WindowPosition = new PythonWindowPosition
                    {
                        X = doc["main_window_position_x"]?.ToObject<int>() ?? 0,
                        Y = doc["main_window_position_y"]?.ToObject<int>() ?? 0,
                        Width = doc["main_window_position_width"]?.ToObject<int>() ?? 1200,
                        Height = doc["main_window_position_height"]?.ToObject<int>() ?? 1080
                    };
                }

                // Parse OCD (On-screen display) settings
                if (doc["ocd_window_name"] != null)
                {
                    config.Ocd = new PythonOcdSettings
                    {
                        WindowName = doc["ocd_window_name"]?.ToString(),
                        Width = doc["ocd_window_width"]?.ToObject<int>() ?? 1920,
                        Height = doc["ocd_window_height"]?.ToObject<int>() ?? 1080
                    };
                }
            }

            // Parse environment configuration (per-environment settings)
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
            // Return null if parsing fails - non-critical
            return null;
        }
    }
}
