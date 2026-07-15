import React, { useEffect, useMemo, useRef } from 'react';
import { Space } from 'antd';
import { FolderOpenOutlined, CheckOutlined, DeleteOutlined, LoadingOutlined, ClearOutlined, PlayCircleOutlined } from '@ant-design/icons';
import { CompactButton, CountChip } from '../../../../shared/components/compact';
import type { CountChipTone } from '../../../../shared/components/compact';
import { ModImportWorkflowTable } from './ModImportWorkflowTable';
import { useWorkflowQueue } from '../../hooks/modImport/useWorkflowQueue';
import { WorkflowStatus } from '../../types/workflow.types';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { Module, WorkflowEventType } from '../../../../shared/services/eventBus';
import { useEventSubscription } from '../../../../shared/hooks/useEventSubscription';
import { refreshMods } from '../../../mod/operations/modOperations';
import { systemService } from '../../../../shared/services/ipc';
import { workflowService } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { notification } from '../../../../shared/utils/notification';
import { useDropZone } from '../../../../shared/hooks/useDropZone';
import './ModImportWorkflowScreen.css';

/** Status filter groups for the dashboard chips. */
type QueueFilter = 'all' | 'running' | 'waiting' | 'completed' | 'failed';

const FILTER_STATUSES: Record<Exclude<QueueFilter, 'all'>, WorkflowStatus[]> = {
  running: [WorkflowStatus.Pending, WorkflowStatus.Processing, WorkflowStatus.Paused, WorkflowStatus.Deleting],
  waiting: [WorkflowStatus.WaitingForInput],
  completed: [WorkflowStatus.Completed],
  failed: [WorkflowStatus.Failed, WorkflowStatus.Cancelled],
};

/**
 * Mod Import Workflow Screen — download-manager-style dashboard for importing mods.
 * - Clickable status chips (filter the queue by state)
 * - Real-time progress table with expandable detail rows + drag-drop imports
 * - Batch confirm/delete with result toasts; one-click clear of finished tasks
 * - AUTO-RESUME: interrupted imports (Pending/Processing rows with no active backend
 *   task, e.g. after an app restart) are resumed automatically when the screen opens.
 */
export const ModImportWorkflowScreen: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { workflows, refresh, isLoading } = useWorkflowQueue();
  const [selectedWorkflowIds, setSelectedWorkflowIds] = React.useState<string[]>([]);
  const [defaultCategory, setDefaultCategory] = React.useState<string | undefined>();
  const [filter, setFilter] = React.useState<QueueFilter>('all');

  // Ref for the table body to attach drop zone
  const tableBodyRef = useRef<HTMLElement | null>(null);
  const tableContainerRef = useRef<HTMLDivElement>(null);

  // Find and store reference to the ant-table-body element
  useEffect(() => {
    if (!tableContainerRef.current) return;

    // Use MutationObserver to wait for the table to render
    const observer = new MutationObserver(() => {
      const tableBody = tableContainerRef.current?.querySelector('.ant-table-body');
      if (tableBody) {
        tableBodyRef.current = tableBody as HTMLElement;
        observer.disconnect();
      }
    });

    observer.observe(tableContainerRef.current, {
      childList: true,
      subtree: true
    });

    // Also try immediately in case table is already rendered
    const tableBody = tableContainerRef.current.querySelector('.ant-table-body');
    if (tableBody) {
      tableBodyRef.current = tableBody as HTMLElement;
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
  useEventSubscription(
    Module.WORKFLOW,
    WorkflowEventType.COMPLETED,
    (payload) => {
      if (!selectedProfileId || !payload) return;
      // Sync handler: useEventSubscription doesn't await, so an async handler would leave its rejection
      // unobserved. Fire the refresh and forget it.
      void refreshMods(selectedProfileId);
    },
    [selectedProfileId],
  );

  // Calculate stats for the filter chips
  const stats = useMemo(() => ({
    all: workflows.length,
    running: workflows.filter((w) => FILTER_STATUSES.running.includes(w.status)).length,
    waiting: workflows.filter((w) => FILTER_STATUSES.waiting.includes(w.status)).length,
    completed: workflows.filter((w) => FILTER_STATUSES.completed.includes(w.status)).length,
    failed: workflows.filter((w) => FILTER_STATUSES.failed.includes(w.status)).length,
  }), [workflows]);

  const filteredWorkflows = useMemo(() => {
    if (filter === 'all') return workflows;
    const statuses = FILTER_STATUSES[filter];
    return workflows.filter((w) => statuses.includes(w.status));
  }, [workflows, filter]);

  const finishedIds = useMemo(() => workflows
    .filter((w) =>
      w.status === WorkflowStatus.Completed ||
      w.status === WorkflowStatus.Failed ||
      w.status === WorkflowStatus.Cancelled)
    .map((w) => w.id), [workflows]);

  const selectedWaitingCount = useMemo(() => {
    return selectedWorkflowIds.filter((id) => {
      const workflow = workflows.find((w) => w.id === id);
      return workflow?.status === WorkflowStatus.WaitingForInput;
    }).length;
  }, [selectedWorkflowIds, workflows]);

  // ===== Resume logic =====
  // Interrupted imports (Pending/Processing rows with nothing actually running in the backend —
  // the state a restart leaves behind) used to sit dead until the user found the "Start Queue"
  // button. Now: once the queue has loaded, if stuck rows exist and the backend reports 0 active
  // workflows, resume them all automatically (once per screen mount) and say so. The manual
  // button remains as a fallback for anything still stuck afterwards.
  const [activeWorkflowCount, setActiveWorkflowCount] = React.useState<number>(0);
  const autoResumeTried = useRef(false);

  useEffect(() => {
    // Once we've attempted the one auto-resume, bail BEFORE the backend call — otherwise every
    // `workflows` change (Processing rows count as "stuck") re-ran getActiveWorkflowCount indefinitely.
    if (!selectedProfileId || isLoading || autoResumeTried.current) return;
    const stuck = workflows.filter(
      (w) => w.status === WorkflowStatus.Pending || w.status === WorkflowStatus.Processing);
    if (stuck.length === 0) return;

    const checkAndResume = async () => {
      try {
        const count = await workflowService.getActiveWorkflowCount(selectedProfileId);
        setActiveWorkflowCount(count);
        if (count > 0) return; // backend still busy — wait for it to go idle, then resume
        autoResumeTried.current = true;
        await workflowService.resumeAllStuckWorkflowsByType(selectedProfileId, 'MOD_IMPORT');
        notification.info(t('workflow.queue.autoResumed', { count: stuck.length }));
        refresh();
        setActiveWorkflowCount(await workflowService.getActiveWorkflowCount(selectedProfileId));
      } catch {
        // Non-critical — the manual Start Queue button still covers this.
      }
    };
    void checkAndResume();
  }, [selectedProfileId, isLoading, workflows, refresh, t]);

  // Manual fallback — only relevant when stuck rows remain AFTER the auto-resume attempt.
  const shouldShowStartQueue = useMemo(() => {
    const hasPending = workflows.some((w) => w.status === WorkflowStatus.Pending || w.status === WorkflowStatus.Processing);
    return hasPending && activeWorkflowCount === 0 && autoResumeTried.current;
  }, [workflows, activeWorkflowCount]);

  const [importing, setImporting] = React.useState(false);

  // Drop zone for continuous file imports on the table body
  useDropZone({
    targetRef: tableBodyRef,
    enabled: !!selectedProfileId && !!tableBodyRef.current,
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
    className: 'mod-import-workflow-screen-drop-active'
  });

  const handleImportFolder = async () => {
    if (!selectedProfileId) return;

    try {
      setImporting(true);
      const result = await systemService.openFolderDialog({
        title: t('mods.import.selectFolderOrFile'),
        rememberPathKey: 'mod-import',
        allowFileSelection: true,
        filters: [
          { name: t('mods.import.filters.archiveFiles'), extensions: ['zip', '7z', 'rar', 'tar', 'gz', 'bz2'] },
          { name: t('mods.import.filters.allFiles'), extensions: ['*'] }
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
      setSelectedWorkflowIds([]);
      refresh();

      if (result.failed.length > 0) {
        notification.warning(t('workflow.queue.batchPartialFailed', { count: result.failed.length }));
      } else {
        notification.success(t('workflow.queue.batchConfirmed', { count: result.successful.length }));
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
      setSelectedWorkflowIds([]);
      refresh();

      if (result.failed.length > 0) {
        notification.warning(t('workflow.queue.batchPartialFailed', { count: result.failed.length }));
      } else {
        notification.success(t('workflow.queue.batchDeleted', { count: result.successful.length }));
      }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  /** Remove every finished (completed/failed/cancelled) task from the queue in one click. */
  const handleClearFinished = async () => {
    if (!selectedProfileId || finishedIds.length === 0) return;

    try {
      const result = await workflowService.batchDeleteWorkflows(selectedProfileId, finishedIds);
      refresh();
      if (result.failed.length > 0) {
        notification.warning(t('workflow.queue.batchPartialFailed', { count: result.failed.length }));
      } else {
        notification.success(t('workflow.queue.batchDeleted', { count: result.successful.length }));
      }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  /** Manual fallback: resume ALL stuck MOD_IMPORT workflows (auto-resume runs this on open). */
  const handleStartQueue = async () => {
    if (!selectedProfileId) return;

    try {
      await workflowService.resumeAllStuckWorkflowsByType(selectedProfileId, 'MOD_IMPORT');
      refresh();
      try {
        setActiveWorkflowCount(await workflowService.getActiveWorkflowCount(selectedProfileId));
      } catch {
        // Non-critical.
      }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const filterChips: { key: QueueFilter; label: string; count: number; tone?: CountChipTone }[] = [
    { key: 'all', label: t('mods.import.stats.total'), count: stats.all },
    { key: 'running', label: t('mods.import.stats.active'), count: stats.running, tone: 'running' },
    { key: 'waiting', label: t('workflow.status.awaitingConfirmation'), count: stats.waiting, tone: 'waiting' },
    { key: 'completed', label: t('mods.import.stats.completed'), count: stats.completed, tone: 'completed' },
    { key: 'failed', label: t('mods.import.stats.failed'), count: stats.failed, tone: 'failed' },
  ];

  return (
    <div className="mod-import-workflow-screen">
      {/* Status Bar - Top */}
      <div className="mod-import-workflow-screen-status-bar">
        <Space size="middle" style={{ width: '100%', justifyContent: 'space-between' }}>
          {/* Clickable status chips — filter the queue (CountChip atom keeps label + count aligned). */}
          <div className="mod-import-workflow-screen-chips">
            {filterChips.map((chip) => (
              <CountChip
                key={chip.key}
                label={chip.label}
                count={chip.count}
                tone={chip.tone}
                active={filter === chip.key}
                icon={chip.key === 'running' && stats.running > 0 ? <LoadingOutlined spin /> : undefined}
                onClick={() => setFilter(chip.key)}
              />
            ))}
          </div>

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

          {/* Manual resume fallback — shown only if imports remain stuck after the auto-resume */}
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
            {t('workflow.queue.confirm')}{selectedWaitingCount > 0 ? ` (${selectedWaitingCount})` : ''}
          </CompactButton.Success>

          <CompactButton.Danger
            icon={<DeleteOutlined />}
            disabled={selectedWorkflowIds.length === 0}
            onClick={handleClearSelected}
          >
            {t('common.delete')}{selectedWorkflowIds.length > 0 ? ` (${selectedWorkflowIds.length})` : ''}
          </CompactButton.Danger>

          <CompactButton
            icon={<ClearOutlined />}
            disabled={finishedIds.length === 0}
            onClick={handleClearFinished}
          >
            {t('workflow.queue.clearFinished')}{finishedIds.length > 0 ? ` (${finishedIds.length})` : ''}
          </CompactButton>
        </Space>
      </div>

      {/* Workflow Queue Table */}
      <div className="mod-import-workflow-screen-content" ref={tableContainerRef}>
        <div className="mod-import-workflow-screen-drop-message" data-drop-message={t('mods.panel.dropToImport')} />
        {isLoading && workflows.length === 0 ? (
          <div className="mod-import-workflow-screen-loading">
            <LoadingOutlined spin style={{ fontSize: 32, color: 'var(--color-primary)' }} />
            <div className="mod-import-workflow-screen-loading-text">
              {t('mods.import.loading.preparing')}
            </div>
            <div className="mod-import-workflow-screen-loading-hint">
              {t('mods.import.loading.hint')}
            </div>
          </div>
        ) : (
          <ModImportWorkflowTable
            workflows={filteredWorkflows}
            onRefresh={refresh}
            selectedRowKeys={selectedWorkflowIds}
            onSelectionChange={setSelectedWorkflowIds}
          />
        )}
      </div>
    </div>
  );
};
