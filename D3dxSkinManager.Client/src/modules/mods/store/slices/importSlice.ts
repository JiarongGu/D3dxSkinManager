/**
 * Import slice - manages import task queue state
 */

import { ImportTask } from '../../components/AddModWindow';

export interface ImportSliceState {
  importTasks: ImportTask[];
  importProcessing: boolean;
  taskIdCounter: number;
  selectedTaskIds: string[];
}

export const initialImportState: ImportSliceState = {
  importTasks: [],
  importProcessing: false,
  taskIdCounter: 0,
  selectedTaskIds: [],
};

export interface ImportSliceActions {
  setImportTasks: (tasks: ImportTask[]) => void;
  setImportProcessing: (processing: boolean) => void;
  setSelectedTaskIds: (ids: string[]) => void;

  // Task management
  addImportTask: (task: Omit<ImportTask, 'id'>) => string;
  addImportTasks: (tasks: ImportTask[]) => void;
  updateImportTask: (taskId: string, updates: Partial<ImportTask>) => void;
  removeImportTask: (taskId: string) => void;
  clearImportTasks: () => void;

  // Batch operations
  updateMultipleTasks: (taskIds: string[], updates: Partial<ImportTask>) => void;

  // Reset
  reset: () => void;
}

export const createImportSliceActions = (
  set: (fn: (state: ImportSliceState) => Partial<ImportSliceState>) => void,
  get: () => ImportSliceState
): ImportSliceActions => ({
  setImportTasks: (tasks) => set(() => ({ importTasks: tasks })),

  setImportProcessing: (processing) => set(() => ({ importProcessing: processing })),

  setSelectedTaskIds: (ids) => set(() => ({ selectedTaskIds: ids })),

  addImportTask: (task) => {
    const state = get();
    const taskId = `TASK-${state.taskIdCounter + 1}`;
    const newTask: ImportTask = { ...task, id: taskId };

    set(() => ({
      importTasks: [...state.importTasks, newTask],
      taskIdCounter: state.taskIdCounter + 1,
    }));

    return taskId;
  },

  addImportTasks: (tasks) =>
    set((state) => ({
      importTasks: [...state.importTasks, ...tasks],
      taskIdCounter: state.taskIdCounter + tasks.length,
    })),

  updateImportTask: (taskId, updates) =>
    set((state) => ({
      importTasks: state.importTasks.map((task) =>
        task.id === taskId ? { ...task, ...updates } : task
      ),
    })),

  removeImportTask: (taskId) =>
    set((state) => ({
      importTasks: state.importTasks.filter((task) => task.id !== taskId),
      selectedTaskIds: state.selectedTaskIds.filter((id) => id !== taskId),
    })),

  clearImportTasks: () =>
    set(() => ({
      importTasks: [],
      selectedTaskIds: [],
    })),

  updateMultipleTasks: (taskIds, updates) =>
    set((state) => ({
      importTasks: state.importTasks.map((task) =>
        taskIds.includes(task.id) ? { ...task, ...updates } : task
      ),
    })),

  reset: () => set(() => initialImportState),
});
