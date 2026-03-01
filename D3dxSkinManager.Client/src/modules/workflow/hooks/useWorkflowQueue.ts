/**
 * Hook for managing workflow queue
 * Tracks multiple workflows and subscribes to their events
 */

import { useState, useEffect, useCallback } from 'react';
import { eventBus, Module, WorkflowEventType } from '../../../shared/services/eventBus';
import { workflowService } from '../services/workflowService';
import { useProfile } from '../../../shared/context/ProfileContext';
import type { WorkflowInfo } from '../types/workflow.types';

interface UseWorkflowQueueReturn {
  workflows: WorkflowInfo[];
  addWorkflow: (workflow: WorkflowInfo) => void;
  removeWorkflow: (workflowId: string) => void;
  updateWorkflow: (workflow: WorkflowInfo) => void;
  clearCompleted: () => void;
  refresh: () => void;
}

/**
 * Hook for managing a queue of workflows
 * Automatically subscribes to workflow events for real-time updates
 */
export const useWorkflowQueue = (): UseWorkflowQueueReturn => {
  const { selectedProfileId } = useProfile();
  const [workflows, setWorkflows] = useState<WorkflowInfo[]>([]);
  const [isInitialized, setIsInitialized] = useState(false);

  /**
   * Add a workflow to the queue
   */
  const addWorkflow = useCallback((workflow: WorkflowInfo) => {
    console.log('[useWorkflowQueue] Adding workflow to queue:', workflow.id);
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
    console.log('[useWorkflowQueue] Removing workflow from queue:', workflowId);
    setWorkflows((prev) => prev.filter((w) => w.id !== workflowId));
  }, []);

  /**
   * Update a workflow in the queue
   */
  const updateWorkflow = useCallback((workflow: WorkflowInfo) => {
    console.log('[useWorkflowQueue] Updating workflow in queue:', workflow.id);
    setWorkflows((prev) => prev.map((w) => (w.id === workflow.id ? workflow : w)));
  }, []);

  /**
   * Clear all completed workflows
   */
  const clearCompleted = useCallback(() => {
    console.log('[useWorkflowQueue] Clearing completed workflows');
    setWorkflows((prev) =>
      prev.filter(
        (w) => w.status !== 3 && w.status !== 4 && w.status !== 5 // Not Completed, Failed, or Cancelled
      )
    );
  }, []);

  /**
   * Load workflows from backend
   */
  const refresh = useCallback(async () => {
    if (!selectedProfileId) {
      console.log('[useWorkflowQueue] No profile selected, skipping load');
      return;
    }

    try {
      console.log('[useWorkflowQueue] Loading workflows from backend');
      const loadedWorkflows = await workflowService.getWorkflowsByType(
        selectedProfileId,
        'MOD_IMPORT'
      );
      console.log('[useWorkflowQueue] Loaded workflows:', loadedWorkflows);
      setWorkflows(loadedWorkflows);
    } catch (error) {
      console.error('[useWorkflowQueue] Failed to load workflows:', error);
    }
  }, [selectedProfileId]);

  /**
   * Load existing workflows on mount
   */
  useEffect(() => {
    if (selectedProfileId && !isInitialized) {
      console.log('[useWorkflowQueue] Initial load of workflows');
      refresh();
      setIsInitialized(true);
    }
  }, [selectedProfileId, isInitialized, refresh]);

  /**
   * Subscribe to workflow events for real-time updates
   */
  useEffect(() => {
    console.log('[useWorkflowQueue] Setting up workflow event subscriptions');

    const unsubCreated = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.CREATED,
      (event) => {
        if (event?.payload) {
          console.log('[useWorkflowQueue] Workflow created event:', event.payload);
          addWorkflow(event.payload);
        }
      }
    );

    const unsubStatusChanged = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.STATUS_CHANGED,
      (event) => {
        if (event?.payload) {
          console.log('[useWorkflowQueue] Workflow status changed event:', event.payload);
          updateWorkflow(event.payload);
        }
      }
    );

    const unsubCompleted = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.COMPLETED,
      (event) => {
        if (event?.payload) {
          console.log('[useWorkflowQueue] Workflow completed event:', event.payload);
          updateWorkflow(event.payload);
        }
      }
    );

    const unsubFailed = eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.FAILED, (event) => {
      if (event?.payload) {
        console.log('[useWorkflowQueue] Workflow failed event:', event.payload);
        updateWorkflow(event.payload);
      }
    });

    const unsubCancelled = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.CANCELLED,
      (event) => {
        if (event?.payload) {
          console.log('[useWorkflowQueue] Workflow cancelled event:', event.payload);
          updateWorkflow(event.payload);
        }
      }
    );

    // Cleanup subscriptions
    return () => {
      console.log('[useWorkflowQueue] Cleaning up workflow event subscriptions');
      unsubCreated();
      unsubStatusChanged();
      unsubCompleted();
      unsubFailed();
      unsubCancelled();
    };
  }, [addWorkflow, updateWorkflow]);

  return {
    workflows,
    addWorkflow,
    removeWorkflow,
    updateWorkflow,
    clearCompleted,
    refresh,
  };
};
