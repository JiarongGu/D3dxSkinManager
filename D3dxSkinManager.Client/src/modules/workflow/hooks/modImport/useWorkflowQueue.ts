/**
 * Hook for managing workflow queue
 * Tracks multiple workflows and subscribes to their events
 */

import { useState, useEffect, useCallback, useMemo } from 'react';
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
  // Use Map for O(1) individual workflow updates instead of O(n) array operations
  const [workflowMap, setWorkflowMap] = useState<Map<string, WorkflowInfo>>(new Map());
  const [isInitialized, setIsInitialized] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  // Convert map to array for consumers (memoized to only change when Map changes)
  // This prevents table from re-parsing ALL workflows when only one workflow updates
  const workflows = useMemo(() => Array.from(workflowMap.values()), [workflowMap]);

  /**
   * Add a workflow to the queue (or update if exists)
   */
  const addWorkflow = useCallback((workflow: WorkflowInfo) => {
    setWorkflowMap((prev) => {
      const newMap = new Map(prev);
      newMap.set(workflow.id, workflow);
      return newMap;
    });
  }, []);

  /**
   * Remove a workflow from the queue
   */
  const removeWorkflow = useCallback((workflowId: string) => {
    setWorkflowMap((prev) => {
      const newMap = new Map(prev);
      newMap.delete(workflowId);
      return newMap;
    });
  }, []);

  /**
   * Update a workflow in the queue
   */
  const updateWorkflow = useCallback((workflow: WorkflowInfo) => {
    setWorkflowMap((prev) => {
      const newMap = new Map(prev);
      newMap.set(workflow.id, workflow);
      return newMap;
    });
  }, []);

  /**
   * Clear all completed workflows
   */
  const clearCompleted = useCallback(() => {
    setWorkflowMap((prev) => {
      const newMap = new Map<string, WorkflowInfo>();
      prev.forEach((workflow, id) => {
        if (
          workflow.status !== WorkflowStatus.Completed &&
          workflow.status !== WorkflowStatus.Failed &&
          workflow.status !== WorkflowStatus.Cancelled
        ) {
          newMap.set(id, workflow);
        }
      });
      return newMap;
    });
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
      // Convert array to Map
      const newMap = new Map<string, WorkflowInfo>();
      loadedWorkflows.forEach((workflow) => {
        newMap.set(workflow.id, workflow);
      });
      setWorkflowMap(newMap);
    } catch (error: unknown) {
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
          const { workflowId, progress, step } = event.payload;

          // Update only the specific workflow - no array iteration!
          setWorkflowMap((prev) => {
            const workflow = prev.get(workflowId);
            if (!workflow) return prev;

            try {
              const context = JSON.parse(workflow.context);
              context.progress = progress;
              context.step = step;

              const newMap = new Map(prev);
              newMap.set(workflowId, {
                ...workflow,
                context: JSON.stringify(context),
              });
              return newMap;
            } catch (error: unknown) {
              return prev;
            }
          });
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
