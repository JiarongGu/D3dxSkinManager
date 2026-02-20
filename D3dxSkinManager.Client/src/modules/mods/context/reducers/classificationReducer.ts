import { ClassificationNode } from "../../../../shared/types/classification.types";
import { ModInfo } from "../../../../shared/types/mod.types";

// ============================================================================
// State Types
// ============================================================================

export interface ClassificationState {
  classificationTree: ClassificationNode[];
  classificationLoading: boolean;
  selectedClassification: ClassificationNode | null;
  classificationFilteredMods: ModInfo[] | null;
  classificationSearch: string;
}

// ============================================================================
// Actions
// ============================================================================

export type ClassificationAction =
  | { type: "SET_CLASSIFICATION_TREE"; payload: ClassificationNode[] }
  | { type: "UPDATE_CLASSIFICATION_TREE_OPTIMISTIC"; payload: ClassificationNode[] }
  | { type: "SET_CLASSIFICATION_LOADING"; payload: boolean }
  | { type: "SELECT_CLASSIFICATION"; payload: ClassificationNode | null }
  | { type: "SET_CLASSIFICATION_FILTERED_MODS"; payload: ModInfo[] | null }
  | { type: "SET_CLASSIFICATION_SEARCH"; payload: string }
  | {
      type: "UPDATE_FILTERED_MOD";
      payload: { sha: string; data: Partial<ModInfo>; newCategory?: string };
    };

// ============================================================================
// Reducer
// ============================================================================

export function classificationReducer(
  state: ClassificationState,
  action: ClassificationAction
): ClassificationState {
  switch (action.type) {
    case "SET_CLASSIFICATION_TREE":
      return {
        ...state,
        classificationTree: action.payload,
        // Don't set loading to false here - let useDelayedLoading handle it
      };

    case "UPDATE_CLASSIFICATION_TREE_OPTIMISTIC":
      return {
        ...state,
        classificationTree: action.payload,
      };

    case "SET_CLASSIFICATION_LOADING":
      return { ...state, classificationLoading: action.payload };

    case "SELECT_CLASSIFICATION":
      return { ...state, selectedClassification: action.payload };

    case "SET_CLASSIFICATION_FILTERED_MODS":
      return { ...state, classificationFilteredMods: action.payload };

    case "SET_CLASSIFICATION_SEARCH":
      return { ...state, classificationSearch: action.payload };

    case "UPDATE_FILTERED_MOD": {
      const { sha, data, newCategory } = action.payload;
      const selectedCategoryId = state.selectedClassification?.id;

      // If category is being updated and there's a classification filter active
      const shouldRemoveFromFilteredList =
        newCategory !== undefined &&
        state.classificationFilteredMods !== null &&
        selectedCategoryId !== undefined &&
        newCategory !== selectedCategoryId;

      return {
        ...state,
        classificationFilteredMods: state.classificationFilteredMods
          ? shouldRemoveFromFilteredList
            ? // Remove mod from filtered list if it no longer matches the filter
              state.classificationFilteredMods.filter((mod) => mod.sha !== sha)
            : // Otherwise update the mod in the filtered list
              state.classificationFilteredMods.map((mod) =>
                mod.sha === sha ? { ...mod, ...data } : mod
              )
          : null,
      };
    }

    default:
      return state;
  }
}

// ============================================================================
// Initial State
// ============================================================================

export const initialClassificationState: ClassificationState = {
  classificationTree: [],
  classificationLoading: false,
  selectedClassification: null,
  classificationFilteredMods: null,
  classificationSearch: "",
};
