using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Parsers;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Tools.Services;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Migration.Steps;

/// <summary>
/// Step 3: Migrate classification hierarchy and rules
/// Creates classification nodes that mods will be attached to
/// Uses IPythonClassificationFileParser for parsing (not inline parsing!)
/// Uses ClassificationService for node creation (not direct repository access!)
/// Uses ModAutoDetectionService for rule management
/// </summary>
public class MigrationStep3MigrateClassifications : IMigrationStep
{
    private readonly IProfilePathService _profilePaths;
    private readonly IModRepository _modRepository;
    private readonly IPythonClassificationFileParser _classificationParser;  // ✅ Using parser!
    private readonly IClassificationService _classificationService;  // ✅ Using service, not repository!
    private readonly IModAutoDetectionService _autoDetectionService;  // ✅ Using service!
    private readonly ILogHelper _logger;

    public int StepNumber => 3;
    public string StepName => "Migrate Classifications";

    public MigrationStep3MigrateClassifications(
        IProfilePathService profilePaths,
        IModRepository modRepository,
        IPythonClassificationFileParser classificationParser,
        IClassificationService classificationService,
        IModAutoDetectionService autoDetectionService,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _modRepository = modRepository;
        _classificationParser = classificationParser;
        _classificationService = classificationService;
        _autoDetectionService = autoDetectionService;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.Options.MigrateClassifications)
        {
            await LogAsync(context.LogPath, "Step 3: Skipping classifications (disabled)");
            return;
        }

        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.ConvertingClassifications,
            CurrentTask = "Migrating classifications...",
            PercentComplete = 30
        });

        await LogAsync(context.LogPath, "Step 3: Migrating classification hierarchy");

        var rules = await MigrateClassificationsAsync(context.EnvironmentPath!, context.LogPath);
        context.Result.ClassificationRulesCreated = rules;

        await LogAsync(context.LogPath, $"Created {rules} classification rules");
        _logger.Info($"Step 3 complete: {rules} classification rules created", "Migration");
    }

    private async Task<int> MigrateClassificationsAsync(string envPath, string logPath)
    {
        var classDir = Path.Combine(envPath, "classification");
        if (!Directory.Exists(classDir))
        {
            await LogAsync(logPath, "WARNING: classification directory not found");
            return 0;
        }

        int totalNodesCreated = 0;

        try
        {
            // ✅ Use parser to get classifications (not inline parsing!)
            var classifications = await _classificationParser.ParseAsync(classDir);
            await LogAsync(logPath, $"Found {classifications.Count} classification files");

            // Process each category
            foreach (var (categoryName, objectNames) in classifications)
            {
                await LogAsync(logPath, $"Processing '{categoryName}' with {objectNames.Count} entries");

                // ✅ Use ClassificationService to create parent node (not repository!)
                var parentNodeId = categoryName;
                var parentNode = await _classificationService.CreateNodeAsync(
                    nodeId: parentNodeId,
                    name: categoryName,
                    parentId: null, // Root level
                    priority: 100,
                    description: $"Category: {categoryName}"
                );

                if (parentNode != null)
                {
                    totalNodesCreated++;
                    await LogAsync(logPath, $"Created parent node: {categoryName}");
                }

                // Process child nodes (objects within this category)
                foreach (var objectName in objectNames)
                {
                    var category = objectName;

                    // ✅ Use ClassificationService to create child node
                    var childNodeId = category;
                    var childNode = await _classificationService.CreateNodeAsync(
                        nodeId: childNodeId,
                        name: category,
                        parentId: parentNodeId,
                        priority: 50,
                        description: $"Object: {category}"
                    );

                    if (childNode != null)
                    {
                        totalNodesCreated++;

                        // Verify mods exist for this category (using repository for read-only query)
                        await VerifyModsForCategoryAsync(category, logPath);
                    }

                    // ✅ Use ModAutoDetectionService to add rules (not manual JSON!)
                    _autoDetectionService.AddRule(new ModAutoDetectionRule
                    {
                        Name = $"{category} ({categoryName})",
                        Pattern = $"*{category}*",
                        Category = category,
                        Priority = 100
                    });
                }
            }

            // ✅ Use ModAutoDetectionService to save rules (not manual File.WriteAllText!)
            await _autoDetectionService.SaveRulesAsync(_profilePaths.AutoDetectionRulesPath);

            await LogAsync(logPath, $"Created {totalNodesCreated} classification nodes total");
            return totalNodesCreated;
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR migrating classifications: {ex.Message}");
            await LogAsync(logPath, $"Stack trace: {ex.StackTrace}");
            return 0;
        }
    }

    /// <summary>
    /// Verify mods exist for a specific category
    /// This is a read-only query, so direct repository access is acceptable
    /// For CRUD operations, we use services (ClassificationService, ModManagementService)
    /// </summary>
    private async Task VerifyModsForCategoryAsync(string category, string logPath)
    {
        try
        {
            // Read-only query to ModRepository is acceptable here
            // For creating/updating/deleting mods, use ModManagementService instead
            var mods = await _modRepository.GetByCategoryAsync(category);

            if (mods.Count == 0)
            {
                await LogAsync(logPath, $"INFO: No mods found for object '{category}'");
                return;
            }

            await LogAsync(logPath, $"Found {mods.Count} mod(s) for object '{category}'");
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR linking mods for object '{category}': {ex.Message}");
        }
    }

    private async Task LogAsync(string logPath, string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            await File.AppendAllTextAsync(logPath, logMessage + Environment.NewLine);
            _logger.Info(message, "Migration");
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
