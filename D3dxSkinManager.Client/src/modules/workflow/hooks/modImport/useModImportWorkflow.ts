/**
 * Hook for managing mod import workflow state and operations
 * Provides real-time updates via workflow events
 */

import { useState, useEffect } from 'react';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { workflowService } from '../../services/workflowService';
import { eventBus, Module, WorkflowEventType } from '../../../../shared/services/eventBus';
import type { WorkflowInfo } from '../../types/workflow.types';
import { handleError } from '../../../../shared/utils/errorHandler';

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
                    setWorkflow(event.payload);
        }
      }
    );

    const unsubStatusChanged = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.STATUS_CHANGED,
      (event) => {
        if (event?.payload && event.payload.id === workflowId) {
                    setWorkflow(event.payload);
        }
      }
    );

    const unsubCompleted = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.COMPLETED,
      (event) => {
        if (event?.payload && event.payload.id === workflowId) {
                    setWorkflow(event.payload);
        }
      }
    );

    const unsubFailed = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.FAILED,
      (event) => {
        if (event?.payload && event.payload.id === workflowId) {
                    setWorkflow(event.payload);
        }
      }
    );

    const unsubCancelled = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.CANCELLED,
      (event) => {
        if (event?.payload && event.payload.id === workflowId) {
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
            const newWorkflow = await workflowService.startModImport(selectedProfileId, folderPath);
            setWorkflow(newWorkflow);
    } catch (error) {
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
            throw new Error('No active workflow');
    }
    if (!selectedProfileId) {
      throw new Error('No profile selected');
    }

    setLoading(true);
    try {
            const updatedWorkflow = await workflowService.updateWorkflowContext(
        selectedProfileId,
        workflow.id,
        context
      );
            setWorkflow(updatedWorkflow);
    } catch (error) {
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
            throw new Error('No active workflow');
    }
    if (!selectedProfileId) {
      throw new Error('No profile selected');
    }

    setLoading(true);
    try {
            const updatedWorkflow = await workflowService.continueWorkflow(
        selectedProfileId,
        workflow.id
      );
            setWorkflow(updatedWorkflow);
    } catch (error) {
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
            return;
    }
    if (!selectedProfileId) {
      throw new Error('No profile selected');
    }

    setLoading(true);
    try {
            const cancelledWorkflow = await workflowService.cancelModImport(
        selectedProfileId,
        workflow.id
      );
            setWorkflow(cancelledWorkflow);
    } catch (error) {
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
