namespace D3dxSkinManager.Modules.Core.Cleanup;

/// <summary>
/// One APP-LEVEL startup cleanup/migration step. Register implementations in
/// <c>CoreServiceExtensions</c> (multiple <c>AddSingleton&lt;IStartupCleanupStep, …&gt;</c>) and the
/// runner executes them in registration order — this is THE central place for "sweep a leftover /
/// migrate a legacy file on startup" work; don't scatter one-off cleanup into bootstrap code.
/// (Profile-level lazy upgrades — seed field fills, legacy-binding upgrades, plaintext-cookie
/// re-protection — stay in their stores, which upgrade on first read.)
/// </summary>
public interface IStartupCleanupStep
{
    /// <summary>Short name for logs (e.g. "managed-downloads").</summary>
    string Name { get; }

    Task RunAsync();
}
