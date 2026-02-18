using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Settings.Services;

/// <summary>
/// Service for managing generic JSON settings files
/// Stores files in data/settings/ directory
/// Thread-safe with file locking
/// </summary>
public class SettingsFileService : ISettingsFileService
{
    private readonly string _settingsDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsFileService(string dataPath)
    {
        _settingsDirectory = Path.Combine(dataPath, "settings");

        // Ensure settings directory exists
        if (!Directory.Exists(_settingsDirectory))
        {
            Directory.CreateDirectory(_settingsDirectory);
            Console.WriteLine($"[SettingsFileService] Created settings directory: {_settingsDirectory}");
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
                Console.WriteLine($"[SettingsFileService] Settings file not found: {filename}");
                return null;
            }

            Console.WriteLine($"[SettingsFileService] Reading settings file: {filename}");
            var content = await File.ReadAllTextAsync(filePath);

            // Validate it's valid JSON before returning
            try
            {
                JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[SettingsFileService] Invalid JSON in file {filename}: {ex.Message}");
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
            Console.WriteLine($"[SettingsFileService] Saving settings file: {filename}");

            // Write to temp file first, then move (atomic operation)
            var tempPath = filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, jsonContent);
            File.Move(tempPath, filePath, overwrite: true);

            Console.WriteLine($"[SettingsFileService] Settings file saved: {filename}");
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
                Console.WriteLine($"[SettingsFileService] Settings file deleted: {filename}");
            }
            else
            {
                Console.WriteLine($"[SettingsFileService] Settings file not found for deletion: {filename}");
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

            Console.WriteLine($"[SettingsFileService] Found {files.Length} settings files");
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

        // Prevent overriding global_settings.json through this API
        if (filename.Equals("global_settings", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Cannot access global_settings through this API");
        }
    }
}
