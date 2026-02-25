import { ModInfo } from '../../../shared/types/mod.types';

/**
 * Task status aligned with backend OperationStatus
 */
export type TaskStatus =
  | 'pending'      // Not yet started
  | 'processing'   // Currently being imported (backend is working)
  | 'success'      // Import completed successfully
  | 'error'        // Import failed
  | 'cancelled';   // User cancelled

/**
 * File type for import source
 */
export type FileType = 'archive' | 'folder';

/**
 * Import task - represents a mod waiting to be imported
 *
 * Frontend-only entity for tracking import queue.
 * Backend processes imports synchronously and emits MOD_IMPORTED event.
 */
export interface ImportTask {
  // Task identification
  id: string;                    // Frontend-generated: TASK-1, TASK-2, etc.

  // Source file info
  filePath: string;              // Absolute path to archive or folder
  fileName: string;              // Display name (e.g., "MyMod.zip")
  fileType: FileType;            // Archive (.zip, .rar, .7z) or folder
  fileSize?: number;             // File size in bytes (for progress estimation)

  // Task state
  status: TaskStatus;            // Current status
  progress: number;              // 0-100 (estimated for now, real progress when backend supports it)
  message?: string;              // Status message or error details

  // Timestamps
  createdAt: Date;               // When task was added to queue
  startedAt?: Date;              // When import started
  completedAt?: Date;            // When import finished (success or error)

  // Mod metadata (user can edit before import)
  modData: Partial<ModInfo>;     // Metadata to use for import

  // Preview
  thumbnailUrl?: string;         // Preview image URL (if available)

  // Backend operation tracking
  operationId?: string;          // If backend supports progress reporting
  importedModSha?: string;       // SHA of imported mod (set when MOD_IMPORTED event received)
}

/**
 * Import statistics for status bar
 */
export interface ImportStats {
  total: number;
  pending: number;
  processing: number;
  success: number;
  error: number;
  cancelled: number;
}

/**
 * Helper to calculate import stats
 */
export function calculateImportStats(tasks: ImportTask[]): ImportStats {
  return {
    total: tasks.length,
    pending: tasks.filter(t => t.status === 'pending').length,
    processing: tasks.filter(t => t.status === 'processing').length,
    success: tasks.filter(t => t.status === 'success').length,
    error: tasks.filter(t => t.status === 'error').length,
    cancelled: tasks.filter(t => t.status === 'cancelled').length,
  };
}
