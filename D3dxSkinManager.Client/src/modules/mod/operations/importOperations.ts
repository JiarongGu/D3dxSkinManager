/**
 * Import operations - mod import workflow management
 */

import { useModsStore } from '../store/modsStore';
import { ImportTask } from '../types/importTask.types';
import { ModInfo } from '../../../shared/types/mod.types';
import { notification } from '../../../shared/utils/notification';
import { handleError } from '../../../shared/utils/errorHandler';
import { refreshMods } from './modOperations';
import { modService } from '../../../shared/services/ipc';

/**
 * Add import task to queue
 */
export function addImportTask(task: Omit<ImportTask, 'id'>): string {
  return useModsStore.getState().addImportTask(task);
}

/**
 * Update import task
 */
export function updateImportTask(taskId: string, updates: Partial<ImportTask>): void {
  useModsStore.getState().updateImportTask(taskId, updates);
}

/**
 * Remove import task
 */
export function removeImportTask(taskId: string): void {
  useModsStore.getState().removeImportTask(taskId);
}

/**
 * Import a single mod
 */
export async function importMod(
  profileId: string,
  task: ImportTask
): Promise<ModInfo | undefined> {
  try {
    const mod = await modService.importMod(profileId, task.filePath);

    // Update the imported mod with metadata from task
    if (mod && task.modData) {
      await modService.updateMetadata(profileId, mod.sha, {
        name: task.modData.name,
        author: task.modData.author,
        tags: task.modData.tags,
        grading: task.modData.grading,
        description: task.modData.description,
      });
    }

    notification.success(`Imported ${task.fileName} successfully`);
    return mod;
  } catch (error: unknown) {
    handleError(error);
    throw error;
  }
}

/**
 * Import multiple mods (batch import)
 */
export async function importMods(
  profileId: string,
  tasks: ImportTask[],
  onComplete?: () => void,
  onClose?: () => void
): Promise<void> {
  const { setImportProcessing, updateImportTask, clearImportTasks } = useModsStore.getState();

  setImportProcessing(true);

  let successCount = 0;
  let failCount = 0;

  for (const task of tasks) {
    try {
      // Update task status to processing
      updateImportTask(task.id, {
        status: 'processing',
        progress: 0,
        message: 'Importing...',
      });

      // Import the mod
      const mod = await modService.importMod(profileId, task.filePath);

      // Update the imported mod with metadata from task
      if (mod && task.modData) {
        await modService.updateMetadata(profileId, mod.sha, {
          name: task.modData.name,
          author: task.modData.author,
          tags: task.modData.tags,
          grading: task.modData.grading,
          description: task.modData.description,
        });
      }

      // Update task status to success
      updateImportTask(task.id, {
        status: 'success',
        progress: 100,
        message: 'Import successful',
      });

      successCount++;
    } catch (error: unknown) {
      // Update task status to error
      const errorMessage = error instanceof Error ? error.message : 'Import failed';
      updateImportTask(task.id, {
        status: 'error',
        progress: 0,
        message: errorMessage,
      });

      failCount++;
    }
  }

  setImportProcessing(false);

  // Show summary notification
  if (successCount > 0 && failCount === 0) {
    notification.success(`Imported ${successCount} mod(s) successfully`);
  } else if (successCount > 0 && failCount > 0) {
    notification.warning(
      `Imported ${successCount} mod(s), ${failCount} failed`
    );
  } else {
    notification.error(`Failed to import ${failCount} mod(s)`);
  }

  // Refresh mods list
  if (onComplete) {
    onComplete();
  } else {
    await refreshMods(profileId);
  }

  // Clear tasks and close window
  clearImportTasks();
  if (onClose) {
    onClose();
  }
}

/**
 * Clear all import tasks
 */
export function clearImportTasks(): void {
  useModsStore.getState().clearImportTasks();
}

/**
 * Update multiple import tasks
 */
export function updateMultipleTasks(
  taskIds: string[],
  updates: Partial<ImportTask>
): void {
  useModsStore.getState().updateMultipleTasks(taskIds, updates);
}
