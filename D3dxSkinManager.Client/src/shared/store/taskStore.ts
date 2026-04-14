/**
 * Global background task store.
 *
 * Any component can register a running task; AppStatusBar subscribes
 * and shows a spinner + count. Hover reveals task list.
 */

import { create } from 'zustand';

export interface BackgroundTask {
  id: string;
  /** Pre-translated display label (e.g. "Updating archive: Blue Hair") */
  label: string;
  /** 0-100 for determinate progress, undefined for indeterminate spinner */
  progress?: number;
}

interface TaskState {
  tasks: BackgroundTask[];
  addTask: (task: BackgroundTask) => void;
  updateTask: (id: string, updates: Partial<Omit<BackgroundTask, 'id'>>) => void;
  removeTask: (id: string) => void;
}

export const useTaskStore = create<TaskState>((set) => ({
  tasks: [],

  addTask: (task) =>
    set((state) => {
      // Idempotent: skip if task with same ID already exists
      if (state.tasks.some((t) => t.id === task.id)) return state;
      return { tasks: [...state.tasks, task] };
    }),

  updateTask: (id, updates) =>
    set((state) => ({
      tasks: state.tasks.map((t) =>
        t.id === id ? { ...t, ...updates } : t,
      ),
    })),

  removeTask: (id) =>
    set((state) => ({
      tasks: state.tasks.filter((t) => t.id !== id),
    })),
}));
