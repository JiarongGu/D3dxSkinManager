using System;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Helper for extracting values from message payloads
/// </summary>
public static class PayloadHelper
{
    public static T GetRequiredValue<T>(JsonElement? payload, string key)
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

    public static T? GetOptionalValue<T>(JsonElement? payload, string key)
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

