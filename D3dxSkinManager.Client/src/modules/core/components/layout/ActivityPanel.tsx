import React from 'react';
import { Drawer, Progress, Tag, Button, Empty } from 'antd';
import {
  LoadingOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  StopOutlined,
  ClockCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProcessStore, ProcessInfo, ProcessStatus } from '../../../../shared/store/processStore';
import { systemService } from '../../../../shared/services/ipc';
import './ActivityPanel.css';

interface ActivityPanelProps {
  open: boolean;
  onClose: () => void;
}

const STATUS_META: Record<ProcessStatus, { color: string; icon: React.ReactNode; key: string }> = {
  queued: { color: 'default', icon: <ClockCircleOutlined />, key: 'activity.status.queued' },
  running: { color: 'processing', icon: <LoadingOutlined spin />, key: 'activity.status.running' },
  completed: { color: 'success', icon: <CheckCircleOutlined />, key: 'activity.status.completed' },
  failed: { color: 'error', icon: <CloseCircleOutlined />, key: 'activity.status.failed' },
  cancelled: { color: 'default', icon: <StopOutlined />, key: 'activity.status.cancelled' },
  interrupted: { color: 'warning', icon: <ExclamationCircleOutlined />, key: 'activity.status.interrupted' },
};

function elapsed(p: ProcessInfo): string {
  const start = new Date(p.startedAt).getTime();
  const end = p.finishedAt ? new Date(p.finishedAt).getTime() : Date.now();
  const s = Math.max(0, Math.round((end - start) / 1000));
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  return `${m}m ${s % 60}s`;
}

const ActivityRow: React.FC<{ p: ProcessInfo }> = ({ p }) => {
  const { t } = useTranslation();
  const meta = STATUS_META[p.status];
  const progressStatus = p.status === 'failed' ? 'exception'
    : p.status === 'completed' ? 'success'
    : p.status === 'interrupted' ? 'normal'
    : 'active';

  return (
    <div className="activity-panel__row">
      <div className="activity-panel__row-head">
        <span className={`activity-panel__icon activity-panel__icon--${p.status}`}>{meta.icon}</span>
        <span className="activity-panel__title" title={p.title}>{p.title}</span>
        <Tag color={meta.color} className="activity-panel__status-tag">{t(meta.key)}</Tag>
        {p.status === 'running' && p.cancellable && (
          <Button
            size="small"
            type="text"
            danger
            onClick={() => void systemService.cancelProcess(p.id)}
          >
            {t('activity.actions.cancel')}
          </Button>
        )}
        {p.status === 'interrupted' && p.resumable && (
          <Button
            size="small"
            type="link"
            onClick={() => void systemService.resumeProcess(p.id)}
          >
            {t('activity.actions.resume')}
          </Button>
        )}
      </div>

      {(p.status === 'running' || p.status === 'interrupted') && (
        // Running: indeterminate (no progress yet) shows a full active bar as a "working" indicator.
        // Interrupted: the bar stops at the last reported progress so you can see how far it got.
        <Progress
          percent={p.status === 'interrupted' ? (p.progress ?? 0) : (p.progress ?? 100)}
          status={progressStatus}
          showInfo={p.progress !== undefined}
          size="small"
        />
      )}

      <div className="activity-panel__row-meta">
        {p.detail && <span className="activity-panel__detail" title={p.detail}>{p.detail}</span>}
        {p.error && <span className="activity-panel__error" title={p.error}>{p.error}</span>}
        <span className="activity-panel__elapsed">{elapsed(p)}</span>
      </div>
    </div>
  );
};

/** Download-manager-style Activity panel: all tracked long-running processes (running + history). */
export const ActivityPanel: React.FC<ActivityPanelProps> = ({ open, onClose }) => {
  const { t } = useTranslation();
  const processes = useProcessStore((s) => s.processes);
  const hasFinished = processes.some((p) => p.status !== 'running');
  // Group active (running/queued) vs finished history; backend already orders running-first.
  const active = processes.filter((p) => p.status === 'running' || p.status === 'queued');
  const history = processes.filter((p) => p.status !== 'running' && p.status !== 'queued');

  return (
    <Drawer
      title={t('activity.title')}
      placement="right"
      width={420}
      open={open}
      onClose={onClose}
      className="activity-panel"
      extra={
        hasFinished && (
          <Button size="small" onClick={() => void systemService.clearCompletedProcesses()}>
            {t('activity.actions.clearCompleted')}
          </Button>
        )
      }
    >
      {processes.length === 0 ? (
        <Empty description={t('activity.empty')} />
      ) : (
        <div className="activity-panel__list">
          {active.length > 0 && (
            <>
              <div className="activity-panel__section">{t('activity.sectionRunning', { count: active.length })}</div>
              {active.map((p) => <ActivityRow key={p.id} p={p} />)}
            </>
          )}
          {history.length > 0 && (
            <>
              <div className="activity-panel__section">{t('activity.sectionHistory')}</div>
              {history.map((p) => <ActivityRow key={p.id} p={p} />)}
            </>
          )}
        </div>
      )}
    </Drawer>
  );
};
