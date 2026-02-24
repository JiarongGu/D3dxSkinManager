/**
 * Category operations - SINGLE SOURCE OF TRUTH for category update logic
 * Handles complex tree count updates with optimistic UI
 */

import { useModsStore } from '../store/modsStore';
import { modService } from '../services/modService';
import { ClassificationNode } from '../../../shared/types/classification.types';
import { ModInfo } from '../../../shared/types/mod.types';
import { notification } from '../../../shared/utils/notification';
import { refreshMods } from './modOperations';
import { refreshClassificationTree } from './classificationOperations';

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
  const currentTree = state.classificationTree;

  // Calculate optimistic tree with updated counts
  const optimisticTree = updateTreeCounts(
    currentTree,
    currentMods,
    oldCategoryId,
    categoryId
  );

  // 1. Apply optimistic updates to mod and tree (Zustand automatically updates all slices)
  state.optimisticCategoryUpdate(sha, categoryId);
  state.setClassificationTree(optimisticTree);

  try {
    // 2. Perform backend operation
    await modService.updateCategory(profileId, sha, categoryId);
    notification.success('Category updated');

    // 3. Refresh both mods and tree from backend (with delayed loading to prevent flicker)
    await Promise.all([refreshMods(profileId), refreshClassificationTree(profileId)]);

    return true;
  } catch (error) {
    // 4. Revert optimistic update on error
    if (modBeingUpdated) {
      state.optimisticCategoryUpdate(sha, modBeingUpdated.category);
    }
    state.setClassificationTree(currentTree);

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
  tree: ClassificationNode[],
  mods: ModInfo[],
  oldCategory: string | undefined,
  newCategory: string
): ClassificationNode[] {
  // Check if newCategory is an ancestor of oldCategory
  const isAncestor = (
    tree: ClassificationNode[],
    ancestorId: string,
    childId: string
  ): boolean => {
    for (const node of tree) {
      if (node.id === ancestorId) {
        // Found the potential ancestor, check if childId exists in its subtree
        const hasChild = (n: ClassificationNode): boolean => {
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

  const updateNode = (node: ClassificationNode): ClassificationNode => {
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
 * Batch update categories for multiple mods
 */
export async function batchUpdateCategories(
  profileId: string,
  shas: string[],
  categoryId: string
): Promise<void> {
  try {
    // For batch operations, skip optimistic updates due to complexity
    // Just perform the operation and refresh
    await Promise.all(
      shas.map((sha) => modService.updateCategory(profileId, sha, categoryId))
    );

    notification.success(`Updated ${shas.length} mod(s) category`);

    // Refresh both mods and tree
    await Promise.all([refreshMods(profileId), refreshClassificationTree(profileId)]);
  } catch (error) {
    notification.error('Failed to batch update categories');
    throw error;
  }
}
