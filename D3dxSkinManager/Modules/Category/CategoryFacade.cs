using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Category.Services;

namespace D3dxSkinManager.Modules.Category;

/// <summary>
/// Interface for Category Management facade
/// Handles: GET_CATEGORY_TREE, CREATE_CATEGORY, etc.
/// Module: CATEGORY
/// </summary>
public interface ICategoryFacade : IModuleFacade
{
    Task<List<CategoryInfo>> GetCategoryTreeAsync();
}

/// <summary>
/// Facade for Category management operations
/// Routes IPC messages for category tree operations
/// Module name: CATEGORY
/// </summary>
public class CategoryFacade : BaseFacade, ICategoryFacade
{
    protected override string ModuleName => "CategoryFacade";

    private readonly ICategoryService _categoryService;
    private readonly IPayloadHelper _payloadHelper;

    public CategoryFacade(
        ICategoryService categoryService,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _categoryService = categoryService;
        _payloadHelper = payloadHelper;
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "GET_CATEGORY_TREE" => await GetCategoryTreeAsync(),
            "CREATE_CATEGORY" => await CreateCategoryAsync(request),
            "UPDATE_CATEGORY" => await UpdateCategoryAsync(request),
            "DELETE_CATEGORY" => await DeleteCategoryAsync(request),
            "MOVE_CATEGORY" => await MoveCategoryAsync(request),
            "BATCH_MOVE_CATEGORIES" => await BatchMoveCategoriesAsync(request),
            "CHECK_CATEGORY_EXISTS" => await CheckCategoryExistsAsync(request),
            "CHECK_CATEGORY_NAME_EXISTS" => await CheckCategoryNameExistsAsync(request),
            _ => throw new InvalidOperationException($"Unknown request type: {request.Type}")
        };
    }

    /// <summary>
    /// Get the Category tree from SQLite database
    /// Returns hierarchical tree structure with thumbnails
    /// </summary>
    public async Task<List<CategoryInfo>> GetCategoryTreeAsync()
    {
        return await _categoryService.GetCategoryTreeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Create a new Category category with auto-generated GUID
    /// </summary>
    private async Task<CategoryInfo?> CreateCategoryAsync(IpcRequest request)
    {
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var parentId = _payloadHelper.GetOptionalValue<string>(request.Payload, "parentId");
        var priorityValue = _payloadHelper.GetOptionalValue<int?>(request.Payload, "priority");
        var priority = priorityValue ?? 100;
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var thumbnail = _payloadHelper.GetOptionalValue<string>(request.Payload, "thumbnail");

        // Generate GUID first - this allows other services to have the ID before DB creation
        var categoryId = Guid.NewGuid().ToString();

        var category = await _categoryService.CreateAsync(
            categoryId,
            name,
            parentId,
            priority,
            description,
            thumbnail
        ).ConfigureAwait(false);

        if (category == null)
        {
            throw new InvalidOperationException($"Category with name '{name}' already exists at this level. Please use a different name.");
        }

        // Note: CategoryService.CreateAsync already calls InvalidateTreeCache() which emits CATEGORY_TREE_UPDATED

        return category;
    }

    /// <summary>
    /// Update a Category category's name, description, and thumbnail
    /// </summary>
    private async Task<bool> UpdateCategoryAsync(IpcRequest request)
    {
        var categoryId = _payloadHelper.GetRequiredValue<string>(request.Payload, "categoryId");
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var description = _payloadHelper.GetOptionalValue<string>(request.Payload, "description");
        var thumbnail = _payloadHelper.GetOptionalValue<string>(request.Payload, "thumbnail");

        var success = await _categoryService.UpdateCategoryAsync(
            categoryId,
            name,
            description,
            thumbnail
        ).ConfigureAwait(false);

        // Note: CategoryService.UpdateCategoryAsync already calls InvalidateTreeCache() which emits CATEGORY_TREE_UPDATED

        return success;
    }

    /// <summary>
    /// Delete a Category category and all its children
    /// </summary>
    private async Task<bool> DeleteCategoryAsync(IpcRequest request)
    {
        var categoryId = _payloadHelper.GetRequiredValue<string>(request.Payload, "categoryId");

        var success = await _categoryService.DeleteAsync(categoryId).ConfigureAwait(false);

        // Note: CategoryService.DeleteAsync already calls InvalidateTreeCache() which emits CATEGORY_TREE_UPDATED

        return success;
    }

    /// <summary>
    /// Move a Category category to a new parent
    /// </summary>
    private async Task<bool> MoveCategoryAsync(IpcRequest request)
    {
        var categoryId = _payloadHelper.GetRequiredValue<string>(request.Payload, "categoryId");
        var newParentId = _payloadHelper.GetOptionalValue<string>(request.Payload, "newParentId");
        var dropPosition = _payloadHelper.GetOptionalValue<int?>(request.Payload, "dropPosition");

        var success = await _categoryService.UpdateParentAsync(
            categoryId,
            newParentId,
            dropPosition
        ).ConfigureAwait(false);

        // Note: CategoryService.UpdateParentAsync already calls InvalidateTreeCache() which emits CATEGORY_TREE_UPDATED

        return success;
    }

    /// <summary>
    /// Move multiple categories to a new parent (batch operation)
    /// </summary>
    private async Task<bool> BatchMoveCategoriesAsync(IpcRequest request)
    {
        var categoryIds = _payloadHelper.GetRequiredValue<List<string>>(request.Payload, "categoryIds");
        var newParentId = _payloadHelper.GetOptionalValue<string>(request.Payload, "newParentId");

        var success = await _categoryService.BatchUpdateParentAsync(
            categoryIds,
            newParentId
        ).ConfigureAwait(false);

        return success;
    }

    /// <summary>
    /// Check if a Category category exists by categoryId (GUID)
    /// </summary>
    private async Task<bool> CheckCategoryExistsAsync(IpcRequest request)
    {
        var categoryId = _payloadHelper.GetRequiredValue<string>(request.Payload, "categoryId");
        return await _categoryService.ExistsAsync(categoryId).ConfigureAwait(false);
    }

    /// <summary>
    /// Check if a Category name already exists in the database
    /// Used for form validation to prevent duplicate names
    /// </summary>
    private async Task<bool> CheckCategoryNameExistsAsync(IpcRequest request)
    {
        var name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name");
        var excludeCategoryId = _payloadHelper.GetOptionalValue<string>(request.Payload, "excludeCategoryId");

        var category = await _categoryService.GetByNameAsync(name).ConfigureAwait(false);

        // If no category found with this name, it doesn't exist
        if (category == null) return false;

        // If we're editing a category, exclude it from the check
        if (!string.IsNullOrEmpty(excludeCategoryId) && category.Id == excludeCategoryId)
        {
            return false; // Same category, not a duplicate
        }

        return true; // Name exists
    }
}
