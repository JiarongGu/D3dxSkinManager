import React, { useEffect, useMemo, useRef } from 'react';
import { Space } from 'antd';
import { FolderOpenOutlined, CheckOutlined, DeleteOutlined, LoadingOutlined, CheckCircleOutlined, CloseCircleOutlined, ClockCircleOutlined, PlayCircleOutlined } from '@ant-design/icons';
import { CompactButton } from '../../../../shared/components/compact';
import { ModImportWorkflowTable } from './ModImportWorkflowTable';
import { useWorkflowQueue } from '../../hooks/modImport/useWorkflowQueue';
import { WorkflowStatus } from '../../types/workflow.types';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { eventBus, Module, WorkflowEventType } from '../../../../shared/services/eventBus';
import { refreshMods } from '../../../mod/operations/modOperations';
import { systemService } from '../../../../shared/services/ipc';
import { workflowService } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { useDropZone } from '../../../../shared/hooks/useDropZone';
import './ModImportWorkflowScreen.css';

/**
 * Mod Import Workflow Screen
 *
 * Download manager style dashboard for importing mods:
 * - Status dashboard with overall statistics
 * - Table view of all active imports with real-time progress
 * - Batch action support (pause/resume/delete)
 * - Auto-imports after compression (no confirmation needed)
 * - Support for multiple concurrent imports
 * - Automatically refreshes mod list when imports complete
 */
export const ModImportWorkflowScreen: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { workflows, clearCompleted, refresh, isLoading } = useWorkflowQueue();
  const [selectedWorkflowIds, setSelectedWorkflowIds] = React.useState<string[]>([]);
  const [defaultCategory, setDefaultCategory] = React.useState<string | undefined>();

  // Ref for the table tbody to attach drop zone
  const tbodyRef = useRef<HTMLElement | null>(null);
  const tableContainerRef = useRef<HTMLDivElement>(null);

  // Find and store reference to the ant-table-tbody element
  useEffect(() => {
    if (!tableContainerRef.current) return;

    // Use MutationObserver to wait for the table to render
    const observer = new MutationObserver(() => {
      const tbody = tableContainerRef.current?.querySelector('.ant-table-tbody');
      if (tbody) {
        tbodyRef.current = tbody as HTMLElement;
        observer.disconnect();
      }
    });

    observer.observe(tableContainerRef.current, {
      childList: true,
      subtree: true
    });

    // Also try immediately in case table is already rendered
    const tbody = tableContainerRef.current.querySelector('.ant-table-tbody');
    if (tbody) {
      tbodyRef.current = tbody as HTMLElement;
      observer.disconnect();
    }

    return () => observer.disconnect();
  }, []);

  // Get default category from store on mount
  useEffect(() => {
    const loadDefaultCategory = async () => {
      const { useModsStore } = await import('../../../mod/store/modsStore');
      const selectedCategory = useModsStore.getState().selectedCategory;
      setDefaultCategory(selectedCategory?.name);
    };
    loadDefaultCategory();
  }, []);

  // Listen for workflow completion and refresh mod list
  useEffect(() => {
    if (!selectedProfileId) return;

    const unsubCompleted = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.COMPLETED,
      async (event) => {
        if (event?.payload) {
                    // Refresh the mod list when a workflow completes
          await refreshMods(selectedProfileId);
        }
      }
    );

    return () => {
      unsubCompleted();
    };
  }, [selectedProfileId]);

  // Calculate stats
  const stats = useMemo(() => {
    const waiting = workflows.filter((w) => w.status === WorkflowStatus.WaitingForInput).length;
    const active = workflows.filter(
      (w) =>
        w.status === WorkflowStatus.Pending ||
        w.status === WorkflowStatus.Processing ||
        w.status === WorkflowStatus.WaitingForInput
    ).length;
    const completed = workflows.filter((w) => w.status === WorkflowStatus.Completed).length;
    const failed = workflows.filter((w) => w.status === WorkflowStatus.Failed).length;

    return { active, waiting, completed, failed, total: workflows.length };
  }, [workflows]);

  const hasCompleted = workflows.some((w) =>
    w.status === WorkflowStatus.Completed ||
    w.status === WorkflowStatus.Failed ||
    w.status === WorkflowStatus.Cancelled
  );

  const selectedWaitingCount = useMemo(() => {
    return selectedWorkflowIds.filter((id) => {
      const workflow = workflows.find((w) => w.id === id);
      return workflow?.status === WorkflowStatus.WaitingForInput;
    }).length;
  }, [selectedWorkflowIds, workflows]);

  // Check if there are stuck Pending workflows (not actively processing)
  // Show "Start Queue" only when:
  // 1. There are Pending/Processing workflows
  // 2. None of them are actively updating (no recent progress events)
  // This typically happens after app reboot when workflows are stuck
  const shouldShowStartQueue = useMemo(() => {
    // Check for Pending workflows (these definitely need to be started)
    const hasPending = workflows.some((w) => w.status === WorkflowStatus.Pending);

    // If there are any Pending workflows, show the button
    // (After app reboot, workflows are set to Pending and need to be resumed)
    return hasPending;
  }, [workflows]);

  const [importing, setImporting] = React.useState(false);

  // Drop zone for continuous file imports on the table tbody
  useDropZone({
    targetRef: tbodyRef,
    enabled: !!selectedProfileId && !!tbodyRef.current,
    onDrop: async (files: string[]) => {
      if (!selectedProfileId || files.length === 0) return;

      try {
        // Get selected category from store to pre-fill in workflow
        // Don't pass __unclassified__ placeholder - use undefined instead
        const { useModsStore } = await import('../../../mod/store/modsStore');
        const selectedCategory = useModsStore.getState().selectedCategory;
        const categoryId = selectedCategory?.id === '__unclassified__'
          ? undefined
          : selectedCategory?.id;

        // Start batch mod import workflows
        // The workflows will appear in the table as they are created
        await workflowService.batchStartModImport(
          selectedProfileId,
          files,
          categoryId
        );
      } catch (error: unknown) {
        handleError(error);
      }
    },
    classes: {
      hover: 'mod-import-workflow-screen-drop-hover',
      drop: 'mod-import-workflow-screen-drop-active'
    }
  });

  const handleImportFolder = async () => {
    if (!selectedProfileId) return;

    try {
      setImporting(true);
      const result = await systemService.openFolderDialog({
        title: t('mods.import.selectFolderOrFile'),
        rememberPathKey: 'mod-import-folder',
        allowFileSelection: true,
        filters: [
          { name: 'Archive Files', extensions: ['zip', '7z', 'rar', 'tar', 'gz', 'bz2'] },
          { name: 'All Files', extensions: ['*'] }
        ]
      });

      if (result.success && result.filePath) {
        // Get selected category from store to pre-fill in workflow
        // Don't pass __unclassified__ placeholder - use undefined instead
        const { useModsStore } = await import('../../../mod/store/modsStore');
        const selectedCategory = useModsStore.getState().selectedCategory;
        const categoryId = selectedCategory?.id === '__unclassified__'
          ? undefined
          : selectedCategory?.id;

        await workflowService.startModImport(selectedProfileId, result.filePath, categoryId);
      }
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setImporting(false);
    }
  };

  const handleConfirmSelected = async () => {
    if (!selectedProfileId || selectedWorkflowIds.length === 0) return;

    try {
      // Batch resume selected workflows (confirm action = continue workflow)
      const result = await workflowService.batchResumeWorkflows(selectedProfileId, selectedWorkflowIds);

      // Clear selection and refresh
      setSelectedWorkflowIds([]);
      refresh();

      // Show result notification
      if (result.failed.length > 0) {
              }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleClearSelected = async () => {
    if (!selectedProfileId || selectedWorkflowIds.length === 0) return;

    try {
      // Batch delete selected workflows (with temp file cleanup)
      const result = await workflowService.batchDeleteWorkflows(selectedProfileId, selectedWorkflowIds);

      // Clear selection and refresh
      setSelectedWorkflowIds([]);
      refresh();

      // Show result notification
      if (result.failed.length > 0) {
              }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  /**
   * Start all Pending workflows (used after app reboot)
   * Only resumes Pending workflows to avoid interfering with actively running tasks
   */
  const handleStartQueue = async () => {
    if (!selectedProfileId) return;

    try {
      // Find all Pending workflows (not Processing - those might be actively running)
      const workflowsToStart = workflows.filter(
        (w) => w.status === WorkflowStatus.Pending
      );

      if (workflowsToStart.length === 0) return;

      // Resume each workflow
      for (const workflow of workflowsToStart) {
        await workflowService.resumeWorkflow(selectedProfileId, workflow.id);
      }

      // Refresh to show updated status
      refresh();
    } catch (error: unknown) {
      handleError(error);
    }
  };

  return (
    <div className="mod-import-workflow-screen">
      {/* Status Bar - Top */}
      <div className="mod-import-workflow-screen-status-bar">
        <Space size="middle" style={{ width: '100%', justifyContent: 'space-between' }}>
          <Space size="middle">
            {/* Total */}
            <div className="mod-import-workflow-screen-stat">
              <span className="mod-import-workflow-screen-stat-label">{t('mods.import.stats.total')}</span>
              <span className="mod-import-workflow-screen-stat-value">{stats.total}</span>
            </div>

            {/* Active (waiting for action / in progress) */}
            <div className="mod-import-workflow-screen-stat">
              {stats.active > 0 && stats.waiting < stats.active ? (
                <LoadingOutlined className="mod-import-workflow-screen-stat-icon" spin />
              ) : (
                <ClockCircleOutlined className="mod-import-workflow-screen-stat-icon" />
              )}
              <span className="mod-import-workflow-screen-stat-label">{t('mods.import.stats.active')}</span>
              <span className="mod-import-workflow-screen-stat-value">{stats.waiting}/{stats.active}</span>
            </div>

            {/* Completed */}
            <div className="mod-import-workflow-screen-stat">
              <CheckCircleOutlined className="mod-import-workflow-screen-stat-icon mod-import-workflow-screen-stat-icon--success" />
              <span className="mod-import-workflow-screen-stat-label">{t('mods.import.stats.completed')}</span>
              <span className="mod-import-workflow-screen-stat-value">{stats.completed}</span>
            </div>

            {/* Failed */}
            <div className="mod-import-workflow-screen-stat">
              <CloseCircleOutlined className="mod-import-workflow-screen-stat-icon mod-import-workflow-screen-stat-icon--error" />
              <span className="mod-import-workflow-screen-stat-label">{t('mods.import.stats.failed')}</span>
              <span className="mod-import-workflow-screen-stat-value">{stats.failed}</span>
            </div>
          </Space>

          {/* Default Category Indicator */}
          {defaultCategory && (
            <div className="mod-import-workflow-screen-default-category">
              <span className="mod-import-workflow-screen-default-category-label">
                {t('mods.import.defaultCategory')}:
              </span>
              <span className="mod-import-workflow-screen-default-category-value">
                {defaultCategory}
              </span>
            </div>
          )}
        </Space>
      </div>

      {/* Action Bar - Bottom */}
      <div className="mod-import-workflow-screen-action-bar">
        <Space size="small">
          <CompactButton.Primary
            icon={<FolderOpenOutlined />}
            loading={importing}
            onClick={handleImportFolder}
          >
            {t('mods.import.import')}
          </CompactButton.Primary>

          {/* Start Queue button - show only when there are Pending workflows (after reboot) */}
          {shouldShowStartQueue && (
            <CompactButton.Success
              icon={<PlayCircleOutlined />}
              onClick={handleStartQueue}
            >
              {t('workflow.queue.startQueue')}
            </CompactButton.Success>
          )}

          <CompactButton.Success
            icon={<CheckOutlined />}
            disabled={selectedWaitingCount === 0}
            onClick={handleConfirmSelected}
          >
            {t('workflow.queue.confirm')}
          </CompactButton.Success>

          <CompactButton.Danger
            icon={<DeleteOutlined />}
            disabled={selectedWorkflowIds.length === 0}
            onClick={handleClearSelected}
          >
            {t('workflow.queue.delete')}
          </CompactButton.Danger>
        </Space>
      </div>

      {/* Workflow Queue Table */}
      <div className="mod-import-workflow-screen-content" ref={tableContainerRef}>
        <div className="mod-import-workflow-screen-drop-message" data-drop-message={t('mods.panel.dropToImport')} />
        {isLoading && workflows.length === 0 ? (
          <div className="mod-import-workflow-screen-loading">
            <LoadingOutlined spin style={{ fontSize: 32, color: '#1890ff' }} />
            <div className="mod-import-workflow-screen-loading-text">
              Preparing import workflows...
            </div>
            <div className="mod-import-workflow-screen-loading-hint">
              Your files are being analyzed. Workflows will appear here as they are created.
            </div>
          </div>
        ) : (
          <ModImportWorkflowTable
            workflows={workflows}
            onRefresh={refresh}
            selectedRowKeys={selectedWorkflowIds}
            onSelectionChange={setSelectedWorkflowIds}
          />
        )}
      </div>
    </div>
  );
};
