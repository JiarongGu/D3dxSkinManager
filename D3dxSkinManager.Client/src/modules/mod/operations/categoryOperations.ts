/**
 * Category operations - SINGLE SOURCE OF TRUTH for category update logic
 * Handles complex tree count updates with optimistic UI
 */

import { debounce } from 'lodash-es';

import { useModsStore } from '../store/modsStore';
import { CategoryInfo, CATEGORY_IDS } from '../../../shared/types/category.types';
import { notification } from '../../../shared/utils/notification';
import { categoryService, modService } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { executeWithDelayedLoading } from '../../../shared/utils/delayedLoading';

/**
 * Update mod category
 * Backend will fire MOD_LIST_UPDATED and CATEGORY_TREE_UPDATED events which trigger refresh via ModProvider
 */
export async function updateModCategory(
  profileId: string,
  sha: string,
  categoryId: string,
  onMismatch?: () => void
): Promise<boolean> {
  try {
    // Perform backend operation
    await modService.updateCategory(profileId, sha, categoryId);
    notification.success('Category updated');

    // Backend fires MOD_LIST_UPDATED and CATEGORY_TREE_UPDATED events
    // → ModProvider refreshes mods and category tree automatically
    return true;
  } catch (error: unknown) {
    notification.error('Failed to update category');

    // Refresh tree on error to ensure counts are correct
    if (onMismatch) {
      onMismatch();
    }
    return false;
  }
}

/**
 * Batch update categories for multiple mods using bulk IPC call
 */
export async function batchUpdateCategories(
  profileId: string,
  shas: string[],
  categoryId: string,
  onMismatch?: () => void
): Promise<boolean> {
  try {
    // For batch operations, skip optimistic updates due to complexity
    // Just perform the operation and refresh
    const updatedCount = await modService.batchUpdateCategory(profileId, shas, categoryId);

    if (updatedCount > 0) {
      notification.success(`Updated ${updatedCount} mod(s) category`);
      return true;
    } else {
      notification.error('No mods were updated');
      return false;
    }
  } catch (error: unknown) {
    notification.error('Failed to batch update categories');

    // Refresh tree on error to ensure counts are correct
    if (onMismatch) {
      onMismatch();
    }
    return false;
  }
}

/**
 * Load Category tree
 * Uses delayed loading (100ms) to avoid flicker for fast loads
 */
export async function loadCategoryTree(profileId: string): Promise<void> {
  const { setCategoryLoading, setCategoryTree } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        const tree = await categoryService.getCategoryTree(profileId);
        setCategoryTree(tree);
      },
      setCategoryLoading,
      100
    );
  } catch (error: unknown) {
    handleError(error);
  }
}

/**
 * Internal refresh implementation
 */
async function _refreshCategoryTree(profileId: string): Promise<void> {
  await loadCategoryTree(profileId);
}

/**
 * Refresh Category tree (debounced 10ms to prevent mass IPC hits)
 */
export const refreshCategoryTree = debounce(_refreshCategoryTree, 10);

/**
 * Internal implementation for loading mods by category
 */
async function _loadModsByCategory(
  profileId: string,
  nodeId: string
): Promise<void> {
  const { setCategoryLoading, setMods } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        const mods = await modService.getModsByCategory(profileId, nodeId);
        setMods(mods);
      },
      setCategoryLoading,
      100
    );
  } catch (error: unknown) {
    handleError(error);
  }
}

/**
 * Load mods filtered by Category node (debounced 10ms to prevent mass IPC hits)
 * Uses delayed loading (100ms) to avoid flicker for fast queries
 */
export const loadModsByCategory = debounce(_loadModsByCategory, 10);

/**
 * Load uncategorized mods (no category assigned)
 * Uses delayed loading (100ms) to avoid flicker for fast queries
 */
export async function loadUncategorizedMods(profileId: string): Promise<void> {
  const { setCategoryLoading, setMods } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        const mods = await modService.getUnclassifiedMods(profileId);
        setMods(mods);
      },
      setCategoryLoading,
      100
    );
  } catch (error: unknown) {
    handleError(error);
  }
}

/**
 * Clear Category filter
 */
export function clearCategoryFilter(): void {
  useModsStore.getState().clearCategoryFilter();
}

/**
 * Select Category node and load its mods
 */
export async function selectCategory(
  profileId: string,
  nodeId: string
): Promise<void> {
  const state = useModsStore.getState();

  // Find the node in the tree
  const findNode = (nodes: typeof state.categoryTree): typeof state.selectedCategory => {
    for (const node of nodes) {
      if (node.id === nodeId) return node;
      if (node.children) {
        const found = findNode(node.children);
        if (found) return found;
      }
    }
    return undefined;
  };

  const node = findNode(state.categoryTree);
  state.setSelectedCategory(node);

  // Load mods for this Category
  if (nodeId === CATEGORY_IDS.UNCLASSIFIED) {
    await loadUncategorizedMods(profileId);
  } else {
    await loadModsByCategory(profileId, nodeId);
  }
}
