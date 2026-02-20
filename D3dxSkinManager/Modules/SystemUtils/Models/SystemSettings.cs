using System;
using System.Collections.Generic;

namespace D3dxSkinManager.Modules.SystemUtils.Models;

/// <summary>
/// System-level settings for file operations and system behavior
/// Stored in: data/settings/system.json
/// </summary>
public class SystemSettings
{
    /// <summary>
    /// File dialog path memory by key
    /// Key: rememberPathKey (e.g., "mod-preview-import", "migration_python_install")
    /// Value: Last used directory path (relative to data folder)
    /// </summary>
    public Dictionary<string, string> FileDialogPaths { get; set; } = new();

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
