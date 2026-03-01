/**
 * Hook for managing mod import workflow state and operations
 * Provides real-time updates via workflow events
 */

import { useState, useEffect } from 'react';
import { useProfile } from '../../../shared/context/ProfileContext';
import { workflowService } from '../services/workflowService';
import { eventBus, Module, WorkflowEventType } from '../../../shared/services/eventBus';
import type { WorkflowInfo } from '../types/workflow.types';
import { handleError } from '../../../shared/utils/errorHandler';

// Legacy metadata type (kept for backward compatibility)
interface ModImportMetadata {
  name: string;
  author?: string;
  description?: string;
  category: string;
  tags: string[];
  grading: string;
}

interface UseModImportWorkflowReturn {
  workflow: WorkflowInfo | null;
  loading: boolean;
  startImport: (folderPath: string) => Promise<void>;
  updateContext: (context: Record<string, unknown>) => Promise<void>;
  continueWorkflow: () => Promise<void>;
  cancelImport: () => Promise<void>;
  clearWorkflow: () => void;
}

/**
 * Hook for managing mod import workflow
 * Automatically subscribes to workflow events for real-time updates
 */
export const useModImportWorkflow = (): UseModImportWorkflowReturn => {
  const { selectedProfileId } = useProfile();
  const [workflow, setWorkflow] = useState<WorkflowInfo | null>(null);
  const [loading, setLoading] = useState(false);

  /**
   * Subscribe to workflow events for real-time updates
   */
  useEffect(() => {
    if (!workflow) return;

    const workflowId = workflow.id;

    // Subscribe to all workflow events
    const unsubCreated = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.CREATED,
      (event) => {
        if (event?.payload && event.payload.id === workflowId) {
          console.log('[useModImportWorkflow] Workflow created:', event.payload);
          setWorkflow(event.payload);
        }
      }
    );

    const unsubStatusChanged = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.STATUS_CHANGED,
      (event) => {
        if (event?.payload && event.payload.id === workflowId) {
          console.log('[useModImportWorkflow] Workflow status changed:', event.payload);
          setWorkflow(event.payload);
        }
      }
    );

    const unsubCompleted = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.COMPLETED,
      (event) => {
        if (event?.payload && event.payload.id === workflowId) {
          console.log('[useModImportWorkflow] Workflow completed:', event.payload);
          setWorkflow(event.payload);
        }
      }
    );

    const unsubFailed = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.FAILED,
      (event) => {
        if (event?.payload && event.payload.id === workflowId) {
          console.log('[useModImportWorkflow] Workflow failed:', event.payload);
          setWorkflow(event.payload);
        }
      }
    );

    const unsubCancelled = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.CANCELLED,
      (event) => {
        if (event?.payload && event.payload.id === workflowId) {
          console.log('[useModImportWorkflow] Workflow cancelled:', event.payload);
          setWorkflow(event.payload);
        }
      }
    );

    // Cleanup subscriptions
    return () => {
      unsubCreated();
      unsubStatusChanged();
      unsubCompleted();
      unsubFailed();
      unsubCancelled();
    };
  }, [workflow?.id]);

  /**
   * Start a new mod import workflow
   */
  const startImport = async (folderPath: string): Promise<void> => {
    if (!selectedProfileId) {
      throw new Error('No profile selected');
    }

    setLoading(true);
    try {
      console.log('[useModImportWorkflow] Starting mod import for:', folderPath);
      const newWorkflow = await workflowService.startModImport(selectedProfileId, folderPath);
      console.log('[useModImportWorkflow] Workflow started:', newWorkflow);
      setWorkflow(newWorkflow);
    } catch (error) {
      console.error('[useModImportWorkflow] Failed to start import:', error);
      handleError(error);
      throw error;
    } finally {
      setLoading(false);
    }
  };

  /**
   * Update workflow context (metadata fields)
   */
  const updateContext = async (context: Record<string, unknown>): Promise<void> => {
    if (!workflow) {
      console.error('[useModImportWorkflow] No active workflow');
      throw new Error('No active workflow');
    }
    if (!selectedProfileId) {
      throw new Error('No profile selected');
    }

    setLoading(true);
    try {
      console.log('[useModImportWorkflow] Updating context:', context);
      const updatedWorkflow = await workflowService.updateWorkflowContext(
        selectedProfileId,
        workflow.id,
        context
      );
      console.log('[useModImportWorkflow] Context updated:', updatedWorkflow);
      setWorkflow(updatedWorkflow);
    } catch (error) {
      console.error('[useModImportWorkflow] Failed to update context:', error);
      handleError(error);
      throw error;
    } finally {
      setLoading(false);
    }
  };

  /**
   * Continue workflow to next step
   */
  const continueWorkflow = async (): Promise<void> => {
    if (!workflow) {
      console.error('[useModImportWorkflow] No active workflow');
      throw new Error('No active workflow');
    }
    if (!selectedProfileId) {
      throw new Error('No profile selected');
    }

    setLoading(true);
    try {
      console.log('[useModImportWorkflow] Continuing workflow');
      const updatedWorkflow = await workflowService.continueWorkflow(
        selectedProfileId,
        workflow.id
      );
      console.log('[useModImportWorkflow] Workflow continued:', updatedWorkflow);
      setWorkflow(updatedWorkflow);
    } catch (error) {
      console.error('[useModImportWorkflow] Failed to provide metadata:', error);
      handleError(error);
      throw error;
    } finally {
      setLoading(false);
    }
  };

  /**
   * Cancel the current workflow
   */
  const cancelImport = async (): Promise<void> => {
    if (!workflow) {
      console.error('[useModImportWorkflow] No active workflow to cancel');
      return;
    }
    if (!selectedProfileId) {
      throw new Error('No profile selected');
    }

    setLoading(true);
    try {
      console.log('[useModImportWorkflow] Cancelling workflow:', workflow.id);
      const cancelledWorkflow = await workflowService.cancelModImport(
        selectedProfileId,
        workflow.id
      );
      console.log('[useModImportWorkflow] Workflow cancelled:', cancelledWorkflow);
      setWorkflow(cancelledWorkflow);
    } catch (error) {
      console.error('[useModImportWorkflow] Failed to cancel workflow:', error);
      handleError(error);
      throw error;
    } finally {
      setLoading(false);
    }
  };

  /**
   * Clear the current workflow from state
   */
  const clearWorkflow = (): void => {
    console.log('[useModImportWorkflow] Clearing workflow');
    setWorkflow(null);
  };

  return {
    workflow,
    loading,
    startImport,
    updateContext,
    continueWorkflow,
    cancelImport,
    clearWorkflow,
  };
};
