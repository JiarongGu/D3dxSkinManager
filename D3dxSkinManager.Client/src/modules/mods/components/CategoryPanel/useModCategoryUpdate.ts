import { useCallback } from "react";
import { useMods } from "../../hooks/useMods";
import { useModsStore } from "../../store/modsStore";
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
  const { updateModCategory: updateModCategoryOp } = useMods();
  const mods = useModsStore(s => s.mods);

  // Store mods in a stable ref to avoid closure issues
  const modsRef = useStableRef(mods);

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
        // Use new category operation - handles optimistic updates and verification automatically
        // If verification detects a mismatch, the onMismatch callback will refresh the tree
        const success = await updateModCategoryOp(
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
    [updateModCategoryOp, onRefreshTree], // modsRef is stable
  );

  return { updateModCategory };
}
