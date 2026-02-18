using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Tools.Models;

/// <summary>
/// Result of a single validation check
/// </summary>
public class ValidationResult
{
    public string CheckName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? Message { get; set; }
    public ValidationSeverity Severity { get; set; }
}

/// <summary>
/// Severity level for validation issues
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message, no action required
    /// </summary>
    Info,

    /// <summary>
    /// Warning - application can continue but with reduced functionality
    /// </summary>
    Warning,

    /// <summary>
    /// Error - critical issue that prevents application from functioning properly
    /// </summary>
    Error
}

/// <summary>
/// Overall startup validation result
/// </summary>
public class StartupValidationReport
{
    public bool IsValid { get; set; }
    public List<ValidationResult> Results { get; set; } = new();
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
}
