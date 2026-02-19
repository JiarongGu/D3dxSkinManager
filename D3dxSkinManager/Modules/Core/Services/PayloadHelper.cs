using System;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for extracting values from message payloads
/// Testable alternative to static PayloadHelper methods
/// </summary>
public interface IPayloadHelper
{
    /// <summary>
    /// Get required value from payload, throws if missing or invalid
    /// </summary>
    T GetRequiredValue<T>(JsonElement? payload, string key);

    /// <summary>
    /// Get optional value from payload, returns default if missing
    /// </summary>
    T? GetOptionalValue<T>(JsonElement? payload, string key);
}

/// <summary>
/// Helper for extracting values from message payloads
/// </summary>
public class PayloadHelper : IPayloadHelper
{
    public T GetRequiredValue<T>(JsonElement? payload, string key)
    {
        if (payload == null || !payload.Value.TryGetProperty(key, out var value))
        {
            throw new ArgumentException($"Missing required payload parameter: {key}");
        }

        try
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)value.GetString()!;
            }
            return JsonSerializer.Deserialize<T>(value.GetRawText())!;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid payload parameter '{key}': {ex.Message}", ex);
        }
    }

    public T? GetOptionalValue<T>(JsonElement? payload, string key)
    {
        if (payload == null || !payload.Value.TryGetProperty(key, out var value))
        {
            return default;
        }

        try
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)value.GetString()!;
            }
            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }
        catch
        {
            return default;
        }
    }
}
