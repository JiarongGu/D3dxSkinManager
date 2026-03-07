/**
 * Category operations - SINGLE SOURCE OF TRUTH for category update logic
 * Handles complex tree count updates with optimistic UI
 */

import { useModsStore } from '../store/modsStore';
import { CATEGORY_IDS, CategoryInfo } from '../../../shared/types/category.types';
import { notification } from '../../../shared/utils/notification';
import { categoryService, modService } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { executeWithDelayedLoading } from '../../../shared/utils/delayedLoading';
import i18n from '../../../shared/services/i18n';

/**
 * Update mod category
 * Backend will fire MOD_LIST_UPDATED and CATEGORY_TREE_UPDATED events which trigger refresh via ModProvider
 */
export async function updateModCategory(
  profileId: string,
  sha: string,
  categoryId: string,
): Promise<boolean> {
  try {
    // Perform backend operation
    await modService.updateCategory(profileId, sha, categoryId);
    notification.success(i18n.t('category.operations.updateSuccess'));

    // Backend fires MOD_LIST_UPDATED and CATEGORY_TREE_UPDATED events
    // → ModProvider refreshes mods and category tree automatically
    return true;
  } catch (error: unknown) {
    notification.error(i18n.t('category.operations.updateFailed'));
    return false;
  }
}

/**
 * Batch update categories for multiple mods using bulk IPC call
 */
export async function batchUpdateCategories(
  profileId: string,
  shas: string[],
  categoryId: string
): Promise<boolean> {
  try {
    // For batch operations, skip optimistic updates due to complexity
    // Just perform the operation and refresh
    const updatedCount = await modService.batchUpdateCategory(profileId, shas, categoryId);

    if (updatedCount > 0) {
      notification.success(i18n.t('category.operations.batchUpdateSuccess', { count: updatedCount }));
      return true;
    } else {
      notification.error(i18n.t('category.operations.noModsUpdated'));
      return false;
    }
  } catch (error: unknown) {
    notification.error(i18n.t('category.operations.batchUpdateFailed'));
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
      200
    );
  } catch (error: unknown) {
    handleError(error);
  }
}

/**
 * Refresh Category tree
 * Note: Debouncing is handled by ModProvider (20ms) to prevent rapid-fire events
 */
export async function refreshCategoryTree(profileId: string): Promise<void> {
  await loadCategoryTree(profileId);
}

/**
 * Load mods filtered by Category node
 * Uses delayed loading (200ms) to avoid flicker for fast queries
 * Note: Debouncing is handled by caller when needed
 */
export async function loadModsByCategory(
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
      200
    );
  } catch (error: unknown) {
    handleError(error);
  }
}

/**
 * Load Unclassified mods (no category assigned)
 * Uses delayed loading (100ms) to avoid flicker for fast queries
 */
export async function loadUnclassifiedMods(profileId: string): Promise<void> {
  const { setCategoryLoading, setMods } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        const mods = await modService.getUnclassifiedMods(profileId);
        setMods(mods);
      },
      setCategoryLoading,
      200
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
 * Load unclassified mod count
 * Updates store state with count of mods without category assignment
 */
export async function loadUnclassifiedCount(profileId: string): Promise<void> {
  const { setUnclassifiedCount } = useModsStore.getState();

  try {
    const count = await modService.getUnclassifiedCount(profileId);
    setUnclassifiedCount(count);
  } catch (error: unknown) {
    handleError(error);
  }
}

/**
 * Select Category node and load its mods
 */
export async function selectCategory(
  profileId: string,
  nodeId: string
): Promise<void> {
  const state = useModsStore.getState();

  // load mods for unclassified category
  if (nodeId === CATEGORY_IDS.UNCLASSIFIED) {
    state.setSelectedCategory({
      id: CATEGORY_IDS.UNCLASSIFIED,
      name: "Unclassified",
      priority: 0,
      children: []
    });
    await loadUnclassifiedMods(profileId);
    return;
  }

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
  
  if (!node) {
    return;
  }
  console.log(node);

  state.setSelectedCategory(node);
  // Load mods for this Category
  await loadModsByCategory(profileId, nodeId);
}
