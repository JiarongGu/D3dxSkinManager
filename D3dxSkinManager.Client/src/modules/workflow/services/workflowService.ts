import { BaseModuleService } from '../../../shared/services/baseModuleService';
import type { WorkflowInfo } from '../types/workflow.types';

export interface BatchOperationResult {
  totalRequested: number;
  successful: string[];
  failed: Array<{
    workflowId: string;
    error: string;
  }>;
}

/**
 * Workflow service for IPC communication with backend
 * Profile-scoped service for workflow operations
 */
class WorkflowService extends BaseModuleService {
  constructor() {
    super('WORKFLOW');
  }

  /**
   * Create a new workflow (generic)
   */
  async createWorkflow(profileId: string, type: string, initialData: string): Promise<WorkflowInfo> {
    return this.sendMessage<WorkflowInfo>('CREATE_WORKFLOW', profileId, {
      type,
      initialData,
    });
  }

  /**
   * Start a new mod import workflow (convenience method)
   * @param profileId - The profile ID
   * @param folderPath - Path to folder or archive file to import
   * @param defaultCategory - Optional category name to pre-fill (from selected category in UI)
   */
  async startModImport(profileId: string, folderPath: string, defaultCategory?: string): Promise<WorkflowInfo> {
    // If defaultCategory is provided, encode it in the initialData as JSON
    const initialData = defaultCategory
      ? JSON.stringify({ folderPath, defaultCategory })
      : folderPath;

    return this.createWorkflow(profileId, 'MOD_IMPORT', initialData);
  }

  /**
   * Update workflow context (partial update of metadata fields)
   */
  async updateWorkflowContext(
    profileId: string,
    workflowId: string,
    context: Record<string, unknown>
  ): Promise<WorkflowInfo> {
    return this.sendMessage<WorkflowInfo>('UPDATE_WORKFLOW_CONTEXT', profileId, {
      workflowId,
      context,
    });
  }

  /**
   * Resume workflow (generic)
   */
  async resumeWorkflow(profileId: string, workflowId: string): Promise<WorkflowInfo> {
    return this.sendMessage<WorkflowInfo>('RESUME_WORKFLOW', profileId, workflowId);
  }

  /**
   * Continue workflow to next step (alias for resumeWorkflow)
   */
  async continueWorkflow(profileId: string, workflowId: string): Promise<WorkflowInfo> {
    return this.resumeWorkflow(profileId, workflowId);
  }

  /**
   * Pause workflow (generic)
   */
  async pauseWorkflow(profileId: string, workflowId: string): Promise<WorkflowInfo> {
    return this.sendMessage<WorkflowInfo>('PAUSE_WORKFLOW', profileId, workflowId);
  }

  /**
   * Cancel a mod import workflow (alias for pauseWorkflow)
   */
  async cancelModImport(profileId: string, workflowId: string): Promise<WorkflowInfo> {
    return this.pauseWorkflow(profileId, workflowId);
  }

  /**
   * Get a workflow by ID
   */
  async getWorkflow(profileId: string, workflowId: string): Promise<WorkflowInfo | null> {
    return this.sendMessage<WorkflowInfo | null>('GET_WORKFLOW', profileId, workflowId);
  }

  /**
   * Get all workflows of a specific type
   */
  async getWorkflowsByType(profileId: string, type: string): Promise<WorkflowInfo[]> {
    return this.sendArrayMessage<WorkflowInfo>('GET_WORKFLOWS_BY_TYPE', profileId, type);
  }

  /**
   * Delete a workflow
   */
  async deleteWorkflow(profileId: string, workflowId: string): Promise<boolean> {
    return this.sendMessage<boolean>('DELETE_WORKFLOW', profileId, workflowId);
  }

  /**
   * Batch delete workflows (with temp file cleanup)
   */
  async batchDeleteWorkflows(profileId: string, workflowIds: string[]): Promise<BatchOperationResult> {
    return this.sendMessage<BatchOperationResult>('BATCH_DELETE_WORKFLOWS', profileId, {
      workflowIds,
    });
  }

  /**
   * Batch resume workflows (only workflows in WaitingForInput status)
   */
  async batchResumeWorkflows(profileId: string, workflowIds: string[]): Promise<BatchOperationResult> {
    return this.sendMessage<BatchOperationResult>('BATCH_RESUME_WORKFLOWS', profileId, {
      workflowIds,
    });
  }

  /**
   * Start multiple mod import workflows from a batch of files/folders
   * @param profileId - The profile ID
   * @param paths - Array of file/folder paths to import
   * @param defaultCategory - Optional category name to pre-fill (from selected category in UI)
   * @returns Array of created workflows
   */
  async batchStartModImport(
    profileId: string,
    paths: string[],
    defaultCategory?: string
  ): Promise<WorkflowInfo[]> {
    const workflows: WorkflowInfo[] = [];

    for (const path of paths) {
      try {
        const workflow = await this.startModImport(profileId, path, defaultCategory);
        workflows.push(workflow);
      } catch (error: unknown) {
                // Continue with other imports even if one fails
      }
    }

    return workflows;
  }
}

export const workflowService = new WorkflowService();
