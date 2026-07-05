namespace D3dxSkinManager.Modules.Mod;

/// <summary>
/// On-disk naming conventions for mod cache folders. A disabled cache keeps the mod's files on
/// disk but renamed <c>DISABLED-{id}</c> so the importer runtime skips it (see
/// filesystem-operation-serialization.md). Use these helpers instead of scattering the literal.
/// </summary>
public static class ModConventions
{
    public const string DisabledCachePrefix = "DISABLED-";

    /// <summary>True when a cache folder name is the disabled form (<c>DISABLED-{id}</c>).</summary>
    public static bool IsDisabledCacheName(string? folderName) =>
        folderName?.StartsWith(DisabledCachePrefix, StringComparison.Ordinal) == true;

    /// <summary>The mod id for a cache folder name, stripping the DISABLED- prefix when present.</summary>
    public static string CacheNameToModId(string folderName) =>
        IsDisabledCacheName(folderName) ? folderName[DisabledCachePrefix.Length..] : folderName;
}
