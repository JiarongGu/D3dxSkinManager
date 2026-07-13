using System.IO;
using System.Linq;

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

    /// <summary>True when an on-disk folder in the mods/cache directory is NOT a mod and must be
    /// SKIPPED by folder→mod enumeration: a dot-prefixed folder (".claude", ".git", ".vs") that
    /// contains no 3DMigoto ".ini". Without this it is treated as a mod id, then disabled/renamed and
    /// its load fails (the ".claude → DISABLED.claude" bug, 2026-07-13). A dot-folder that DOES contain
    /// an ".ini" is NOT skipped — 3DMigoto would load it, a separate concern.</summary>
    public static bool IsIgnoredNonModFolder(string dirPath)
    {
        var name = Path.GetFileName(dirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(name) || name[0] != '.') return false;
        try { return !Directory.EnumerateFiles(dirPath, "*.ini", SearchOption.AllDirectories).Any(); }
        catch { return true; } // unreadable dot-folder → not a mod, skip it
    }
}
