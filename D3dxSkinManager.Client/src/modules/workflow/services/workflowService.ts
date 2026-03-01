import { BaseModuleService } from '../../../shared/services/baseModuleService';
import type { WorkflowInfo } from '../types/workflow.types';

/**
 * Workflow service for IPC communication with backend
 * Profile-scoped service for workflow operations
 */
class WorkflowService extends BaseModuleService {
  constructor() {
    super('WORKFLOW');
  }

  /**
   * Start a new mod import workflow
   */
  async startModImport(profileId: string, folderPath: string): Promise<WorkflowInfo> {
    return this.sendMessage<WorkflowInfo>('START_MOD_IMPORT', profileId, folderPath);
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
   * Continue workflow to next step
   */
  async continueWorkflow(profileId: string, workflowId: string): Promise<WorkflowInfo> {
    return this.sendMessage<WorkflowInfo>('CONTINUE_WORKFLOW', profileId, workflowId);
  }

  /**
   * Cancel a mod import workflow
   */
  async cancelModImport(profileId: string, workflowId: string): Promise<WorkflowInfo> {
    return this.sendMessage<WorkflowInfo>('CANCEL_MOD_IMPORT', profileId, workflowId);
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
}

export const workflowService = new WorkflowService();
