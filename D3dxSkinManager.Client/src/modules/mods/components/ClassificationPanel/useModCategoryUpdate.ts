import { useCallback } from "react";
import { useModsContext } from "../../context/ModsContext";
import { useStableRef } from "../../../../shared/hooks/useStableRef";
import { notification } from "../../../../shared/utils/notification";

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
  const { state, actions } = useModsContext();
  const { updateModCategory: updateModCategoryOptimistic } = actions;

  // Store mods in a stable ref to avoid closure issues
  const modsRef = useStableRef(state.mods);

  /**
   * Update a mod's category with optimistic updates
   * @param modSha - SHA of the mod to update
   * @param categoryId - New category ID (empty string for unclassified)
   * @param categoryName - Display name of the category (for success message)
   */
  const updateModCategory = useCallback(
    async (modSha: string, categoryId: string, categoryName: string) => {
      // Find the mod name if not provided
      const mod = modsRef.current.find((m) => m.sha === modSha);
      const modName = mod?.name || modSha;

      try {
        // Use optimistic update from ModsContext - handles state updates automatically
        // The optimistic update will trigger verification after 50ms
        // If verification detects a mismatch OR error occurs, the onMismatch callback will refresh the tree
        const success = await updateModCategoryOptimistic(
          modSha,
          categoryId,
          onRefreshTree, // Only called when verification mismatch or error occurs
        );

        if (success) {
          notification.success(`Moved "${modName}" to "${categoryName}"`);
          return true;
        } else {
          notification.error("Failed to update mod category");
          return false;
        }
      } catch (error) {
        console.error("Error updating mod category:", error);
        notification.error("Failed to update mod category");
        return false;
      }
    },
    [updateModCategoryOptimistic, onRefreshTree], // modsRef is stable
  );

  return { updateModCategory };
}
