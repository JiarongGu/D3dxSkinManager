import { useState, useEffect, useCallback } from "react";
import { debounce } from "lodash-es";
import type { Color } from "antd/es/color-picker";
import { Tag } from "../types/mod.types";
import { modService } from "../services/ipc";
import { handleError } from "../utils/errorHandler";
import { notification } from "../utils/notification";

/**
 * Shared hook for tag management operations
 * Used by TagManagementTool and TagManagementDialog
 */
export function useTagManagement(profileId: string | undefined) {
  const [allTags, setAllTags] = useState<Tag[]>([]);
  const [loading, setLoading] = useState(false);

  // Load all tags
  const loadTags = useCallback(async () => {
    if (!profileId) return;

    try {
      setLoading(true);
      const tags = await modService.getAllTags(profileId);
      setAllTags(tags);
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setLoading(false);
    }
  }, [profileId]);

  // Load tags on mount and when profileId changes
  useEffect(() => {
    if (profileId) {
      void loadTags();
    }
  }, [profileId, loadTags]);

  // Create or update tag
  const upsertTag = useCallback(async (tagName: string, hexColor: string) => {
    if (!profileId) return false;

    try {
      await modService.upsertTag(profileId, tagName, hexColor);
      await loadTags();
      return true;
    } catch (error: unknown) {
      handleError(error);
      return false;
    }
  }, [profileId, loadTags]);

  // Delete tag
  const deleteTag = useCallback(async (tagName: string) => {
    if (!profileId) return false;

    try {
      await modService.deleteTag(profileId, tagName);
      await loadTags();
      notification.success(`Tag "${tagName}" deleted`);
      return true;
    } catch (error: unknown) {
      handleError(error);
      return false;
    }
  }, [profileId, loadTags]);

  // Debounced function to save tag color to backend
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const debouncedSaveTagColor = useCallback(
    debounce(async (profileId: string, tagName: string, hexColor: string) => {
      try {
        await modService.upsertTag(profileId, tagName, hexColor);
      } catch (error: unknown) {
        handleError(error);
      }
    }, 500),
    [],
  );

  // Update tag color locally and save to backend with debounce
  const updateTagColor = useCallback((tagName: string, color: Color) => {
    if (!profileId) return;

    const hexColor = color.toHexString();

    // Update local state immediately for real-time feedback
    setAllTags((prev) =>
      prev.map((t) =>
        t.name === tagName
          ? { ...t, color: hexColor, updatedAt: new Date().toISOString() }
          : t,
      ),
    );

    // Debounced save to backend (500ms delay)
    void debouncedSaveTagColor(profileId, tagName, hexColor);
  }, [profileId, debouncedSaveTagColor]);

  // Cleanup debounced function
  useEffect(() => {
    return () => {
      debouncedSaveTagColor.cancel();
    };
  }, [debouncedSaveTagColor]);

  return {
    allTags,
    loading,
    loadTags,
    upsertTag,
    deleteTag,
    updateTagColor,
  };
}
