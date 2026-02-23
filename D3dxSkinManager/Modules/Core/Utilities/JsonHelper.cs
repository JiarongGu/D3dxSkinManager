using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Helper for consistent JSON serialization across the application
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Default JSON serialization options used throughout the application
    /// Omits null values to align with frontend undefined convention
    /// </summary>
    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serialize an object to JSON string using default options
    /// </summary>
    public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(obj, options ?? DefaultOptions);
    }

    /// <summary>
    /// Deserialize a JSON string to an object using default options
    /// </summary>
    public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
    }

    /// <summary>
    /// Serialize an object to JSON and write to file asynchronously
    /// </summary>
    public static async Task SerializeToFileAsync<T>(string filePath, T obj, JsonSerializerOptions? options = null)
    {
        var json = Serialize(obj, options);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }

    /// <summary>
    /// Read JSON from file and deserialize asynchronously
    /// </summary>
    public static async Task<T?> DeserializeFromFileAsync<T>(string filePath, JsonSerializerOptions? options = null)
    {
        if (!File.Exists(filePath))
        {
            return default;
        }

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        return Deserialize<T>(json, options);
    }
}
