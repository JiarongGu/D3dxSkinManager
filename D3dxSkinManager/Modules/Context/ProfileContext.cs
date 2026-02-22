namespace D3dxSkinManager.Modules.Context;

/// <summary>
/// Interface for profile context service
/// </summary>
public interface IProfileContext
{
    /// <summary>
    /// Get ProfileId of the currently active profile
    /// </summary>
    string ProfileId { get; }

    /// <summary>
    /// Service for managing active profile context
    /// </summary>
}

public class ProfileContext : IProfileContext
{
    private readonly string _profileId;

    public ProfileContext(string profileId)
    {
        _profileId = profileId;
    }

    public string ProfileId => _profileId;
}