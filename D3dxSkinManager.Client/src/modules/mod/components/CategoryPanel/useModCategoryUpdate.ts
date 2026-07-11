import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { useMods, useModsState } from "../../hooks/useMods";
import { useStableRef } from "../../../../shared/hooks/useStableRef";
import { notification } from "../../../../shared/utils/notification";

/**
 * Custom hook for updating mod categories via drag-and-drop
 * Consolidates logic for both tree nodes and unclassified item
 * Uses optimistic updates for instant UI feedback
 */
export function useModCategoryUpdate() {
  const { t } = useTranslation();
  const { updateModCategory: updateModCategoryOp, updateModsCategory: updateModsCategoryOp } = useMods();
  const mods = useModsState(s => s.mods);

  // Store mods in a stable ref to avoid closure issues
  const modsRef = useStableRef(mods ?? []);

  /**
   * Update a mod's category with optimistic updates
   * @param modId - ID of the mod to update
   * @param categoryId - New category ID (empty string for unclassified)
   * @param categoryName - Display name of the category (for success message)
   */
  const updateModCategory = useCallback(
    async (modId: string, categoryId: string, categoryName: string) => {
      // Find the mod name if not provided
      const mod = modsRef.current.find((m: { id: string }) => m.id === modId);
      const modName = mod?.name || modId;

      try {
        const success = await updateModCategoryOp(
          modId,
          categoryId,
        );

        if (success) {
          notification.success(`Moved "${modName}" to "${categoryName}"`);
          return true;
        } else {
          notification.error(t('category.update.modCategoryFailed'));
          return false;
        }
      } catch (error: unknown) {
                notification.error(t('category.update.modCategoryFailed'));
        return false;
      }
    },
    [updateModCategoryOp], // modsRef is stable
  );

  /**
   * Update multiple mods' categories with optimistic updates
   * @param modIds - Array of mod IDs to update
   * @param categoryId - New category ID (empty string for unclassified)
   * @param categoryName - Display name of the category (for success message)
   */
  const updateModsCategory = useCallback(
    async (modIds: string[], categoryId: string, categoryName: string) => {
      try {
        const success = await updateModsCategoryOp(
          modIds,
          categoryId,
        );

        if (success) {
          notification.success(`Moved ${modIds.length} mod(s) to "${categoryName}"`);
          return true;
        } else {
          notification.error(t('category.update.modsCategoryFailed'));
          return false;
        }
      } catch (error: unknown) {
                notification.error(t('category.update.modsCategoryFailed'));
        return false;
      }
    },
    [updateModsCategoryOp]
  );

  return { updateModCategory, updateModsCategory };
}
