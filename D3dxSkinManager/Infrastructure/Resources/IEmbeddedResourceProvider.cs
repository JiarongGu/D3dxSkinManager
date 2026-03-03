namespace D3dxSkinManager.Infrastructure.Resources;

/// <summary>
/// Provides access to embedded resources (web assets, language files, etc.)
/// Supports both file-based (development) and embedded (production) modes
/// </summary>
public interface IEmbeddedResourceProvider
{
    /// <summary>
    /// Get a resource stream by its virtual path
    /// </summary>
    /// <param name="virtualPath">Virtual path (e.g., "wwwroot/index.html", "data/languages/en.json")</param>
    /// <returns>Stream if found, null if not found</returns>
    Stream? GetResourceStream(string virtualPath);

    /// <summary>
    /// Get resource content as string (for text files)
    /// </summary>
    /// <param name="virtualPath">Virtual path</param>
    /// <returns>Content string if found, null if not found</returns>
    string? GetResourceString(string virtualPath);

    /// <summary>
    /// Check if a resource exists
    /// </summary>
    /// <param name="virtualPath">Virtual path</param>
    /// <returns>True if resource exists</returns>
    bool ResourceExists(string virtualPath);

    /// <summary>
    /// Get all available resource paths (for debugging/listing)
    /// </summary>
    /// <returns>List of all embedded resource paths</returns>
    IEnumerable<string> GetAllResourcePaths();

    /// <summary>
    /// Check if running in embedded mode (production) or file-based mode (development)
    /// </summary>
    bool IsEmbeddedMode { get; }
}
