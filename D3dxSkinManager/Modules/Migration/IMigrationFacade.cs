using System;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Migration.Models;

namespace D3dxSkinManager.Modules.Migration;

/// <summary>
/// Interface for Migration facade
/// Handles: MIGRATION_AUTO_DETECT, MIGRATION_ANALYZE, MIGRATION_START, etc.
/// Prefix: MIGRATION_*
/// </summary>
public interface IMigrationFacade : IModuleFacade
{

    Task<string?> AutoDetectPythonInstallationAsync();
    Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath);
    Task<MigrationResult> StartMigrationAsync(MigrationOptions options, IProgress<MigrationProgress>? progress = null);
    Task<bool> ValidateMigrationAsync(string pythonPath, string reactDataPath);
}
