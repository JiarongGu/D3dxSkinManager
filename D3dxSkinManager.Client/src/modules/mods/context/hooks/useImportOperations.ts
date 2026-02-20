import { notification } from '../../../../shared/utils/notification';
import { useReducer, useCallback, Dispatch } from "react";
import { ModInfo } from "../../../../shared/types/mod.types";
import { modService } from "../../services/modService";
import { ImportTask, TaskStatus } from "../../components/AddModWindow";
import {
  importReducer,
  initialImportState,
  ImportState,
  ImportAction,
} from "../reducers/importReducer";

export interface UseImportOperationsReturn {
  // State
  state: ImportState;
  dispatch: Dispatch<ImportAction>;

  // Actions
  importMod: (profileId: string, task: ImportTask) => Promise<ModInfo | null>;
  importMods: (
    profileId: string,
    tasks: ImportTask[],
    onSuccess: () => Promise<void>,
    onCloseWindow: () => void
  ) => Promise<void>;
  addImportTasks: (tasks: ImportTask[]) => void;
  updateImportTask: (id: string, updates: Partial<ImportTask>) => void;
  removeImportTask: (id: string) => void;
  clearImportTasks: () => void;
  getNextTaskId: () => string;
}

export function useImportOperations(): UseImportOperationsReturn {
  const [state, dispatch] = useReducer(importReducer, initialImportState);

  const importMod = useCallback(
    async (profileId: string, task: ImportTask): Promise<ModInfo | null> => {
      if (!profileId) {
        return null;
      }
      try {
        dispatch({
          type: "UPDATE_IMPORT_TASK",
          payload: {
            id: task.id,
            updates: { status: "processing" as TaskStatus, progress: 30 },
          },
        });

        const importedMod = await modService.importMod(profileId, task.filePath);

        dispatch({
          type: "UPDATE_IMPORT_TASK",
          payload: { id: task.id, updates: { progress: 60 } },
        });

        if (
          task.modData.name ||
          task.modData.author ||
          task.modData.tags ||
          task.modData.grading
        ) {
          await modService.updateMetadata(profileId, importedMod.sha, {
            name: task.modData.name,
            author: task.modData.author,
            tags: task.modData.tags,
            grading: task.modData.grading,
            description: task.modData.description,
          });
        }

        dispatch({
          type: "UPDATE_IMPORT_TASK",
          payload: {
            id: task.id,
            updates: {
              status: "success" as TaskStatus,
              progress: 100,
              message: "Import successful",
            },
          },
        });

        return importedMod;
      } catch (error) {
        const errorMessage =
          error instanceof Error ? error.message : "Import failed";
        dispatch({
          type: "UPDATE_IMPORT_TASK",
          payload: {
            id: task.id,
            updates: { status: "error" as TaskStatus, message: errorMessage },
          },
        });
        throw error;
      }
    },
    []
  );

  const importMods = useCallback(
    async (
      profileId: string,
      tasks: ImportTask[],
      onSuccess: () => Promise<void>,
      onCloseWindow: () => void
    ) => {
      dispatch({ type: "SET_IMPORT_PROCESSING", payload: true });
      let successCount = 0;
      let errorCount = 0;

      for (const task of tasks) {
        if (task.status !== "pending") continue;

        try {
          await importMod(profileId, task);
          successCount++;
        } catch (error) {
          errorCount++;
        }
      }

      dispatch({ type: "SET_IMPORT_PROCESSING", payload: false });

      if (errorCount === 0) {
        notification.success(`Successfully imported ${successCount} mod(s)`);
      } else if (successCount > 0) {
        notification.warning(
          `Imported ${successCount} mod(s), ${errorCount} failed`
        );
      } else {
        notification.error("Failed to import all mods");
      }

      if (successCount > 0) {
        await onSuccess();
      }

      if (errorCount === 0) {
        setTimeout(() => {
          onCloseWindow();
          dispatch({ type: "CLEAR_IMPORT_TASKS" });
        }, 2000);
      }
    },
    [importMod]
  );

  const addImportTasks = useCallback((tasks: ImportTask[]) => {
    dispatch({ type: "ADD_IMPORT_TASKS", payload: tasks });
  }, []);

  const updateImportTask = useCallback(
    (id: string, updates: Partial<ImportTask>) => {
      dispatch({ type: "UPDATE_IMPORT_TASK", payload: { id, updates } });
    },
    []
  );

  const removeImportTask = useCallback((id: string) => {
    dispatch({ type: "REMOVE_IMPORT_TASK", payload: id });
  }, []);

  const clearImportTasks = useCallback(() => {
    dispatch({ type: "CLEAR_IMPORT_TASKS" });
  }, []);

  const getNextTaskId = useCallback(() => {
    const id = `TASK-${state.taskIdCounter}`;
    dispatch({ type: "INCREMENT_TASK_ID" });
    return id;
  }, [state.taskIdCounter]);

  return {
    state,
    dispatch,
    importMod,
    importMods,
    addImportTasks,
    updateImportTask,
    removeImportTask,
    clearImportTasks,
    getNextTaskId,
  };
}
