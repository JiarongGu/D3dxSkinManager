using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Migration.Models;

/// <summary>
/// Python configuration data
/// </summary>
public class PythonConfiguration
{
    public string? StyleTheme { get; set; }
    public string? Uuid { get; set; }
    public string? GamePath { get; set; }
    public string? GameLaunchArgument { get; set; }
    public WindowPosition? WindowPosition { get; set; }
    public OcdSettings? Ocd { get; set; }
    public Dictionary<string, object> Advanced { get; set; } = new();
}

public class WindowPosition
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class OcdSettings
{
    public string? WindowName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
