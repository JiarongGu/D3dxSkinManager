/**
 * Workflow module types
 * Simple types for the new stateless workflow system
 */

export enum WorkflowStatus {
  Pending = "pending",
  Processing = "processing",
  WaitingForInput = "waitingForInput",
  Completed = "completed",
  Failed = "failed",
  Cancelled = "cancelled",
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
  progress: number; // 0-100
  // Metadata fields (user can edit these)
  name?: string;
  author?: string;
  description?: string;
  category?: string; // Category ID
  categoryName?: string; // Category name (for display)
  tags: string[];
  grading: string;
  importedModSha?: string;
}

export const ModImportWorkflowSteps = {
  ExtractMetadata: 'extract_metadata',
  CompressFolder: 'compress_folder',
  ImportMod: 'import_mod',
} as const;

export const WorkflowTypes = {
  ModImport: 'MOD_IMPORT',
} as const;
