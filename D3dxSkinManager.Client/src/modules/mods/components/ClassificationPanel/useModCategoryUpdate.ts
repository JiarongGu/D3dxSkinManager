import { useCallback } from 'react';
import { App } from 'antd';
import { useModsContext } from '../../context/ModsContext';

interface UseModCategoryUpdateProps {
  onRefreshTree?: () => Promise<void>;
}

/**
 * Custom hook for updating mod categories via drag-and-drop
 * Consolidates logic for both tree nodes and unclassified item
 * Uses optimistic updates for instant UI feedback
 */
export function useModCategoryUpdate({
  onRefreshTree,
}: UseModCategoryUpdateProps) {
  const { message } = App.useApp();
  const { actions } = useModsContext();
  const { updateModCategory: updateModCategoryOptimistic } = actions;

  /**
   * Update a mod's category with optimistic updates
   * @param modSha - SHA of the mod to update
   * @param modName - Display name of the mod (for success message)
   * @param categoryId - New category ID (empty string for unclassified)
   * @param categoryName - Display name of the category (for success message)
   */
  const updateModCategory = useCallback(
    async (
      modSha: string,
      modName: string,
      categoryId: string,
      categoryName: string
    ) => {
      try {
        // Use optimistic update from ModsContext - handles state updates automatically
        // The optimistic update will trigger verification after 50ms
        // If verification detects a mismatch OR error occurs, the onMismatch callback will refresh the tree
        const success = await updateModCategoryOptimistic(
          modSha,
          categoryId,
          onRefreshTree // Only called when verification mismatch or error occurs
        );

        if (success) {
          message.success(`Moved "${modName}" to "${categoryName}"`);
          return true;
        } else {
          message.error('Failed to update mod category');
          return false;
        }
      } catch (error) {
        console.error('Error updating mod category:', error);
        message.error('Failed to update mod category');
        return false;
      }
    },
    [updateModCategoryOptimistic, message, onRefreshTree]
  );

  return { updateModCategory };
}
