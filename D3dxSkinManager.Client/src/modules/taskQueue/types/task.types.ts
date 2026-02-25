/**
 * Task execution status
 */
export type TaskStatus = 'pending' | 'processing' | 'completed' | 'failed' | 'cancelled' | 'awaitingConfirmation';

/**
 * File type for import tasks
 */
export type FileType = 'archive' | 'folder';

/**
 * Task information from backend
 */
export interface TaskInfo {
  id: string;
  type: string;
  status: TaskStatus;
  progress: number;
  message?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  inputData: string;
  outputData?: string;
  errorMessage?: string;
  operationId?: string;
  profileId?: string;
  correlationId?: string;
  chainPhase?: number;
  nextTaskType?: string;
}

/**
 * Task progress update
 */
export interface TaskProgress {
  taskId: string;
  progress: number;
  currentStep?: string;
  message?: string;
}

/**
 * Mod import task input
 */
export interface ModImportTaskInput {
  filePath: string;
  isFolder: boolean;
  profileId?: string;
  name?: string;
  author?: string;
  description?: string;
  grading?: string;
  tags?: string[];
  category?: string;
}

/**
 * Mod import task output
 */
export interface ModImportTaskOutput {
  sha: string;
  name: string;
  success: boolean;
  errorMessage?: string;
}
