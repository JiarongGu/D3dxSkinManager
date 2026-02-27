using System;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Core.Helpers;

/// <summary>
/// Extracts typed values from IPC message payloads.
/// </summary>
public interface IPayloadHelper
{
    T GetRequiredValue<T>(JsonElement? payload, string key);
    T? GetOptionalValue<T>(JsonElement? payload, string key);
}

public class PayloadHelper : IPayloadHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

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
            return JsonSerializer.Deserialize<T>(value.GetRawText(), JsonOptions)!;
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
            return JsonSerializer.Deserialize<T>(value.GetRawText(), JsonOptions);
        }
        catch
        {
            return default;
        }
    }
}
