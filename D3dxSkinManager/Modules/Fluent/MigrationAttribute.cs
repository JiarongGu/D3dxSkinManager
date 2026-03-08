namespace D3dxSkinManager.Modules.Fluent;

/// <summary>
/// Attribute to mark a migration class with its version
/// Version format: YYYYMMDDHHmm (e.g., 202603081735 for 2026-03-08 17:35)
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class MigrationAttribute : Attribute
{
    /// <summary>
    /// Migration version as a long timestamp (YYYYMMDDHHmm)
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Optional description of what this migration does
    /// </summary>
    public string? Description { get; set; }

    public MigrationAttribute(long version)
    {
        Version = version;
    }
}
