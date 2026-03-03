/**
 * Hook for managing workflow queue
 * Tracks multiple workflows and subscribes to their events
 */

import { useState, useEffect, useCallback } from 'react';
import { eventBus, Module, WorkflowEventType } from '../../../../shared/services/eventBus';
import { workflowService } from '../../services/workflowService';
import { useProfile } from '../../../../shared/context/ProfileContext';
import type { WorkflowInfo } from '../../types/workflow.types';
import { WorkflowStatus } from '../../types/workflow.types';

interface UseWorkflowQueueReturn {
  workflows: WorkflowInfo[];
  addWorkflow: (workflow: WorkflowInfo) => void;
  removeWorkflow: (workflowId: string) => void;
  updateWorkflow: (workflow: WorkflowInfo) => void;
  clearCompleted: () => void;
  refresh: () => void;
  isLoading: boolean;
}

/**
 * Hook for managing a queue of workflows
 * Automatically subscribes to workflow events for real-time updates
 */
export const useWorkflowQueue = (): UseWorkflowQueueReturn => {
  const { selectedProfileId } = useProfile();
  const [workflows, setWorkflows] = useState<WorkflowInfo[]>([]);
  const [isInitialized, setIsInitialized] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  /**
   * Add a workflow to the queue
   */
  const addWorkflow = useCallback((workflow: WorkflowInfo) => {
    setWorkflows((prev) => {
      // Check if workflow already exists
      const exists = prev.some((w) => w.id === workflow.id);
      if (exists) {
        return prev.map((w) => (w.id === workflow.id ? workflow : w));
      }
      return [...prev, workflow];
    });
  }, []);

  /**
   * Remove a workflow from the queue
   */
  const removeWorkflow = useCallback((workflowId: string) => {
    setWorkflows((prev) => prev.filter((w) => w.id !== workflowId));
  }, []);

  /**
   * Update a workflow in the queue
   */
  const updateWorkflow = useCallback((workflow: WorkflowInfo) => {
    setWorkflows((prev) => prev.map((w) => (w.id === workflow.id ? workflow : w)));
  }, []);

  /**
   * Clear all completed workflows
   */
  const clearCompleted = useCallback(() => {
    setWorkflows((prev) =>
      prev.filter(
        (w) => w.status !== WorkflowStatus.Completed &&
               w.status !== WorkflowStatus.Failed &&
               w.status !== WorkflowStatus.Cancelled
      )
    );
  }, []);

  /**
   * Load workflows from backend
   */
  const refresh = useCallback(async () => {
    if (!selectedProfileId) {
      return;
    }

    try {
      setIsLoading(true);
      const loadedWorkflows = await workflowService.getWorkflowsByType(
        selectedProfileId,
        'MOD_IMPORT'
      );
      setWorkflows(loadedWorkflows);
    } catch (error) {
      // Error handled by error handler
    } finally {
      setIsLoading(false);
    }
  }, [selectedProfileId]);

  /**
   * Load existing workflows on mount
   */
  useEffect(() => {
    if (selectedProfileId && !isInitialized) {
      refresh();
      setIsInitialized(true);
    }
  }, [selectedProfileId, isInitialized, refresh]);

  /**
   * Subscribe to workflow events for real-time updates
   */
  useEffect(() => {
    const unsubCreated = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.CREATED,
      (event) => {
        if (event?.payload) {
          addWorkflow(event.payload);
        }
      }
    );

    const unsubStatusChanged = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.STATUS_CHANGED,
      (event) => {
        if (event?.payload) {
          updateWorkflow(event.payload);
        }
      }
    );

    const unsubCompleted = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.COMPLETED,
      (event) => {
        if (event?.payload) {
          updateWorkflow(event.payload);
        }
      }
    );

    const unsubFailed = eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.FAILED, (event) => {
      if (event?.payload) {
        updateWorkflow(event.payload);
      }
    });

    const unsubCancelled = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.CANCELLED,
      (event) => {
        if (event?.payload) {
          updateWorkflow(event.payload);
        }
      }
    );

    const unsubProgress = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.PROGRESS,
      (event) => {
        if (event?.payload && event.payload.workflowId) {
          // Update the workflow in the queue by fetching latest state
          // Progress events contain { workflowId, progress, step }
          // We need to update the context in the workflow
          const { workflowId, progress, step } = event.payload;
          setWorkflows((prev) =>
            prev.map((w) => {
              if (w.id === workflowId) {
                try {
                  const context = JSON.parse(w.context);
                  context.progress = progress;
                  context.step = step;
                  return {
                    ...w,
                    context: JSON.stringify(context),
                  };
                } catch (error) {
                  // Error handled by error handler
                  return w;
                }
              }
              return w;
            })
          );
        }
      }
    );

    const unsubDeleted = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.DELETED,
      (event) => {
        if (event?.payload) {
          // Payload is the workflow ID (string)
          const workflowId = event.payload as string;
          removeWorkflow(workflowId);
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
      unsubProgress();
      unsubDeleted();
    };
  }, [addWorkflow, updateWorkflow, removeWorkflow]);

  return {
    workflows,
    addWorkflow,
    removeWorkflow,
    updateWorkflow,
    clearCompleted,
    refresh,
    isLoading,
  };
};
