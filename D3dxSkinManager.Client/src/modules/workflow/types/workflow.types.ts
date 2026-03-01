/**
 * Workflow module types
 * Simple types for the new stateless workflow system
 */

export enum WorkflowStatus {
  Pending = 0,
  Processing = 1,
  WaitingForInput = 2,
  Completed = 3,
  Failed = 4,
  Cancelled = 5,
}

export interface WorkflowInfo {
  id: string;
  type: string;
  status: WorkflowStatus;
  context: string; // JSON string
  errorMessage?: string;
  createdAt: string;
  completedAt?: string;
}

/**
 * ModImport workflow specific types
 */
export interface ModImportWorkflowContext {
  step: string;
  folderPath?: string;
  tempArchivePath?: string;
  folderName?: string;
  fileCount?: number;
  // Metadata fields (user can edit these)
  name?: string;
  author?: string;
  description?: string;
  category?: string;
  tags: string[];
  grading: string;
  importedModSha?: string;
}

export const ModImportWorkflowSteps = {
  ExtractMetadata: 'extract_metadata',
  WaitingForUserConfirmation: 'waiting_for_user_confirmation',
  CompressFolder: 'compress_folder',
  ImportMod: 'import_mod',
  Completed: 'completed',
} as const;

export const WorkflowTypes = {
  ModImport: 'MOD_IMPORT',
} as const;
