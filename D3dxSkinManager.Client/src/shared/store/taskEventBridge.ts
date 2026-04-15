/**
 * Global bridge: backend progress events → taskStore.
 *
 * Call `initTaskEventBridge()` once at app startup.
 * This ensures background tasks stay visible in the status bar
 * even when the originating tool screen is closed.
 *
 * Components still call addTask() to register a task initially.
 * This bridge handles progress updates and completion cleanup.
 */

import { eventBus, Module, ToolsEventType } from '../services/eventBus';
import { useTaskStore } from './taskStore';

const ANALYSIS_TASK_ID = 'mod-analysis';
const PACKAGE_TASK_ID = 'mod-package';
const MOD_ID_MIGRATION_TASK_ID = 'mod-id-migration';

export function initTaskEventBridge(): () => void {
  const unsubAnalysisProgress = eventBus.subscribe(
    Module.TOOL,
    ToolsEventType.MOD_ANALYSIS_PROGRESS,
    (e) => {
      const payload = e.payload;
      if (!payload) return;

      const store = useTaskStore.getState();
      const pct = payload.total > 0
        ? Math.round((payload.current / payload.total) * 100)
        : undefined;

      // Only update progress — component or resumeRunningSession handles addTask with proper label
      if (store.tasks.some((t) => t.id === ANALYSIS_TASK_ID)) {
        store.updateTask(ANALYSIS_TASK_ID, { progress: pct });
      }
    },
  );

  const unsubAnalysisComplete = eventBus.subscribe(
    Module.TOOL,
    ToolsEventType.MOD_ANALYSIS_COMPLETE,
    (e) => {
      if (e.payload?.status === 'running') return;
      useTaskStore.getState().removeTask(ANALYSIS_TASK_ID);
    },
  );

  const unsubPackageProgress = eventBus.subscribe(
    Module.TOOL,
    ToolsEventType.MOD_PACKAGE_PROGRESS,
    (e) => {
      if (!e.payload) return;

      const store = useTaskStore.getState();
      const pct = e.payload.total > 0
        ? Math.round((e.payload.current / e.payload.total) * 100)
        : undefined;

      if (store.tasks.some((t) => t.id === PACKAGE_TASK_ID)) {
        store.updateTask(PACKAGE_TASK_ID, { progress: pct });
      }
    },
  );

  const unsubMigrationProgress = eventBus.subscribe(
    Module.TOOL,
    ToolsEventType.MOD_ID_MIGRATION_PROGRESS,
    (e) => {
      if (!e.payload) return;

      const store = useTaskStore.getState();
      const pct = e.payload.total > 0
        ? Math.round((e.payload.current / e.payload.total) * 100)
        : undefined;

      if (store.tasks.some((t) => t.id === MOD_ID_MIGRATION_TASK_ID)) {
        store.updateTask(MOD_ID_MIGRATION_TASK_ID, { progress: pct });
      }
    },
  );

  const unsubMigrationComplete = eventBus.subscribe(
    Module.TOOL,
    ToolsEventType.MOD_ID_MIGRATION_COMPLETE,
    () => {
      useTaskStore.getState().removeTask(MOD_ID_MIGRATION_TASK_ID);
    },
  );

  return () => {
    unsubAnalysisProgress();
    unsubAnalysisComplete();
    unsubPackageProgress();
    unsubMigrationProgress();
    unsubMigrationComplete();
  };
}
