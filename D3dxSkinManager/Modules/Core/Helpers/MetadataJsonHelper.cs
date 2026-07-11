using global::System.Text.Json.Nodes;

namespace D3dxSkinManager.Modules.Core.Helpers;

/// <summary>
/// Helpers for the mod Metadata JSON blob (<c>ModEntity.Metadata</c>): parse-or-empty and merge a
/// single top-level key while PRESERVING every other field. Any service that stamps a namespaced
/// section (<c>fix.*</c>, <c>remote.*</c>, …) into the shared metadata string should use this instead
/// of hand-rolling the parse/merge (which was copy-pasted across ModFixService + RemoteImportService).
/// Never throws — invalid/absent JSON yields an empty object.
/// (<c>global::System</c> qualifier avoids the <c>Modules.System</c> namespace collision.)
/// </summary>
public static class MetadataJsonHelper
{
    /// <summary>Parse a Metadata JSON string to a <see cref="JsonObject"/>; returns an empty object on
    /// null/whitespace/invalid JSON (never throws).</summary>
    public static JsonObject ParseOrEmpty(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return new JsonObject();
        try { return JsonNode.Parse(metadata) as JsonObject ?? new JsonObject(); }
        catch { return new JsonObject(); }
    }

    /// <summary>Merge <paramref name="value"/> under <paramref name="key"/> into the Metadata JSON,
    /// preserving all other top-level fields; returns the serialized result.</summary>
    public static string MergeKey(string? metadata, string key, JsonNode value)
    {
        var obj = ParseOrEmpty(metadata);
        obj[key] = value;
        return obj.ToJsonString();
    }
}
