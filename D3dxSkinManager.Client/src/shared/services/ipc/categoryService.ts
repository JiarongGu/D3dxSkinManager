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
   * Find a Category category by ID (local tree search)
   * For validation, use categoryExists() instead which checks the database
   */
  findNodeById(tree: CategoryInfo[], id: string): CategoryInfo | undefined {
    for (const category of tree) {
      if (category.id === id) {
        return category;
      }
      if (category.children.length > 0) {
        const found = this.findNodeById(category.children, id);
        if (found) return found;
      }
    }
    return undefined;
  }

  /**
   * Check if a Category category exists in the database by nodeId (GUID)
   * Returns true if exists, false otherwise
   */
  async categoryExists(profileId: string, categoryId: string): Promise<boolean> {
    return this.sendMessage<boolean>("CHECK_CATEGORY_EXISTS", profileId, {
      categoryId,
    });
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
   * Get all leaf categories from tree
   */
  getAllLeafNodes(tree: CategoryInfo[]): CategoryInfo[] {
    const leaves: CategoryInfo[] = [];

    const traverse = (categories: CategoryInfo[]) => {
      for (const category of categories) {
        if (category.children.length === 0) {
          leaves.push(category);
        } else {
          traverse(category.children);
        }
      }
    };

    traverse(tree);
    return leaves;
  }

  /**
   * Flatten tree to list of all categories
   */
  flattenTree(tree: CategoryInfo[]): CategoryInfo[] {
    const result: CategoryInfo[] = [];

    const traverse = (categories: CategoryInfo[]) => {
      for (const category of categories) {
        result.push(category);
        if (category.children.length > 0) {
          traverse(category.children);
        }
      }
    };

    traverse(tree);
    return result;
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
