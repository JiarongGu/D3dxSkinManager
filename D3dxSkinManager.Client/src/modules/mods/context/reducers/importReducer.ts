import { ImportTask } from "../../components/AddModWindow";

// ============================================================================
// State Types
// ============================================================================

export interface ImportState {
  importTasks: ImportTask[];
  importProcessing: boolean;
  taskIdCounter: number;
}

// ============================================================================
// Actions
// ============================================================================

export type ImportAction =
  | { type: "ADD_IMPORT_TASKS"; payload: ImportTask[] }
  | {
      type: "UPDATE_IMPORT_TASK";
      payload: { id: string; updates: Partial<ImportTask> };
    }
  | { type: "REMOVE_IMPORT_TASK"; payload: string }
  | { type: "CLEAR_IMPORT_TASKS" }
  | { type: "SET_IMPORT_PROCESSING"; payload: boolean }
  | { type: "INCREMENT_TASK_ID" };

// ============================================================================
// Reducer
// ============================================================================

export function importReducer(
  state: ImportState,
  action: ImportAction
): ImportState {
  switch (action.type) {
    case "ADD_IMPORT_TASKS":
      return {
        ...state,
        importTasks: [...state.importTasks, ...action.payload],
      };

    case "UPDATE_IMPORT_TASK":
      return {
        ...state,
        importTasks: state.importTasks.map((task) =>
          task.id === action.payload.id
            ? { ...task, ...action.payload.updates }
            : task
        ),
      };

    case "REMOVE_IMPORT_TASK":
      return {
        ...state,
        importTasks: state.importTasks.filter(
          (task) => task.id !== action.payload
        ),
      };

    case "CLEAR_IMPORT_TASKS":
      return { ...state, importTasks: [] };

    case "SET_IMPORT_PROCESSING":
      return { ...state, importProcessing: action.payload };

    case "INCREMENT_TASK_ID":
      return { ...state, taskIdCounter: state.taskIdCounter + 1 };

    default:
      return state;
  }
}

// ============================================================================
// Initial State
// ============================================================================

export const initialImportState: ImportState = {
  importTasks: [],
  importProcessing: false,
  taskIdCounter: 1,
};
