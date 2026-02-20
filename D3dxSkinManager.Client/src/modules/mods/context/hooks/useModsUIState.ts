import { useReducer, useCallback, Dispatch } from "react";
import { ModInfo } from "../../../../shared/types/mod.types";
import { ImportTask } from "../../components/AddModWindow";
import {
  uiReducer,
  initialUIState,
  UIState,
  UIAction,
} from "../reducers/uiReducer";

export interface UseModsUIStateReturn {
  // State
  state: UIState;
  dispatch: Dispatch<UIAction>;

  // Actions
  setSelectedObject: (object: string) => void;
  setExpandedKeys: (keys: React.Key[]) => void;
  setSearchQuery: (query: string) => void;
  openEditDialog: (mod: ModInfo) => void;
  closeEditDialog: () => void;
  openTagDialog: (tags: string[], context: "mod" | "import") => void;
  closeTagDialog: () => void;
  openBatchEditDialog: (mods: ModInfo[]) => void;
  closeBatchEditDialog: () => void;
  openImportWindow: () => void;
  closeImportWindow: () => void;
  openAddModUnit: (task: ImportTask) => void;
  closeAddModUnit: () => void;
  openBatchEditUnit: (taskIds: string[]) => void;
  closeBatchEditUnit: () => void;
  setCurrentTags: (tags: string[]) => void;
}

export function useModsUIState(): UseModsUIStateReturn {
  const [state, dispatch] = useReducer(uiReducer, initialUIState);

  const setSelectedObject = useCallback((object: string) => {
    dispatch({ type: "SET_SELECTED_OBJECT", payload: object });
  }, []);

  const setExpandedKeys = useCallback((keys: React.Key[]) => {
    dispatch({ type: "SET_EXPANDED_KEYS", payload: keys });
  }, []);

  const setSearchQuery = useCallback((query: string) => {
    dispatch({ type: "SET_SEARCH_QUERY", payload: query });
  }, []);

  const openEditDialog = useCallback((mod: ModInfo) => {
    dispatch({ type: "OPEN_EDIT_DIALOG", payload: mod });
  }, []);

  const closeEditDialog = useCallback(() => {
    dispatch({ type: "CLOSE_EDIT_DIALOG" });
  }, []);

  const openTagDialog = useCallback(
    (tags: string[], context: "mod" | "import") => {
      dispatch({ type: "OPEN_TAG_DIALOG", payload: { tags, context } });
    },
    []
  );

  const closeTagDialog = useCallback(() => {
    dispatch({ type: "CLOSE_TAG_DIALOG" });
  }, []);

  const openBatchEditDialog = useCallback((mods: ModInfo[]) => {
    dispatch({ type: "OPEN_BATCH_EDIT_DIALOG", payload: mods });
  }, []);

  const closeBatchEditDialog = useCallback(() => {
    dispatch({ type: "CLOSE_BATCH_EDIT_DIALOG" });
  }, []);

  const openImportWindow = useCallback(() => {
    dispatch({ type: "OPEN_IMPORT_WINDOW" });
  }, []);

  const closeImportWindow = useCallback(() => {
    dispatch({ type: "CLOSE_IMPORT_WINDOW" });
  }, []);

  const openAddModUnit = useCallback((task: ImportTask) => {
    dispatch({ type: "OPEN_ADD_MOD_UNIT", payload: task });
  }, []);

  const closeAddModUnit = useCallback(() => {
    dispatch({ type: "CLOSE_ADD_MOD_UNIT" });
  }, []);

  const openBatchEditUnit = useCallback((taskIds: string[]) => {
    dispatch({ type: "OPEN_BATCH_EDIT_UNIT", payload: taskIds });
  }, []);

  const closeBatchEditUnit = useCallback(() => {
    dispatch({ type: "CLOSE_BATCH_EDIT_UNIT" });
  }, []);

  const setCurrentTags = useCallback((tags: string[]) => {
    dispatch({ type: "SET_CURRENT_TAGS", payload: tags });
  }, []);

  return {
    state,
    dispatch,
    setSelectedObject,
    setExpandedKeys,
    setSearchQuery,
    openEditDialog,
    closeEditDialog,
    openTagDialog,
    closeTagDialog,
    openBatchEditDialog,
    closeBatchEditDialog,
    openImportWindow,
    closeImportWindow,
    openAddModUnit,
    closeAddModUnit,
    openBatchEditUnit,
    closeBatchEditUnit,
    setCurrentTags,
  };
}
