import React, { useEffect, useMemo } from 'react';
import { Space } from 'antd';
import { FolderOpenOutlined, CheckOutlined, DeleteOutlined, LoadingOutlined, CheckCircleOutlined, CloseCircleOutlined, ClockCircleOutlined } from '@ant-design/icons';
import { CompactButton } from '../../../../shared/components/compact';
import { ModImportWorkflowTable } from './ModImportWorkflowTable';
import { useWorkflowQueue } from '../../hooks/modImport/useWorkflowQueue';
import { WorkflowStatus } from '../../types/workflow.types';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { eventBus, Module, WorkflowEventType } from '../../../../shared/services/eventBus';
import { refreshMods } from '../../../mod/operations/modOperations';
import { systemService } from '../../../../shared/services/systemService';
import { workflowService } from '../../services/workflowService';
import { handleError } from '../../../../shared/utils/errorHandler';
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
  const { workflows, clearCompleted, refresh } = useWorkflowQueue();
  const [selectedWorkflowIds, setSelectedWorkflowIds] = React.useState<string[]>([]);
  const [defaultCategory, setDefaultCategory] = React.useState<string | undefined>();

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
          console.log('[ModImportWorkflowScreen] Workflow completed, refreshing mod list');
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

  const [importing, setImporting] = React.useState(false);

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
        const { useModsStore } = await import('../../../mod/store/modsStore');
        const selectedCategory = useModsStore.getState().selectedCategory;
        const categoryName = selectedCategory?.name;

        await workflowService.startModImport(selectedProfileId, result.filePath, categoryName);
      }
    } catch (error) {
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
        console.warn(`Batch confirm: ${result.successful.length} successful, ${result.failed.length} failed`, result.failed);
      }
    } catch (error) {
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
        console.warn(`Batch delete: ${result.successful.length} successful, ${result.failed.length} failed`, result.failed);
      }
    } catch (error) {
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
      <div className="mod-import-workflow-screen-content">
        <ModImportWorkflowTable
          workflows={workflows}
          onRefresh={refresh}
          selectedRowKeys={selectedWorkflowIds}
          onSelectionChange={setSelectedWorkflowIds}
        />
      </div>
    </div>
  );
};
