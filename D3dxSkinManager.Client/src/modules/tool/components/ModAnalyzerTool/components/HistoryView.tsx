import React from 'react';
import { Empty, Tag } from 'antd';
import {
  ArrowLeftOutlined,
  DeleteOutlined,
  EyeOutlined,
  ClearOutlined,
  ClockCircleOutlined,
  CheckCircleOutlined,
  PauseCircleOutlined,
  StopOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton, CompactCard, CompactIconButton } from '../../../../../shared/components/compact';
import { StatusTag } from '../../../../../shared/components/common/StatusTag';
import { ConfirmDialog } from '../../../../../shared/components/dialogs/ConfirmDialog';
import type { AnalysisSessionSummary } from '../../../../../shared/types/analysis.types';

interface HistoryViewProps {
  sessions: AnalysisSessionSummary[];
  onViewSession: (sessionId: string, sessionStatus?: string) => void;
  onDeleteSession: (sessionId: string) => void;
  onClearAll: () => void;
  onBack: () => void;
}

export const HistoryView: React.FC<HistoryViewProps> = ({ sessions, onViewSession, onDeleteSession, onClearAll, onBack }) => {
  const { t } = useTranslation();
  const [confirmClearAll, setConfirmClearAll] = React.useState(false);

  return (
    <div className="mod-analyzer__history">
      {/* Header */}
      <div className="mod-analyzer__history-header">
        <div className="mod-analyzer__history-header-left">
          <CompactButton icon={<ArrowLeftOutlined />} onClick={onBack}>
            {t('tools.modAnalyzer.back')}
          </CompactButton>
          <span className="mod-analyzer__history-title">{t('tools.modAnalyzer.history')}</span>
          <Tag>{sessions.length}</Tag>
        </div>
        {sessions.length > 0 && (
          <CompactButton danger icon={<ClearOutlined />} onClick={() => setConfirmClearAll(true)}>
            {t('tools.modAnalyzer.clearAll')}
          </CompactButton>
        )}
      </div>

      {/* Session list */}
      <div className="mod-analyzer__history-list">
        {sessions.length === 0 ? (
          <div className="mod-analyzer__empty">
            <Empty description={t('tools.modAnalyzer.noHistory')} />
          </div>
        ) : (
          sessions.map(session => (
            <SessionCard
              key={session.id}
              session={session}
              onView={() => onViewSession(session.id, session.status)}
              onDelete={() => onDeleteSession(session.id)}
            />
          ))
        )}
      </div>

      <ConfirmDialog
        visible={confirmClearAll}
        title={t('tools.modAnalyzer.clearAllTitle')}
        content={t('tools.modAnalyzer.clearAllConfirm')}
        okType="danger"
        onOk={async () => { onClearAll(); setConfirmClearAll(false); }}
        onCancel={() => setConfirmClearAll(false)}
      />
    </div>
  );
};

const SessionCard: React.FC<{
  session: AnalysisSessionSummary;
  onView: () => void;
  onDelete: () => void;
}> = ({ session, onView, onDelete }) => {
  const { t } = useTranslation();
  const dateStr = new Date(session.startedAt).toLocaleString();
  const isCompleted = session.status === 'completed';
  const isCancelled = session.status === 'cancelled';
  const isPaused = session.status === 'paused';
  const isRunning = session.status === 'running';

  return (
    <CompactCard hoverable className="mod-analyzer__session-card" onClick={onView}>
      <div className="mod-analyzer__session-card-content">
        <div className="mod-analyzer__session-card-top">
          <div className="mod-analyzer__session-card-info">
            {isCompleted ? (
              <CheckCircleOutlined style={{ color: 'var(--color-success)' }} />
            ) : isCancelled ? (
              <StopOutlined style={{ color: 'var(--color-text-tertiary)' }} />
            ) : isPaused ? (
              <PauseCircleOutlined style={{ color: 'var(--color-warning)' }} />
            ) : isRunning ? (
              <ClockCircleOutlined style={{ color: 'var(--color-primary)' }} />
            ) : (
              <ClockCircleOutlined style={{ color: 'var(--color-text-tertiary)' }} />
            )}
            <span className="mod-analyzer__session-card-date">{dateStr}</span>
            {session.categoryName && <Tag>{session.categoryName}</Tag>}
            {!session.categoryName && <Tag>{t('tools.modAnalyzer.allCategories')}</Tag>}
          </div>
          {/* CompactIconButton (L1): identical borderless render path for both actions — the old
              primary + primary-danger CompactButton pair rasterized their 1px borders on different
              pixel rows at fractional DPI, reading as the danger button sitting higher. */}
          <div className="mod-analyzer__session-card-actions" onClick={e => e.stopPropagation()}>
            <CompactIconButton tone="primary" icon={<EyeOutlined />} title={t('tools.modAnalyzer.viewSession')} onClick={onView} />
            <CompactIconButton tone="danger" icon={<DeleteOutlined />} title={t('common.delete')} onClick={onDelete} />
          </div>
        </div>
        <div className="mod-analyzer__session-card-stats">
          <span>{session.analyzedCount}/{session.totalMods} {t('tools.modAnalyzer.mods')}</span>
          {session.errorCount > 0 && <StatusTag tone="error" icon={null} label={`${session.errorCount} ${t('tools.modAnalyzer.broken')}`} />}
          {session.warningCount > 0 && <StatusTag tone="warning" icon={null} label={`${session.warningCount} ${t('tools.modAnalyzer.warnings')}`} />}
          {session.identicalCount > 0 && <StatusTag tone="error" icon={null} label={`${session.identicalCount} ${t('tools.modAnalyzer.identical')}`} />}
          {session.textureVariantCount > 0 && <StatusTag tone="warning" icon={null} label={`${session.textureVariantCount} ${t('tools.modAnalyzer.textureVariant')}`} />}
          {session.conflictCount > 0 && <StatusTag tone="error" icon={null} label={`${session.conflictCount} ${t('tools.modAnalyzer.conflicts')}`} />}
          {session.healthyCount > 0 && <StatusTag tone="success" icon={null} label={`${session.healthyCount} ${t('tools.modAnalyzer.healthy')}`} />}
        </div>
      </div>
    </CompactCard>
  );
};
