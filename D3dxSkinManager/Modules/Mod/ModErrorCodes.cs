namespace D3dxSkinManager.Modules.Mod;

/// <summary>
/// Error codes for mod operation failures
/// These codes are used by the frontend for i18n translation
/// Frontend maps these to user-friendly messages in the translation files
/// </summary>
public static class ModErrorCodes
{
    // Mod Deletion Errors (DELETE_ prefix)
    public const string DELETE_MOD_NOT_FOUND = "MOD_DELETE_MOD_NOT_FOUND";
    public const string DELETE_CACHE_FAILED = "MOD_DELETE_CACHE_FAILED";
    public const string DELETE_ARCHIVE_FAILED = "MOD_DELETE_ARCHIVE_FAILED";
    public const string DELETE_PREVIEW_FAILED = "MOD_DELETE_PREVIEW_FAILED";
    public const string DELETE_DATABASE_FAILED = "MOD_DELETE_DATABASE_FAILED";
    public const string DELETE_FAILED = "MOD_DELETE_FAILED";

    // Mod Load/Unload Errors
    public const string LOAD_FAILED = "MOD_LOAD_FAILED";
    public const string UNLOAD_FAILED = "MOD_UNLOAD_FAILED";

    // Generic Mod Errors
    public const string UNKNOWN_ERROR = "MOD_UNKNOWN_ERROR";
}
