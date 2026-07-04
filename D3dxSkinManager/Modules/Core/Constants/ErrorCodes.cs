namespace D3dxSkinManager.Modules.Core.Constants;

/// <summary>
/// Standard error codes for application-wide error handling
/// Frontend should have corresponding error message mappings
/// </summary>
public static class ErrorCodes
{
    // Mod Operation Errors (MOD_*)
    public const string MOD_FOLDER_IN_USE = "MOD_FOLDER_IN_USE";
    public const string MOD_NOT_FOUND = "MOD_NOT_FOUND";
    public const string MOD_EXTRACTION_FAILED = "MOD_EXTRACTION_FAILED";
    public const string MOD_ARCHIVE_UPDATE_FAILED = "MOD_ARCHIVE_UPDATE_FAILED";
    public const string MOD_NO_CACHE = "MOD_NO_CACHE";

    // File Operation Errors (FILE_*)
    public const string FILE_NOT_FOUND = "FILE_NOT_FOUND";
    public const string FILE_ACCESS_DENIED = "FILE_ACCESS_DENIED";

    // Generic Errors
    public const string UNKNOWN_ERROR = "UNKNOWN_ERROR";
}
