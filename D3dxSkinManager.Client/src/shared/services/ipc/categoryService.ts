import { BaseModuleService } from "../baseModuleService";
import type { CategoryInfo } from "../../types/category.types";

/**
 * Service for managing character Category tree
 * Communicates with backend CategoryService via IPC
 */
export class CategoryService extends BaseModuleService {
  constructor() {
    super("CATEGORY");
  }

  /**
   * Get the full Category tree from SQLite database
   * Returns hierarchical structure with thumbnails
   */
  async getCategoryTree(profileId: string): Promise<CategoryInfo[]> {
    return this.sendMessage<CategoryInfo[]>("GET_CATEGORY_TREE", profileId);
  }

  /**
   * Check if a Category name already exists in the database (case-insensitive)
   * Returns true if exists, false otherwise
   * Use this for form validation to prevent duplicate names
   * @param profileId - The profile ID
   * @param name - The Category name to check
   * @param excludeCategoryId - Optional category ID to exclude from check (for edit validation)
   */
  async nameExists(
    profileId: string,
    name: string,
    excludeCategoryId?: string,
  ): Promise<boolean> {
    return this.sendMessage<boolean>("CHECK_CATEGORY_NAME_EXISTS", profileId, {
      name,
      excludeCategoryId,
    });
  }

  /**
   * Create a new Category category with auto-generated GUID
   * @param profileId - The profile ID
   * @param name - The display name of the category
   * @param parentId - The parent category ID (undefined for root level)
   * @param priority - Priority for sorting (default 100)
   * @param description - Optional description
   * @param thumbnail - Optional thumbnail path
   */
  async createCategory(
    profileId: string,
    name: string,
    parentId?: string,
    priority?: number,
    description?: string,
    thumbnail?: string,
  ): Promise<CategoryInfo | undefined> {
    return this.sendMessage<CategoryInfo | undefined>(
      "CREATE_CATEGORY",
      profileId,
      {
        name,
        parentId,
        priority: priority || 100,
        description,
        thumbnail,
      },
    );
  }

  /**
   * Move a Category category to a new parent or position
   */
  async moveCategory(
    profileId: string,
    categoryId: string,
    newParentId: string | undefined,
    dropPosition: number,
  ): Promise<boolean> {
    return this.sendMessage<boolean>("MOVE_CATEGORY", profileId, {
      categoryId,
      newParentId: newParentId,
      dropPosition,
    });
  }

  /**
   * Move multiple categories to a new parent (batch operation)
   */
  async batchMoveCategories(
    profileId: string,
    categoryIds: string[],
    newParentId: string | undefined,
  ): Promise<boolean> {
    return this.sendMessage<boolean>("BATCH_MOVE_CATEGORIES", profileId, {
      categoryIds,
      newParentId,
    });
  }

  /**
   * Update a Category category's name, description, and thumbnail
   */
  async updateCategory(
    profileId: string,
    categoryId: string,
    name: string,
    description?: string,
    thumbnail?: string,
  ): Promise<boolean> {
    return this.sendMessage<boolean>("UPDATE_CATEGORY", profileId, {
      categoryId,
      name,
      description,
      thumbnail,
    });
  }

  /**
   * Delete a Category category
   */
  async deleteCategory(
    profileId: string,
    categoryId: string,
  ): Promise<boolean> {
    return this.sendMessage<boolean>("DELETE_CATEGORY", profileId, {
      categoryId,
    });
  }
}
