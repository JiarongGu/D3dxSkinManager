import React from 'react';
import { Button, Empty, Space } from 'antd';
import { FolderOpenOutlined, DeleteOutlined } from '@ant-design/icons';
import { FolderImportButton } from './FolderImportButton';
import { WorkflowQueueTable } from './WorkflowQueueTable';
import { useWorkflowQueue } from '../hooks/useWorkflowQueue';
import { useTranslation } from 'react-i18next';
import './ModImportQueueScreen.css';

/**
 * Mod Import Queue Screen
 *
 * Download manager style interface for importing mods:
 * - Table view of all active imports
 * - Progress tracking for each import
 * - Inline metadata editing when needed
 * - Support for multiple concurrent imports
 */
export const ModImportQueueScreen: React.FC = () => {
  const { t } = useTranslation();
  const { workflows, clearCompleted, refresh } = useWorkflowQueue();

  const hasWorkflows = workflows.length > 0;
  const hasCompleted = workflows.some((w) => w.status === 3 || w.status === 4 || w.status === 5);

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
