import { useCallback, useMemo } from "react";
import { useMods } from "../../hooks/useMods";
import { useModsStore } from "../../store/modsStore";
import { useStableRef } from "../../../../shared/hooks/useStableRef";
import { notification } from "../../../../shared/utils/notification";

/**
 * Custom hook for updating mod categories via drag-and-drop
 * Consolidates logic for both tree nodes and unclassified item
 * Uses optimistic updates for instant UI feedback
 */
export function useModCategoryUpdate() {
  const { updateModCategory: updateModCategoryOp, updateModsCategory: updateModsCategoryOp } = useMods();
  const mods = useModsStore(s => s.mods);
  const categoryFilteredMods = useModsStore(s => s.CategoryFilteredMods);

  // Combine both mod lists to ensure we can find the mod name
  // When a category is selected, the mod being dragged is in categoryFilteredMods, not in mods
  // Memoize to avoid creating new array on every render
  const allMods = useMemo(() => {
    return categoryFilteredMods ? [...mods, ...categoryFilteredMods] : mods;
  }, [mods, categoryFilteredMods]);

  // Store mods in a stable ref to avoid closure issues
  const modsRef = useStableRef(allMods);

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
        const success = await updateModCategoryOp(
          modSha,
          categoryId,
        );

        if (success) {
          notification.success(`Moved "${modName}" to "${categoryName}"`);
          return true;
        } else {
          notification.error("Failed to update mod category");
          return false;
        }
      } catch (error: unknown) {
                notification.error("Failed to update mod category");
        return false;
      }
    },
    [updateModCategoryOp], // modsRef is stable
  );

  /**
   * Update multiple mods' categories with optimistic updates
   * @param modShas - Array of mod SHAs to update
   * @param categoryId - New category ID (empty string for unclassified)
   * @param categoryName - Display name of the category (for success message)
   */
  const updateModsCategory = useCallback(
    async (modShas: string[], categoryId: string, categoryName: string) => {
      try {
        const success = await updateModsCategoryOp(
          modShas,
          categoryId,
        );

        if (success) {
          notification.success(`Moved ${modShas.length} mod(s) to "${categoryName}"`);
          return true;
        } else {
          notification.error("Failed to update categories for selected mods");
          return false;
        }
      } catch (error: unknown) {
                notification.error("Failed to update categories for selected mods");
        return false;
      }
    },
    [updateModsCategoryOp]
  );

  return { updateModCategory, updateModsCategory };
}
