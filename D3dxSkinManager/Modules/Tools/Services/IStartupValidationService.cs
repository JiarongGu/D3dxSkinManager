using System.Threading.Tasks;
using D3dxSkinManager.Modules.Tools.Models;

namespace D3dxSkinManager.Modules.Tools.Services;

/// <summary>
/// Interface for startup validation service
/// </summary>
public interface IStartupValidationService
{
    /// <summary>
    /// Perform all startup validation checks
    /// </summary>
    Task<StartupValidationReport> ValidateStartupAsync();

    /// <summary>
    /// Validate required directories exist and are accessible
    /// </summary>
    Task<ValidationResult> ValidateDirectoriesAsync();

    /// <summary>
    /// Validate 3DMigoto installation and work directory
    /// </summary>
    Task<ValidationResult> Validate3DMigotoAsync();

    /// <summary>
    /// Validate configuration file integrity
    /// </summary>
    Task<ValidationResult> ValidateConfigurationAsync();

    /// <summary>
    /// Validate database file is accessible
    /// </summary>
    Task<ValidationResult> ValidateDatabaseAsync();

    /// <summary>
    /// Validate required components are present
    /// </summary>
    Task<ValidationResult> ValidateComponentsAsync();
}
