namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Provides access to a specific profile's profile-scoped service provider WITHOUT switching the active
/// profile. Backed by <c>ProfileServiceRouter</c> (Infrastructure), which builds + migrates + caches a
/// DI provider per profile. Lets a GLOBAL service (e.g. <c>ProfileBundleService</c> behind the global
/// <c>ProfileFacade</c>) read/write another profile's profile-scoped data (categories, remote libraries)
/// — the cross-profile read (export a chosen profile) + write (import into a freshly-created profile).
/// </summary>
public interface IProfileServiceProvider
{
    /// <summary>
    /// Get (creating + migrating on first use, then cached) the scoped <see cref="IServiceProvider"/>
    /// for a profile. Resolve profile-scoped services from it, e.g.
    /// <c>GetProfileServices(id).GetRequiredService&lt;ICategoryService&gt;()</c>.
    /// </summary>
    IServiceProvider GetProfileServices(string profileId);
}

/// <summary>
/// Settable holder for the concrete <see cref="IProfileServiceProvider"/>. Registered as a Core
/// singleton so global services can inject <see cref="IProfileServiceProvider"/> at construction time,
/// then <see cref="Bind"/> is called in <c>ApplicationHost</c> once the router exists. The router is
/// <c>new</c>'d AFTER the root container is built (it needs that provider), so it cannot be a normal DI
/// registration — this accessor is the indirection that bridges the two. Calling before <see cref="Bind"/>
/// throws (a bundle op can only run after the app has loaded, so the router is always bound by then).
/// </summary>
public sealed class ProfileServiceProviderAccessor : IProfileServiceProvider
{
    private volatile IProfileServiceProvider? _inner;

    /// <summary>Bind the concrete provider (the router) once it has been created at startup.</summary>
    public void Bind(IProfileServiceProvider inner) => _inner = inner;

    public IServiceProvider GetProfileServices(string profileId)
        => (_inner ?? throw new InvalidOperationException(
                "IProfileServiceProvider was used before the profile service router was initialized"))
            .GetProfileServices(profileId);
}
