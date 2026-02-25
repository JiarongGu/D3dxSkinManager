using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Parsers;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Migration.Steps;

/// <summary>
/// Step 3: Migrate Category hierarchy
/// Creates Category nodes that mods will be attached to
/// Uses IPythonCategoryFileParser for parsing (not inline parsing!)
/// Uses CategoryService for node creation (not direct repository access!)
/// </summary>
public class MigrationStep3MigrateCategories : IMigrationStep
{
    private readonly IProfilePathService _profilePaths;
    private readonly IModRepository _modRepository;
    private readonly IPythonCategoryFileParser _categoryParser;  // Using parser!
    private readonly ICategoryService _categoryService;  // Using service, not repository!
    private readonly ILogHelper _logger;

    public int StepNumber => 3;
    public string StepName => "Migrate Categories";

    public MigrationStep3MigrateCategories(
        IProfilePathService profilePaths,
        IModRepository modRepository,
        IPythonCategoryFileParser CategoryParser,
        ICategoryService categoryService,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _modRepository = modRepository;
        _categoryParser = CategoryParser;
        _categoryService = categoryService;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.Options.MigrateCategories)
        {
            await LogAsync(context.LogPath, "Step 3: Skipping Categories (disabled)");
            return;
        }

        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.ConvertingCategories,
            CurrentTask = "Migrating Categories...",
            PercentComplete = 30
        });

        await LogAsync(context.LogPath, "Step 3: Migrating Category hierarchy").ConfigureAwait(false);

        var rules = await MigrateCategoriesAsync(context, context.EnvironmentPath!, context.LogPath).ConfigureAwait(false);
        context.Result.CategoryRulesCreated = rules;

        await LogAsync(context.LogPath, $"Created {rules} Category rules").ConfigureAwait(false);
        _logger.Info($"Step 3 complete: {rules} Category rules created", "Migration");
    }

    private async Task<int> MigrateCategoriesAsync(MigrationContext context, string envPath, string logPath)
    {
        var classDir = Path.Combine(envPath, "classification");
        if (!Directory.Exists(classDir))
        {
            await LogAsync(logPath, "WARNING: classification directory not found").ConfigureAwait(false);
            return 0;
        }

        int totalNodesCreated = 0;

        // Use parser to get Categories (not inline parsing!)
        var categories = await _categoryParser.ParseAsync(classDir).ConfigureAwait(false);
        await LogAsync(logPath, $"Found {categories.Count} Category files").ConfigureAwait(false);

        // Process each category
        foreach (var (categoryName, categoryNames) in categories)
        {
            _logger.Info($"Processing '{categoryName}' with {categoryNames.Count} entries", "Migration");

            // Check if parent node already exists by name
            var parentNode = await _categoryService.GetByNameAsync(categoryName).ConfigureAwait(false);
            string? parentCategoryId = null;

            if (parentNode != null)
            {
                await LogAsync(logPath, $"Skipping existing parent node: {categoryName} (ID: {parentNode.Id})").ConfigureAwait(false);
                _logger.Info($"Parent node already exists: {categoryName} (ID: {parentNode.Id})", "Migration");
                parentCategoryId = parentNode.Id;
            }
            else
            {
                // Generate GUID upfront - this allows us to have the ID before DB creation
                // Useful if we need to reference this ID in other operations within the same transaction
                parentCategoryId = Guid.NewGuid().ToString();

                // Create new parent node
                parentNode = await _categoryService.CreateAsync(
                    parentCategoryId,
                    categoryName,
                    null, // Root level
                    100,
                    $"Category: {categoryName}"
                ).ConfigureAwait(false);

                if (parentNode != null)
                {
                    totalNodesCreated++;
                    _logger.Info($"Created parent node: {categoryName} (ID: {parentNode.Id})", "Migration");
                }
            }

            // Process child nodes (categories within this category)
            foreach (var childCategoryName in categoryNames)
            {
                // Check if child node already exists by name
                var childNode = await _categoryService.GetByNameAsync(childCategoryName).ConfigureAwait(false);
                string? childCategoryId = null;

                if (childNode != null)
                {
                    await LogAsync(logPath, $"Skipping existing child node: {childCategoryName} (ID: {childNode.Id})").ConfigureAwait(false);
                    _logger.Info($"Child node already exists: {childCategoryName} (ID: {childNode.Id})", "Migration");
                }
                else
                {
                    // Generate GUID upfront - this allows us to have the ID before DB creation
                    // Useful if we need to reference this ID in other operations within the same transaction
                    childCategoryId = Guid.NewGuid().ToString();

                    // Create new child node
                    childNode = await _categoryService.CreateAsync(
                        childCategoryId,
                        childCategoryName,
                        parentNode?.Id, // Use the actual generated parent GUID
                        50,
                        $"Object: {childCategoryName}"
                    ).ConfigureAwait(false);

                    if (childNode != null)
                    {
                        totalNodesCreated++;
                        _logger.Info($"Created child node: {childCategoryName} (ID: {childNode.Id})", "Migration");
                    }
                }

                // Verify mods exist for this category (using repository for read-only query)
                if (childNode != null)
                {
                    await VerifyModsForCategoryAsync(childCategoryName, logPath).ConfigureAwait(false);
                }

                // Update Category node with wildcard pattern for auto-detection (legacy Python project uses wildcard)
                if (childNode != null)
                {
                    await _categoryService.UpdateCategoryAsync(
                        childNode.Id,
                        childNode.Name,
                        childNode.Description,
                        childNode.Thumbnail,
                        "Wildcard",
                        $"*{childCategoryName}*"
                    ).ConfigureAwait(false);
                    await LogAsync(logPath, $"Set auto-detection pattern for {categoryName}/{childCategoryName}: *{childCategoryName}*").ConfigureAwait(false);
                }
            }
        }

        await LogAsync(logPath, $"Created {totalNodesCreated} Category nodes total with auto-detection patterns").ConfigureAwait(false);
        return totalNodesCreated;
    }

    /// <summary>
    /// Verify mods exist for a specific category
    /// This is a read-only query, so direct repository access is acceptable
    /// For CRUD operations, we use services (CategoryService, ModManagementService)
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
                await LogAsync(logPath, $"INFO: No mods found for category '{category}'").ConfigureAwait(false);
                return;
            }

            await LogAsync(logPath, $"Found {mods.Count} mod(s) for category '{category}'");
        }
        catch (Exception ex)
        {
            await LogAsync(logPath, $"ERROR linking mods for category '{category}': {ex.Message}").ConfigureAwait(false);
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
