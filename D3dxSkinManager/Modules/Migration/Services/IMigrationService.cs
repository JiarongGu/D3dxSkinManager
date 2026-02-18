using System;
using System.Threading;
using System.Threading.Tasks;

using D3dxSkinManager.Modules.Migration.Models;
namespace D3dxSkinManager.Modules.Migration.Services;

/// <summary>
/// Service for migrating data from Python d3dxSkinManage to React version
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Analyze Python installation and return migration analysis
    /// </summary>
    Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath);

    /// <summary>
    /// Perform migration with specified options
    /// </summary>
    /// <param name="options">Migration options</param>
    /// <param name="progress">Progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<MigrationResult> MigrateAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate migration by comparing source and destination
    /// </summary>
    Task<bool> ValidateMigrationAsync(string pythonPath, string reactDataPath);

    /// <summary>
    /// Auto-detect Python installation path
    /// </summary>
    Task<string?> AutoDetectPythonInstallationAsync();
}
