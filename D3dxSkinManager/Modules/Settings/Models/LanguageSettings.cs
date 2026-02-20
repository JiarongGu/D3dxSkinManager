namespace D3dxSkinManager.Modules.Settings.Models;

/// <summary>
/// Language settings model
/// Stored as translation JSON files in data/languages/
/// </summary>
public class LanguageSettings
{
    /// <summary>
    /// Language code (e.g., "en", "cn")
    /// </summary>
    public string Code { get; set; } = "en";

    /// <summary>
    /// Language display name (e.g., "English", "中文")
    /// </summary>
    public string Name { get; set; } = "English";

    /// <summary>
    /// Translation dictionary (nested JSON structure)
    /// Key-value pairs for all UI strings
    /// </summary>
    public Dictionary<string, object> Translations { get; set; } = new();
}
