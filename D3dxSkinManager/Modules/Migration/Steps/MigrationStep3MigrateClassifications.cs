using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Parsers;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

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
    private readonly IPythonClassificationFileParser _classificationParser;  // Using parser!
    private readonly IClassificationService _classificationService;  // Using service, not repository!
    private readonly IModAutoDetectionService _autoDetectionService;  // Using service!
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

        await LogAsync(context.LogPath, "Step 3: Migrating classification hierarchy").ConfigureAwait(false);

        var rules = await MigrateClassificationsAsync(context, context.EnvironmentPath!, context.LogPath).ConfigureAwait(false);
        context.Result.ClassificationRulesCreated = rules;

        await LogAsync(context.LogPath, $"Created {rules} classification rules").ConfigureAwait(false);
        _logger.Info($"Step 3 complete: {rules} classification rules created", "Migration");
    }

    private async Task<int> MigrateClassificationsAsync(MigrationContext context, string envPath, string logPath)
    {
        var classDir = Path.Combine(envPath, "classification");
        if (!Directory.Exists(classDir))
        {
            await LogAsync(logPath, "WARNING: classification directory not found").ConfigureAwait(false);
            return 0;
        }

        int totalNodesCreated = 0;

        // Use parser to get classifications (not inline parsing!)
        var classifications = await _classificationParser.ParseAsync(classDir).ConfigureAwait(false);
        await LogAsync(logPath, $"Found {classifications.Count} classification files").ConfigureAwait(false);

        // Process each category
        foreach (var (categoryName, objectNames) in classifications)
        {
            _logger.Info($"Processing '{categoryName}' with {objectNames.Count} entries", "Migration");

            // Check if parent node already exists by name
            var parentNode = await _classificationService.GetNodeByNameAsync(categoryName).ConfigureAwait(false);

            if (parentNode != null)
            {
                await LogAsync(logPath, $"Skipping existing parent node: {categoryName} (ID: {parentNode.Id})").ConfigureAwait(false);
                _logger.Info($"Parent node already exists: {categoryName} (ID: {parentNode.Id})", "Migration");
            }
            else
            {
                // Create new parent node
                parentNode = await _classificationService.CreateNodeAsync(
                    nodeId: "", // Deprecated - service generates GUID
                    name: categoryName,
                    parentId: null, // Root level
                    priority: 100,
                    description: $"Category: {categoryName}"
                ).ConfigureAwait(false);

                if (parentNode != null)
                {
                    totalNodesCreated++;
                    _logger.Info($"Created parent node: {categoryName} (ID: {parentNode.Id})", "Migration");
                }
            }

            // Process child nodes (objects within this category)
            foreach (var objectName in objectNames)
            {
                var category = objectName;

                // Check if child node already exists by name
                var childNode = await _classificationService.GetNodeByNameAsync(category).ConfigureAwait(false);

                if (childNode != null)
                {
                    await LogAsync(logPath, $"Skipping existing child node: {category} (ID: {childNode.Id})").ConfigureAwait(false);
                    _logger.Info($"Child node already exists: {category} (ID: {childNode.Id})", "Migration");
                }
                else
                {
                    // Create new child node
                    childNode = await _classificationService.CreateNodeAsync(
                        nodeId: "", // Deprecated - service generates GUID
                        name: category,
                        parentId: parentNode?.Id, // Use the actual generated parent GUID
                        priority: 50,
                        description: $"Object: {category}"
                    ).ConfigureAwait(false);

                    if (childNode != null)
                    {
                        totalNodesCreated++;
                        _logger.Info($"Created child node: {category} (ID: {childNode.Id})", "Migration");
                    }
                }

                // Verify mods exist for this category (using repository for read-only query)
                if (childNode != null)
                {
                    await VerifyModsForCategoryAsync(category, logPath).ConfigureAwait(false);
                }

                // Use ModAutoDetectionService to add rules (now using classification ID!)
                if (childNode != null)
                {
                    await _autoDetectionService.AddRuleAsync(new ModAutoDetectionRule
                    {
                        Name = $"{category} ({categoryName})",
                        Pattern = $"*{category}*",
                        Category = childNode.Id, // Use classification ID
                        Priority = 100
                    });
                }
            }
        }

        // Use ModAutoDetectionService to save rules (not manual File.WriteAllText!)
        await _autoDetectionService.SaveRulesAsync(_profilePaths.AutoDetectionRulesPath).ConfigureAwait(false);

        await LogAsync(logPath, $"Created {totalNodesCreated} classification nodes total").ConfigureAwait(false);
        return totalNodesCreated;
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
            var mods = await _modRepository.GetByCategoryAsync(category).ConfigureAwait(false);

            if (mods.Count == 0)
            {
                await LogAsync(logPath, $"INFO: No mods found for object '{category}'").ConfigureAwait(false);
                return;
            }

            await LogAsync(logPath, $"Found {mods.Count} mod(s) for object '{category}'");
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR linking mods for object '{category}': {ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task LogAsync(string logPath, string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            await File.AppendAllTextAsync(logPath, logMessage + Environment.NewLine).ConfigureAwait(false);
            _logger.Info(message, "Migration");
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
