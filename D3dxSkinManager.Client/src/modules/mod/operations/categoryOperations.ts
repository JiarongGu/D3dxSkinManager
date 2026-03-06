/**
 * Category operations - SINGLE SOURCE OF TRUTH for category update logic
 * Handles complex tree count updates with optimistic UI
 */

import { debounce } from 'lodash-es';

import { useModsStore } from '../store/modsStore';
import { CategoryInfo, CATEGORY_IDS } from '../../../shared/types/category.types';
import { ModInfo } from '../../../shared/types/mod.types';
import { notification } from '../../../shared/utils/notification';
import { refreshMods } from './modOperations';
import { categoryService, modService } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { executeWithDelayedLoading } from '../../../shared/utils/delayedLoading';

/**
 * Update mod category with optimistic updates and tree count recalculation
 */
export async function updateModCategory(
  profileId: string,
  sha: string,
  categoryId: string,
  onMismatch?: () => void
): Promise<boolean> {
  const state = useModsStore.getState();

  // Find the mod being updated
  const modBeingUpdated = state.mods.find((m) => m.sha === sha);
  const oldCategoryId = modBeingUpdated?.category;

  // If moving to the same category, do nothing
  if (oldCategoryId === categoryId) {
    return true;
  }

  // Capture current state for rollback
  const currentMods = state.mods;
  const currentTree = state.CategoryTree;

  // Calculate optimistic tree with updated counts
  const optimisticTree = updateTreeCounts(
    currentTree,
    currentMods,
    oldCategoryId,
    categoryId
  );

  // 1. Apply optimistic updates to mod and tree (Zustand automatically updates all slices)
  state.optimisticCategoryUpdate(sha, categoryId);
  state.setCategoryTree(optimisticTree);

  try {
    // 2. Perform backend operation
    await modService.updateCategory(profileId, sha, categoryId);
    notification.success('Category updated');

    // 3. Refresh mods list (category tree will be refreshed by CATEGORY_TREE_UPDATED event)
    // The backend emits MOD.CATEGORY_UPDATED → CategoryEventHandler invalidates cache → emits CATEGORY_TREE_UPDATED
    // ModsProvider listens to CATEGORY_TREE_UPDATED and calls refreshCategoryTree automatically
    await refreshMods(profileId);

    return true;
  } catch (error: unknown) {
    // 4. Revert optimistic update on error
    if (modBeingUpdated) {
      state.optimisticCategoryUpdate(sha, modBeingUpdated.category);
    }
    state.setCategoryTree(currentTree);

    notification.error('Failed to update category');

    // Refresh tree on error to ensure counts are correct
    if (onMismatch) {
      onMismatch();
    }
    return false;
  }
}

/**
 * Helper function to update tree counts when moving a mod between categories
 * Logic: -1 from old category, +1 to new category UNLESS new is ancestor of old
 *
 * This is the SINGLE SOURCE OF TRUTH for tree count calculation
 */
function updateTreeCounts(
  tree: CategoryInfo[],
  mods: ModInfo[],
  oldCategory: string | undefined,
  newCategory: string
): CategoryInfo[] {
  // Check if newCategory is an ancestor of oldCategory
  const isAncestor = (
    tree: CategoryInfo[],
    ancestorId: string,
    childId: string
  ): boolean => {
    for (const node of tree) {
      if (node.id === ancestorId) {
        // Found the potential ancestor, check if childId exists in its subtree
        const hasChild = (n: CategoryInfo): boolean => {
          if (n.id === childId) return true;
          if (n.children) {
            return n.children.some(hasChild);
          }
          return false;
        };
        return hasChild(node);
      }
      if (node.children && isAncestor(node.children, ancestorId, childId)) {
        return true;
      }
    }
    return false;
  };

  const movingToAncestor = oldCategory ? isAncestor(tree, newCategory, oldCategory) : false;

  const updateNode = (node: CategoryInfo): CategoryInfo => {
    let updatedNode = { ...node };

    // Decrement old category
    if (oldCategory && node.id === oldCategory) {
      updatedNode.modCount = Math.max(0, (node.modCount || 0) - 1);
    }

    // Increment new category ONLY if not moving to ancestor
    // (if moving to ancestor, the count stays the same because child count already contributes to parent)
    if (node.id === newCategory && !movingToAncestor) {
      updatedNode.modCount = (node.modCount || 0) + 1;
    }

    // Recursively update children
    if (node.children && node.children.length > 0) {
      updatedNode.children = node.children.map(updateNode);
    }

    return updatedNode;
  };

  return tree.map(updateNode);
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

      // Refresh both mods and tree
      // The backend emits MOD.CATEGORY_UPDATED → CategoryEventHandler invalidates cache → emits CATEGORY_TREE_UPDATED
      await refreshMods(profileId);

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
  const { setCategoryLoading, setCategoryFilteredMods } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        const mods = await modService.getModsByCategory(profileId, nodeId);
        setCategoryFilteredMods(mods);
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
  const { setCategoryLoading, setCategoryFilteredMods } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        const mods = await modService.getUnclassifiedMods(profileId);
        setCategoryFilteredMods(mods);
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
  const findNode = (nodes: typeof state.CategoryTree): typeof state.selectedCategory => {
    for (const node of nodes) {
      if (node.id === nodeId) return node;
      if (node.children) {
        const found = findNode(node.children);
        if (found) return found;
      }
    }
    return undefined;
  };

  const node = findNode(state.CategoryTree);
  state.setSelectedCategory(node);

  // Load mods for this Category
  if (nodeId === CATEGORY_IDS.UNCLASSIFIED) {
    await loadUncategorizedMods(profileId);
  } else {
    await loadModsByCategory(profileId, nodeId);
  }
}
