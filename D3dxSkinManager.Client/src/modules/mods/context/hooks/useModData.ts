import { notification } from '../../../../shared/utils/notification';
import { useReducer, useCallback, Dispatch, useEffect } from "react";
import { ModInfo } from "../../../../shared/types/mod.types";
import { modService } from "../../services/modService";
import { useDelayedLoading } from "../../../../shared/hooks/useDelayedLoading";
import {
  modsDataReducer,
  initialModsDataState,
  ModsDataState,
  ModsDataAction,
} from "../reducers/modsDataReducer";

export interface UseModDataReturn {
  // State
  state: ModsDataState;
  dispatch: Dispatch<ModsDataAction>;

  // Actions
  loadMods: (profileId: string) => Promise<void>;
  refreshMods: (profileId: string) => Promise<void>;
  updateMod: (profileId: string, sha: string, data: Partial<ModInfo>) => Promise<void>;
  deleteMod: (profileId: string, sha: string, onSuccess: () => Promise<void>) => Promise<void>;
  selectMod: (mod: ModInfo | null) => void;
  selectMods: (mods: ModInfo[]) => void;

  // Note: loadModInGame/unloadModFromGame are in ModsContext
  // ModsContext handles these operations because they need to sync
  // both the main mod list AND the classification filtered list
}

export function useModData(): UseModDataReturn {
  const [state, dispatch] = useReducer(modsDataReducer, initialModsDataState);

  // Use delayed loading - only show loading state if operation takes >100ms
  const { loading, execute: executeWithDelayedLoading } = useDelayedLoading(100);

  // Sync delayed loading state with reducer
  useEffect(() => {
    dispatch({ type: "SET_LOADING", payload: loading });
  }, [loading]);

  const loadMods = useCallback(async (profileId: string) => {
    if (!profileId) {
      return;
    }

    try {
      await executeWithDelayedLoading(async () => {
        const mods = await modService.getAllMods(profileId);
        dispatch({ type: "SET_MODS", payload: mods });
      });
    } catch (error) {
      // Handle errors from executeWithDelayedLoading
      if (error instanceof Error && error.message === 'Operation already in progress') {
        return;
      }

      const errorMessage =
        error instanceof Error ? error.message : "Failed to load mods";
      // Only show error if it's not the expected "Profile ID is required" error
      if (!errorMessage.includes("Profile ID is required")) {
        dispatch({ type: "SET_ERROR", payload: errorMessage });
        notification.error(errorMessage);
      }
    }
  }, [executeWithDelayedLoading]);

  const refreshMods = useCallback(
    async (profileId: string) => {
      await loadMods(profileId);
    },
    [loadMods]
  );

  const updateMod = useCallback(
    async (profileId: string, sha: string, data: Partial<ModInfo>) => {
      if (!profileId) {
        return;
      }
      try {
        await modService.updateMetadata(profileId, sha, data);
        dispatch({ type: "UPDATE_MOD_LOCAL", payload: { sha, data } });
        notification.success("Mod updated successfully");
      } catch (error) {
        notification.error("Failed to update mod");
        throw error;
      }
    },
    []
  );

  const deleteMod = useCallback(
    async (profileId: string, sha: string, onSuccess: () => Promise<void>) => {
      if (!profileId) {
        return;
      }
      try {
        await modService.deleteMod(profileId, sha);
        await onSuccess();
        notification.success("Mod deleted successfully");
      } catch (error) {
        notification.error("Failed to delete mod");
        throw error;
      }
    },
    []
  );

  const selectMod = useCallback((mod: ModInfo | null) => {
    dispatch({ type: "SELECT_MOD", payload: mod });
  }, []);

  const selectMods = useCallback((mods: ModInfo[]) => {
    dispatch({ type: "SELECT_MODS", payload: mods });
  }, []);

  return {
    state,
    dispatch,
    loadMods,
    refreshMods,
    updateMod,
    deleteMod,
    selectMod,
    selectMods,
  };
}
