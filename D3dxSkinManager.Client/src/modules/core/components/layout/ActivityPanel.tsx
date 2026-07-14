import React from 'react';
import { Drawer, Progress, Empty } from 'antd';
import {
  LoadingOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  StopOutlined,
  ClockCircleOutlined,
  ExclamationCircleOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProcessStore, processTitle, processDetail, ProcessInfo, ProcessStatus } from '../../../../shared/store/processStore';
import { StatusTag, StatusTone } from '../../../../shared/components/common/StatusTag';
import { systemService } from '../../../../shared/services/ipc';
import './ActivityPanel.css';
import { CompactButton, CompactLinkButton } from '../../../../shared/components/compact';
import { navigateToTab } from '../../../../shared/hooks/useAppNavigation';
import { useModsStore } from '../../../mod/store/modsStore';

/** Activity tasks that live in the import queue — clicking one jumps to the import management screen. */
const IMPORT_TITLE_KEYS = new Set(['process.remoteImport']);

interface ActivityPanelProps {
  open: boolean;
  onClose: () => void;
}

const STATUS_META: Record<ProcessStatus, { tone: StatusTone; icon: React.ReactNode; key: string }> = {
  queued: { tone: 'neutral', icon: <ClockCircleOutlined />, key: 'activity.status.queued' },
  running: { tone: 'processing', icon: <LoadingOutlined spin />, key: 'activity.status.running' },
  completed: { tone: 'success', icon: <CheckCircleOutlined />, key: 'activity.status.completed' },
  failed: { tone: 'error', icon: <CloseCircleOutlined />, key: 'activity.status.failed' },
  cancelled: { tone: 'neutral', icon: <StopOutlined />, key: 'activity.status.cancelled' },
  interrupted: { tone: 'warning', icon: <ExclamationCircleOutlined />, key: 'activity.status.interrupted' },
};

function elapsed(p: ProcessInfo): string {
  const start = new Date(p.startedAt).getTime();
  const end = p.finishedAt ? new Date(p.finishedAt).getTime() : Date.now();
  const s = Math.max(0, Math.round((end - start) / 1000));
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  return `${m}m ${s % 60}s`;
}

const ActivityRow: React.FC<{ p: ProcessInfo; onOpenImportScreen?: () => void }> = ({ p, onOpenImportScreen }) => {
  const { t } = useTranslation();
  const meta = STATUS_META[p.status];
  const title = processTitle(p, t);
  const detail = processDetail(p, t);
  const progressStatus = p.status === 'failed' ? 'exception'
    : p.status === 'completed' ? 'success'
    : p.status === 'interrupted' ? 'normal'
    : 'active';

  // An import task (remote download) also lives in the import queue — clicking the row jumps there.
  const isImport = !!p.titleKey && IMPORT_TITLE_KEYS.has(p.titleKey);
  const handleRowClick = (e: React.MouseEvent) => {
    if (!isImport) return;
    if ((e.target as HTMLElement).closest('button, a')) return; // don't hijack cancel/resume clicks
    navigateToTab('mods');
    useModsStore.getState().openImportWorkflowScreen();
    onOpenImportScreen?.();
  };

  return (
    <div
      className={`activity-panel__row${isImport ? ' activity-panel__row--clickable' : ''}`}
      onClick={handleRowClick}
      role={isImport ? 'button' : undefined}
      title={isImport ? t('activity.openInQueue') : undefined}
    >
      <div className="activity-panel__row-head">
        <span className={`activity-panel__icon activity-panel__icon--${p.status}`}>{meta.icon}</span>
        <span className="activity-panel__title" title={title}>{title}</span>
        <StatusTag tone={meta.tone} label={t(meta.key)} icon={null} className="activity-panel__status-tag" />
        {p.status === 'running' && p.cancellable && (
          <CompactButton
            size="small"
            type="text"
            danger
            onClick={() => void systemService.cancelProcess(p.id)}
          >
            {t('activity.actions.cancel')}
          </CompactButton>
        )}
        {p.status === 'interrupted' && p.resumable && (
          <CompactLinkButton
            size="small"
            onClick={() => void systemService.resumeProcess(p.id)}
          >
            {t('activity.actions.resume')}
          </CompactLinkButton>
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
        {detail && <span className="activity-panel__detail" title={detail}>{detail}</span>}
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
      // antd v6 deprecated the `width` prop — set the panel width via the semantic `styles.wrapper`.
      styles={{ wrapper: { width: 420 } }}
      open={open}
      onClose={onClose}
      className="activity-panel"
      extra={
        hasFinished && (
          <CompactButton size="small" onClick={() => void systemService.clearCompletedProcesses()}>
            {t('activity.actions.clearCompleted')}
          </CompactButton>
        )
      }
    >
      {processes.length === 0 ? (
        <div className="activity-panel__empty">
          <Empty description={t('activity.empty')} />
        </div>
      ) : (
        <div className="activity-panel__list">
          {active.length > 0 && (
            <>
              <div className="activity-panel__section">{t('activity.sectionRunning', { count: active.length })}</div>
              {active.map((p) => <ActivityRow key={p.id} p={p} onOpenImportScreen={onClose} />)}
            </>
          )}
          {history.length > 0 && (
            <>
              <div className="activity-panel__section">{t('activity.sectionHistory')}</div>
              {history.map((p) => <ActivityRow key={p.id} p={p} onOpenImportScreen={onClose} />)}
            </>
          )}
        </div>
      )}
    </Drawer>
  );
};
