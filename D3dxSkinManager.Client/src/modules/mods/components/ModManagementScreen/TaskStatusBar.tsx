import React from 'react';
import { Progress, Space, Tag, Divider } from 'antd';
import { ImportStats } from '../../types/importTask.types';
import { useTranslation } from 'react-i18next';
import './TaskStatusBar.css';

interface TaskStatusBarProps {
  stats: ImportStats;
}

/**
 * Task Status Bar - Shows overall import queue progress
 *
 * Displays:
 * - Progress bar showing completion percentage
 * - Task counts by status (total, pending, processing, success, error)
 * - Compact view suitable for bottom of screen
 */
export const TaskStatusBar: React.FC<TaskStatusBarProps> = ({ stats }) => {
  const { t } = useTranslation();

  // Calculate progress percentage
  const completed = stats.success + stats.error + stats.cancelled;
  const progressPercent = stats.total > 0 ? Math.round((completed / stats.total) * 100) : 0;

  // Determine progress status
  const getProgressStatus = (): 'success' | 'exception' | 'active' | undefined => {
    if (stats.error > 0 && completed === stats.total) return 'exception';
    if (completed === stats.total && stats.total > 0) return 'success';
    if (stats.processing > 0) return 'active';
    return undefined;
  };

  return (
    <div className="task-status-bar">
      {/* Progress Bar */}
      <div className="task-status-bar-progress">
        <Progress
          percent={progressPercent}
          status={getProgressStatus()}
          size="small"
          showInfo={false}
        />
      </div>

      {/* Statistics */}
      <div className="task-status-bar-stats">
        <Space split={<Divider type="vertical" />} size="small">
          <span className="task-status-bar-stat">
            {t('importQueue.stats.total')}: <strong>{stats.total}</strong>
          </span>

          {stats.pending > 0 && (
            <span className="task-status-bar-stat">
              <Tag color="default" className="task-status-bar-tag">{stats.pending}</Tag>
              {t('importQueue.stats.pending')}
            </span>
          )}

          {stats.processing > 0 && (
            <span className="task-status-bar-stat">
              <Tag color="processing" className="task-status-bar-tag">{stats.processing}</Tag>
              {t('importQueue.stats.processing')}
            </span>
          )}

          {stats.success > 0 && (
            <span className="task-status-bar-stat">
              <Tag color="success" className="task-status-bar-tag">{stats.success}</Tag>
              {t('importQueue.stats.success')}
            </span>
          )}

          {stats.error > 0 && (
            <span className="task-status-bar-stat">
              <Tag color="error" className="task-status-bar-tag">{stats.error}</Tag>
              {t('importQueue.stats.error')}
            </span>
          )}
        </Space>
      </div>
    </div>
  );
};
