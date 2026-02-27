/**
 * Constants for chain types used throughout the TaskQueue system
 * Must match backend ChainTypes.cs
 */
export const ChainTypes = {
  /**
   * Interactive folder import chain with user metadata input
   * Workflow: COMPRESS_FOLDER → [AwaitingConfirmation] → IMPORT_FROM_TEMP
   */
  FOLDER_IMPORT: 'FOLDER_IMPORT',

  /**
   * Quick folder import chain without user interaction
   * Workflow: COMPRESS_FOLDER → [Auto] → IMPORT_FROM_TEMP
   */
  QUICK_FOLDER_IMPORT: 'QUICK_FOLDER_IMPORT',

  /**
   * Import with validation step before final import
   * Workflow: COMPRESS_FOLDER → validate → [UserReview] → IMPORT_FROM_TEMP
   */
  VALIDATED_IMPORT: 'VALIDATED_IMPORT',

  /**
   * Batch processing chain for multiple items
   * Workflow: configure → process_item_1 → process_item_2 → ... → complete
   */
  BATCH_PROCESSING: 'BATCH_PROCESSING'
} as const;

export type ChainType = typeof ChainTypes[keyof typeof ChainTypes];