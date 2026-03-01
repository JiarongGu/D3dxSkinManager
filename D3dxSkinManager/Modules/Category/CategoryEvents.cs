namespace D3dxSkinManager.Modules.Category;

/// <summary>
/// Category module event type constants.
/// Used with ModuleNames.CATEGORY as the module identifier.
/// Example: EmitAsync(ModuleNames.CATEGORY, CategoryEvents.CATEGORY_TREE_UPDATED, payload)
/// </summary>
public static class CategoryEvents
{
    /// <summary>
    /// Emitted when the category tree structure or counts change
    /// This includes: creating/updating/deleting categories, or when mod categories change
    /// </summary>
    public const string CATEGORY_TREE_UPDATED = "CATEGORY_TREE_UPDATED";
}
