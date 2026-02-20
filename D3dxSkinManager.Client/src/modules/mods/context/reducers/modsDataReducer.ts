import { ModInfo } from "../../../../shared/types/mod.types";

// ============================================================================
// State Types
// ============================================================================

export interface ModsDataState {
  mods: ModInfo[];
  loading: boolean;
  error: string | null;
  selectedMod: ModInfo | null;
  selectedMods: ModInfo[];
}

// ============================================================================
// Actions
// ============================================================================

export type ModsDataAction =
  | { type: "SET_MODS"; payload: ModInfo[] }
  | { type: "SET_LOADING"; payload: boolean }
  | { type: "SET_ERROR"; payload: string | null }
  | { type: "SELECT_MOD"; payload: ModInfo | null }
  | { type: "SELECT_MODS"; payload: ModInfo[] }
  | {
      type: "UPDATE_MOD_LOCAL";
      payload: { sha: string; data: Partial<ModInfo> };
    };

// ============================================================================
// Reducer
// ============================================================================

export function modsDataReducer(
  state: ModsDataState,
  action: ModsDataAction
): ModsDataState {
  switch (action.type) {
    case "SET_MODS":
      return { ...state, mods: action.payload, error: null };
      // Don't set loading to false here - let useDelayedLoading handle it

    case "SET_LOADING":
      return { ...state, loading: action.payload };

    case "SET_ERROR":
      return { ...state, error: action.payload, loading: false };

    case "SELECT_MOD":
      return { ...state, selectedMod: action.payload };

    case "SELECT_MODS":
      return { ...state, selectedMods: action.payload };

    case "UPDATE_MOD_LOCAL":
      return {
        ...state,
        mods: state.mods.map((mod) =>
          mod.sha === action.payload.sha
            ? { ...mod, ...action.payload.data }
            : mod
        ),
        selectedMod:
          state.selectedMod?.sha === action.payload.sha
            ? { ...state.selectedMod, ...action.payload.data }
            : state.selectedMod,
      };

    default:
      return state;
  }
}

// ============================================================================
// Initial State
// ============================================================================

export const initialModsDataState: ModsDataState = {
  mods: [],
  loading: false,
  error: null,
  selectedMod: null,
  selectedMods: [],
};
