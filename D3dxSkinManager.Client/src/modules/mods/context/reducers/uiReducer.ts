import { ModInfo } from "../../../../shared/types/mod.types";
import { ImportTask } from "../../components/AddModWindow";

// ============================================================================
// State Types
// ============================================================================

export interface UIState {
  // Selection
  selectedObject: string;
  expandedKeys: React.Key[];
  searchQuery: string;

  // Dialog State
  editDialogVisible: boolean;
  tagDialogVisible: boolean;
  batchEditDialogVisible: boolean;
  importWindowVisible: boolean;
  addModUnitVisible: boolean;
  batchEditUnitVisible: boolean;

  // Edit State
  modToEdit: ModInfo | null;
  currentTags: string[];
  currentEditTask: ImportTask | null;
  selectedTaskIds: string[];
  tagDialogContext: "mod" | "import";
}

// ============================================================================
// Actions
// ============================================================================

export type UIAction =
  | { type: "SET_SELECTED_OBJECT"; payload: string }
  | { type: "SET_EXPANDED_KEYS"; payload: React.Key[] }
  | { type: "SET_SEARCH_QUERY"; payload: string }
  | { type: "OPEN_EDIT_DIALOG"; payload: ModInfo }
  | { type: "CLOSE_EDIT_DIALOG" }
  | {
      type: "OPEN_TAG_DIALOG";
      payload: { tags: string[]; context: "mod" | "import" };
    }
  | { type: "CLOSE_TAG_DIALOG" }
  | { type: "OPEN_BATCH_EDIT_DIALOG"; payload: ModInfo[] }
  | { type: "CLOSE_BATCH_EDIT_DIALOG" }
  | { type: "OPEN_IMPORT_WINDOW" }
  | { type: "CLOSE_IMPORT_WINDOW" }
  | { type: "OPEN_ADD_MOD_UNIT"; payload: ImportTask }
  | { type: "CLOSE_ADD_MOD_UNIT" }
  | { type: "OPEN_BATCH_EDIT_UNIT"; payload: string[] }
  | { type: "CLOSE_BATCH_EDIT_UNIT" }
  | { type: "SET_CURRENT_TAGS"; payload: string[] };

// ============================================================================
// Reducer
// ============================================================================

export function uiReducer(state: UIState, action: UIAction): UIState {
  switch (action.type) {
    case "SET_SELECTED_OBJECT":
      return { ...state, selectedObject: action.payload };

    case "SET_EXPANDED_KEYS":
      return { ...state, expandedKeys: action.payload };

    case "SET_SEARCH_QUERY":
      return { ...state, searchQuery: action.payload };

    case "OPEN_EDIT_DIALOG":
      return {
        ...state,
        editDialogVisible: true,
        modToEdit: action.payload,
      };

    case "CLOSE_EDIT_DIALOG":
      return {
        ...state,
        editDialogVisible: false,
        modToEdit: null,
      };

    case "OPEN_TAG_DIALOG":
      return {
        ...state,
        tagDialogVisible: true,
        currentTags: action.payload.tags,
        tagDialogContext: action.payload.context,
      };

    case "CLOSE_TAG_DIALOG":
      return {
        ...state,
        tagDialogVisible: false,
        currentTags: [],
      };

    case "OPEN_BATCH_EDIT_DIALOG":
      return {
        ...state,
        batchEditDialogVisible: true,
      };

    case "CLOSE_BATCH_EDIT_DIALOG":
      return {
        ...state,
        batchEditDialogVisible: false,
      };

    case "OPEN_IMPORT_WINDOW":
      return { ...state, importWindowVisible: true };

    case "CLOSE_IMPORT_WINDOW":
      return { ...state, importWindowVisible: false };

    case "OPEN_ADD_MOD_UNIT":
      return {
        ...state,
        addModUnitVisible: true,
        currentEditTask: action.payload,
      };

    case "CLOSE_ADD_MOD_UNIT":
      return {
        ...state,
        addModUnitVisible: false,
        currentEditTask: null,
      };

    case "OPEN_BATCH_EDIT_UNIT":
      return {
        ...state,
        batchEditUnitVisible: true,
        selectedTaskIds: action.payload,
      };

    case "CLOSE_BATCH_EDIT_UNIT":
      return { ...state, batchEditUnitVisible: false, selectedTaskIds: [] };

    case "SET_CURRENT_TAGS":
      return { ...state, currentTags: action.payload };

    default:
      return state;
  }
}

// ============================================================================
// Initial State
// ============================================================================

export const initialUIState: UIState = {
  selectedObject: "",
  expandedKeys: [],
  searchQuery: "",
  editDialogVisible: false,
  tagDialogVisible: false,
  batchEditDialogVisible: false,
  importWindowVisible: false,
  addModUnitVisible: false,
  batchEditUnitVisible: false,
  modToEdit: null,
  currentTags: [],
  currentEditTask: null,
  selectedTaskIds: [],
  tagDialogContext: "mod",
};
