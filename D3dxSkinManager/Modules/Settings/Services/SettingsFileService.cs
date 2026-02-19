using D3dxSkinManager.Modules.Core.Services;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Settings.Services;

/// <summary>
/// Service for managing generic JSON settings files
/// Allows frontend to store/retrieve any JSON settings by filename
/// Files are stored in data/settings/ directory
/// </summary>
public interface ISettingsFileService
{
    /// <summary>
    /// Get a settings file by name (without .json extension)
    /// Returns the JSON content as a string, or null if file doesn't exist
    /// </summary>
    Task<string?> GetSettingsFileAsync(string filename);

    /// <summary>
    /// Save a settings file by name (without .json extension)
    /// Content should be valid JSON string
    /// </summary>
    Task SaveSettingsFileAsync(string filename, string jsonContent);

    /// <summary>
    /// Delete a settings file by name (without .json extension)
    /// </summary>
    Task DeleteSettingsFileAsync(string filename);

    /// <summary>
    /// Check if a settings file exists
    /// </summary>
    Task<bool> SettingsFileExistsAsync(string filename);

    /// <summary>
    /// List all settings files (returns filenames without .json extension)
    /// </summary>
    Task<string[]> ListSettingsFilesAsync();
}

/// <summary>
/// Service for managing generic JSON settings files
/// Stores files in data/settings/ directory
/// Thread-safe with file locking
/// </summary>
public class SettingsFileService : ISettingsFileService
{
    private readonly string _settingsDirectory;
    private readonly ILogHelper _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsFileService(IPathHelper pathHelper, ILogHelper logger)
    {
        _settingsDirectory = Path.Combine(pathHelper.BaseDataPath, "settings");
        _logger = logger;

        // Ensure settings directory exists
        if (!Directory.Exists(_settingsDirectory))
        {
            Directory.CreateDirectory(_settingsDirectory);
            _logger.Info($"Created settings directory: {_settingsDirectory}", "SettingsFileService");
        }
    }

    /// <summary>
    /// Get a settings file by name
    /// </summary>
    public async Task<string?> GetSettingsFileAsync(string filename)
    {
        ValidateFilename(filename);

        await _lock.WaitAsync();
        try
        {
            var filePath = GetFilePath(filename);

            if (!File.Exists(filePath))
            {
                _logger.Debug($"Settings file not found: {filename}", "SettingsFileService");
                return null;
            }

            _logger.Debug($"Reading settings file: {filename}", "SettingsFileService");
            var content = await File.ReadAllTextAsync(filePath);

            // Validate it's valid JSON before returning
            try
            {
                JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                _logger.Error($"Invalid JSON in file {filename}: {ex.Message}", "SettingsFileService", ex);
                throw new InvalidOperationException($"Settings file contains invalid JSON: {filename}");
            }

            return content;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Save a settings file
    /// </summary>
    public async Task SaveSettingsFileAsync(string filename, string jsonContent)
    {
        ValidateFilename(filename);

        // Validate JSON before saving
        try
        {
            JsonDocument.Parse(jsonContent);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON content: {ex.Message}");
        }

        await _lock.WaitAsync();
        try
        {
            var filePath = GetFilePath(filename);
            _logger.Debug($"Saving settings file: {filename}", "SettingsFileService");

            // Write to temp file first, then move (atomic operation)
            var tempPath = filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, jsonContent);
            File.Move(tempPath, filePath, overwrite: true);

            _logger.Info($"Settings file saved: {filename}", "SettingsFileService");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Delete a settings file
    /// </summary>
    public async Task DeleteSettingsFileAsync(string filename)
    {
        ValidateFilename(filename);

        await _lock.WaitAsync();
        try
        {
            var filePath = GetFilePath(filename);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.Info($"Settings file deleted: {filename}", "SettingsFileService");
            }
            else
            {
                _logger.Debug($"Settings file not found for deletion: {filename}", "SettingsFileService");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Check if a settings file exists
    /// </summary>
    public async Task<bool> SettingsFileExistsAsync(string filename)
    {
        ValidateFilename(filename);

        await _lock.WaitAsync();
        try
        {
            var filePath = GetFilePath(filename);
            return File.Exists(filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// List all settings files
    /// </summary>
    public async Task<string[]> ListSettingsFilesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!Directory.Exists(_settingsDirectory))
            {
                return Array.Empty<string>();
            }

            var files = Directory.GetFiles(_settingsDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();

            _logger.Debug($"Found {files.Length} settings files", "SettingsFileService");
            return files!;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get full file path for a settings file
    /// </summary>
    private string GetFilePath(string filename)
    {
        return Path.Combine(_settingsDirectory, $"{filename}.json");
    }

    /// <summary>
    /// Validate filename is safe (no path traversal)
    /// </summary>
    private void ValidateFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            throw new ArgumentException("Filename cannot be empty");
        }

        // Check for invalid characters and path traversal attempts
        var invalidChars = Path.GetInvalidFileNameChars();
        if (filename.Any(c => invalidChars.Contains(c)) ||
            filename.Contains("..") ||
            filename.Contains("/") ||
            filename.Contains("\\"))
        {
            throw new ArgumentException($"Invalid filename: {filename}");
        }

        // Prevent overriding global.json through this API
        if (filename.Equals("global_settings", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Cannot access global_settings through this API");
        }
    }
}
