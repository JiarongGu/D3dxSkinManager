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
import { CompactButton, CompactCard } from '../../../../../shared/components/compact';
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
          <div className="mod-analyzer__session-card-actions" onClick={e => e.stopPropagation()}>
            <CompactButton type="primary" icon={<EyeOutlined />} onClick={onView} />
            <CompactButton type="primary" danger icon={<DeleteOutlined />} onClick={onDelete} />
          </div>
        </div>
        <div className="mod-analyzer__session-card-stats">
          <span>{session.analyzedCount}/{session.totalMods} {t('tools.modAnalyzer.mods')}</span>
          {session.errorCount > 0 && <Tag color="error">{session.errorCount} {t('tools.modAnalyzer.broken')}</Tag>}
          {session.warningCount > 0 && <Tag color="warning">{session.warningCount} {t('tools.modAnalyzer.warnings')}</Tag>}
          {session.identicalCount > 0 && <Tag color="red">{session.identicalCount} {t('tools.modAnalyzer.identical')}</Tag>}
          {session.textureVariantCount > 0 && <Tag color="orange">{session.textureVariantCount} {t('tools.modAnalyzer.textureVariant')}</Tag>}
          {session.conflictCount > 0 && <Tag color="error">{session.conflictCount} {t('tools.modAnalyzer.conflicts')}</Tag>}
          {session.healthyCount > 0 && <Tag color="success">{session.healthyCount} {t('tools.modAnalyzer.healthy')}</Tag>}
        </div>
      </div>
    </CompactCard>
  );
};
