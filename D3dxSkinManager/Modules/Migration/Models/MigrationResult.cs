using System;
using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Migration.Models;

/// <summary>
/// Migration result
/// </summary>
public class MigrationResult
{
    public bool Success { get; set; }
    public int ModsMigrated { get; set; }
    public int ArchivesCopied { get; set; }
    public int PreviewsCopied { get; set; }
    public int ClassificationRulesCreated { get; set; }
    public long TotalBytesProcessed { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? LogFilePath { get; set; }
}
