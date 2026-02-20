import { notification } from '../../../../shared/utils/notification';
import { useReducer, useCallback, Dispatch, useEffect } from "react";
import { ModInfo } from "../../../../shared/types/mod.types";
import { ClassificationNode } from "../../../../shared/types/classification.types";
import { modService } from "../../services/modService";
import { classificationService } from "../../../../shared/services/classificationService";
import { useDelayedLoading } from "../../../../shared/hooks/useDelayedLoading";
import {
  classificationReducer,
  initialClassificationState,
  ClassificationState,
  ClassificationAction,
} from "../reducers/classificationReducer";

export interface UseClassificationDataReturn {
  // State
  state: ClassificationState;
  dispatch: Dispatch<ClassificationAction>;

  // Actions
  loadClassificationTree: (profileId: string) => Promise<void>;
  refreshClassificationTree: (profileId: string) => Promise<void>;
  loadModsByClassification: (profileId: string, nodeId: string) => Promise<void>;
  loadUnclassifiedMods: (profileId: string) => Promise<void>;
  updateModCategory: (
    profileId: string,
    sha: string,
    categoryId: string,
    onTreeMismatch?: () => void
  ) => Promise<boolean>;
  selectClassification: (node: ClassificationNode | null) => void;
  setClassificationSearch: (query: string) => void;
  updateTreeOptimistic: (tree: ClassificationNode[]) => void;
}

export function useClassificationData(): UseClassificationDataReturn {
  const [state, dispatch] = useReducer(
    classificationReducer,
    initialClassificationState
  );

  // Use delayed loading - only show loading state if operation takes >100ms
  const { loading, execute: executeWithDelayedLoading } = useDelayedLoading(100);

  // Sync delayed loading state with reducer
  useEffect(() => {
    dispatch({ type: "SET_CLASSIFICATION_LOADING", payload: loading });
  }, [loading]);

  const loadClassificationTree = useCallback(async (profileId: string) => {
    if (!profileId) {
      return;
    }

    try {
      await executeWithDelayedLoading(async () => {
        // Just fetch and update tree - loading state is handled by useEffect
        const tree = await classificationService.getClassificationTree(profileId);
        dispatch({ type: "SET_CLASSIFICATION_TREE", payload: tree });
      });
    } catch (error) {
      // Handle errors from executeWithDelayedLoading
      if (error instanceof Error && error.message === 'Operation already in progress') {
        return;
      }

      const errorMessage =
        error instanceof Error
          ? error.message
          : "Failed to load classification tree";
      // Only log if it's not the expected "Profile ID is required" error
      if (!errorMessage.includes("Profile ID is required")) {
        console.error(
          "[useClassificationData] Failed to load classification tree:",
          errorMessage
        );
      }
    }
  }, [executeWithDelayedLoading]);

  const refreshClassificationTree = useCallback(
    async (profileId: string) => {
      await loadClassificationTree(profileId);
    },
    [loadClassificationTree]
  );

  const loadModsByClassification = useCallback(
    async (profileId: string, nodeId: string) => {
      if (!profileId) {
        return;
      }
      try {
        const mods = await modService.getModsByClassification(
          profileId,
          nodeId
        );
        dispatch({ type: "SET_CLASSIFICATION_FILTERED_MODS", payload: mods });
      } catch (error) {
        const errorMessage =
          error instanceof Error
            ? error.message
            : "Failed to load mods by classification";
        console.error(
          "[useClassificationData] Failed to load mods by classification:",
          errorMessage
        );
        dispatch({ type: "SET_CLASSIFICATION_FILTERED_MODS", payload: [] });
      }
    },
    []
  );

  const loadUnclassifiedMods = useCallback(async (profileId: string) => {
    if (!profileId) {
      return;
    }
    try {
      const mods = await modService.getUnclassifiedMods(profileId);
      dispatch({ type: "SET_CLASSIFICATION_FILTERED_MODS", payload: mods });
    } catch (error) {
      const errorMessage =
        error instanceof Error
          ? error.message
          : "Failed to load unclassified mods";
      console.error(
        "[useClassificationData] Failed to load unclassified mods:",
        errorMessage
      );
      dispatch({ type: "SET_CLASSIFICATION_FILTERED_MODS", payload: [] });
    }
  }, []);

  const updateModCategory = useCallback(
    async (
      profileId: string,
      sha: string,
      categoryId: string,
      onTreeMismatch?: () => void
    ) => {
      if (!profileId) {
        return false;
      }

      // Update filtered mod optimistically
      dispatch({
        type: "UPDATE_FILTERED_MOD",
        payload: { sha, data: { category: categoryId }, newCategory: categoryId },
      });

      try {
        // Perform backend operation
        await modService.updateCategory(profileId, sha, categoryId);
        notification.success("Category updated");

        return true;
      } catch (error) {
        notification.error("Failed to update category");

        // Refresh tree on error to ensure counts are correct
        if (onTreeMismatch) {
          onTreeMismatch();
        }
        return false;
      }
    },
    []
  );

  const selectClassification = useCallback((node: ClassificationNode | null) => {
    dispatch({ type: "SELECT_CLASSIFICATION", payload: node });
  }, []);

  const setClassificationSearch = useCallback((query: string) => {
    dispatch({ type: "SET_CLASSIFICATION_SEARCH", payload: query });
  }, []);

  const updateTreeOptimistic = useCallback((tree: ClassificationNode[]) => {
    dispatch({ type: "UPDATE_CLASSIFICATION_TREE_OPTIMISTIC", payload: tree });
  }, []);

  return {
    state,
    dispatch,
    loadClassificationTree,
    refreshClassificationTree,
    loadModsByClassification,
    loadUnclassifiedMods,
    updateModCategory,
    selectClassification,
    setClassificationSearch,
    updateTreeOptimistic,
  };
}
