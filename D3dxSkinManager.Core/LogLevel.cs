namespace D3dxSkinManager.Modules.Core.Helpers;

/// <summary>
/// Log levels for categorizing log messages. Matches the frontend LogLevel enum exactly.
/// (Part of the plugin SDK — plugins log via IPluginContext.Log(LogLevel, ...).)
/// </summary>
public enum LogLevel
{
    /// <summary>Verbose - Extremely detailed diagnostic information (high-frequency events like mouse movements, IPC messages)</summary>
    Verbose = 0,

    /// <summary>Debug - Verbose diagnostic information</summary>
    Debug = 1,

    /// <summary>Info - General informational messages</summary>
    Info = 2,

    /// <summary>Warn - Warning messages</summary>
    Warn = 3,

    /// <summary>Error - Error messages</summary>
    Error = 4,

    /// <summary>All - Show everything (special value for filtering)</summary>
    All = -1,

    /// <summary>Off - Disable all logging (special value for filtering)</summary>
    Off = -2
}
