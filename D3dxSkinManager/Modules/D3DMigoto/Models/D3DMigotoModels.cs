namespace D3dxSkinManager.Modules.D3DMigoto.Models;

/// <summary>
/// Information about a 3DMigoto version
/// </summary>
public class D3DMigotoVersion
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    public bool IsDeployed { get; set; }
}

/// <summary>
/// Result of deploying a 3DMigoto version
/// </summary>
public class DeploymentResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
