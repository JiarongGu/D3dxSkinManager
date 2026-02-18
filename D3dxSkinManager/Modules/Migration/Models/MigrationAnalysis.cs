using System;
using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Migration.Models;

/// <summary>
/// Analysis result of Python installation
/// </summary>
public class MigrationAnalysis
{
    public bool IsValid { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public int TotalMods { get; set; }
    public long TotalArchiveSize { get; set; }
    public string TotalArchiveSizeFormatted { get; set; } = "0 B";
    public long TotalPreviewSize { get; set; }
    public string TotalPreviewSizeFormatted { get; set; } = "0 B";
    public long TotalCacheSize { get; set; }
    public string TotalCacheSizeFormatted { get; set; } = "0 B";
    public List<string> Environments { get; set; } = new();
    public string ActiveEnvironment { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public PythonConfiguration? Configuration { get; set; }
}
