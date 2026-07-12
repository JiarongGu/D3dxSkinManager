namespace D3dxSkinManager.Plugin.Sdk;

/// <summary>
/// Plugin SDK metadata. The contract version lets the host gate compatibility: a plugin manifest can
/// declare the SDK version it was built against, and the host can reject a plugin built against an
/// incompatible (newer-major) contract instead of crashing on load. Bump <see cref="ContractVersion"/>
/// (major) on a breaking change to the Core contracts.
/// </summary>
public static class PluginSdk
{
    /// <summary>Semantic contract version of the plugin API surface (Core interfaces + DTOs).</summary>
    public const string ContractVersion = "1.0";
}
