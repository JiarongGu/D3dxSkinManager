/**
 * Constants for task types used throughout the TaskQueue system
 * Must match backend TaskTypes.cs
 */
export const TaskTypes = {
  /**
   * Mod import task - imports a mod from file or folder
   */
  MOD_IMPORT: 'MOD_IMPORT',

  /**
   * Compress folder task - compresses a folder to temporary archive
   */
  COMPRESS_FOLDER: 'COMPRESS_FOLDER',

  /**
   * Import from temp task - imports mod from temporary archive with metadata
   */
  IMPORT_FROM_TEMP: 'IMPORT_FROM_TEMP'
} as const;

export type TaskType = typeof TaskTypes[keyof typeof TaskTypes];