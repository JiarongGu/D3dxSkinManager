namespace D3dxSkinManager.Modules.Plugin;

/// <summary>
/// The plugin API contract version the HOST implements. A pack declares the SDK contract version it was
/// built against (manifest <c>sdkContractVersion</c>); the host installs it only when the MAJOR versions
/// match (a major bump = breaking contract change). Lives in Core so both the app and the SDK see it.
/// Bump the major on a breaking change to the Core plugin interfaces.
/// </summary>
public static class PluginContract
{
    // 2.0 (2026-07-13): breaking change — IImageReviewPlugin.ReviewImageAsync returns a bool VERDICT
    // (the plugin owns its own threshold) instead of a double confidence. Packs built on 1.x are gated
    // out (major mismatch) until rebuilt against this contract.
    public const string Version = "2.0";

    /// <summary>Major version number ("1.2" → 1). Used for the compatibility gate.</summary>
    public static int Major(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return -1;
        var dot = version.IndexOf('.');
        var head = dot >= 0 ? version[..dot] : version;
        return int.TryParse(head, out var m) ? m : -1;
    }

    /// <summary>A pack built against <paramref name="packContractVersion"/> is compatible when its major
    /// equals the host's. An unspecified pack version is treated as compatible (legacy packs).</summary>
    public static bool IsCompatible(string? packContractVersion)
    {
        if (string.IsNullOrWhiteSpace(packContractVersion)) return true;
        return Major(packContractVersion) == Major(Version);
    }
}
