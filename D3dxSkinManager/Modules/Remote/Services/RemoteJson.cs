using System.Text.Json;
using System.Text.Json.Serialization;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for the Remote module's config persistence — centralizes
/// what was duplicated across the source/library/tag-label stores + repositories (drift risk: someone
/// flipping CamelCase in one copy but not the others). All variants are camelCase + case-insensitive
/// (matching the on-disk res/data JSON AND the SQLite mirror columns). Pick by write target:
///   • <see cref="Compact"/> — single-line JSON for SQLite blob columns / non-indented stores.
///   • <see cref="Pretty"/>  — human-editable file JSON (indented + tolerant read: comments, trailing commas).
///   • <see cref="Sparse"/>  — compact + omit nulls, for the resolved/diffed source overlay.
/// Instances are shared + treated read-only (STJ freezes options on first use — never mutate).
/// </summary>
internal static class RemoteJson
{
    /// <summary>Compact single-line camelCase (SQLite blobs, non-indented stores).</summary>
    public static readonly JsonSerializerOptions Compact = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    /// <summary>Compact + omit null-valued properties (the sparse source overlay / diff output).</summary>
    public static readonly JsonSerializerOptions Sparse = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Human-editable file JSON: indented + tolerant read (skip comments, allow trailing commas).</summary>
    public static readonly JsonSerializerOptions Pretty = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
