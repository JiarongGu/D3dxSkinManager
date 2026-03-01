import React, { useEffect } from 'react';
import { Button, Empty, Space } from 'antd';
import { FolderOpenOutlined, DeleteOutlined } from '@ant-design/icons';
import { FolderImportButton } from './FolderImportButton';
import { WorkflowQueueTable } from './WorkflowQueueTable';
import { useWorkflowQueue } from '../hooks/useWorkflowQueue';
import { WorkflowStatus } from '../types/workflow.types';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../shared/context/ProfileContext';
import { eventBus, Module, WorkflowEventType } from '../../../shared/services/eventBus';
import { refreshMods } from '../../mod/operations/modOperations';
import './ModImportQueueScreen.css';

/**
 * Mod Import Queue Screen
 *
 * Download manager style interface for importing mods:
 * - Table view of all active imports
 * - Progress tracking for each import
 * - Auto-imports after compression (no confirmation needed)
 * - Support for multiple concurrent imports
 * - Automatically refreshes mod list when imports complete
 */
export const ModImportQueueScreen: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { workflows, clearCompleted, refresh } = useWorkflowQueue();

  // Listen for workflow completion and refresh mod list
  useEffect(() => {
    if (!selectedProfileId) return;

    const unsubCompleted = eventBus.subscribe(
      Module.WORKFLOW,
      WorkflowEventType.COMPLETED,
      async (event) => {
        if (event?.payload) {
          console.log('[ModImportQueueScreen] Workflow completed, refreshing mod list');
          // Refresh the mod list when a workflow completes
          await refreshMods(selectedProfileId);
        }
      }
    );

    return () => {
      unsubCompleted();
    };
  }, [selectedProfileId]);

  const hasWorkflows = workflows.length > 0;
  const hasCompleted = workflows.some((w) =>
    w.status === WorkflowStatus.Completed ||
    w.status === WorkflowStatus.Failed ||
    w.status === WorkflowStatus.Cancelled
  );

  return (
    <div className="mod-import-queue-screen">
      {/* Toolbar */}
      <div className="queue-toolbar">
        <Space>
          <FolderImportButton />
          {/* Future: Add Archive Import button */}
        </Space>

        <Space>
          {hasCompleted && (
            <Button
              icon={<DeleteOutlined />}
              onClick={clearCompleted}
              size="small"
            >
              {t('workflow.queue.clearCompleted')}
            </Button>
          )}
        </Space>
      </div>

      {/* Workflow Queue Table */}
      <div className="queue-content">
        {hasWorkflows ? (
          <WorkflowQueueTable workflows={workflows} onRefresh={refresh} />
        ) : (
          <Empty
            className="queue-empty"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
              <div>
                <p>{t('workflow.queue.empty')}</p>
                <p style={{ fontSize: '12px', color: '#888', marginTop: 8 }}>
                  {t('workflow.queue.emptyHint')}
                </p>
              </div>
            }
          >
            <FolderImportButton />
          </Empty>
        )}
      </div>
    </div>
  );
};
