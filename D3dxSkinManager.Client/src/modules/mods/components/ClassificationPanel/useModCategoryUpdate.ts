import { useCallback } from 'react';
import { App } from 'antd';
import { modService } from '../../services/modService';
import { useProfile } from '../../../../shared/context/ProfileContext';

interface UseModCategoryUpdateProps {
  onRefreshTree?: () => Promise<void>;
  onModsRefresh?: () => Promise<void>;
}

/**
 * Custom hook for updating mod categories via drag-and-drop
 * Consolidates logic for both tree nodes and unclassified item
 */
export function useModCategoryUpdate({
  onRefreshTree,
  onModsRefresh,
}: UseModCategoryUpdateProps) {
  const { message } = App.useApp();
  const { selectedProfileId } = useProfile();

  /**
   * Update a mod's category
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
      if (!selectedProfileId) {
        return false;
      }

      try {
        const success = await modService.updateCategory(
          selectedProfileId,
          modSha,
          categoryId
        );

        if (success) {
          message.success(`Moved "${modName}" to "${categoryName}"`);

          // Refresh the tree to update counts
          if (onRefreshTree) {
            await onRefreshTree();
          }

          // Refresh the mods list to show updated category
          if (onModsRefresh) {
            await onModsRefresh();
          }

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
    [selectedProfileId, message, onRefreshTree, onModsRefresh]
  );

  return { updateModCategory };
}
