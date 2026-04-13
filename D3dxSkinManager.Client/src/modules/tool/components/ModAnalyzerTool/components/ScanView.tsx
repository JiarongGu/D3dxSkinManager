import React from 'react';
import { Progress } from 'antd';
import {
  PlayCircleOutlined,
  PauseCircleOutlined,
  RadarChartOutlined,
  CloseCircleOutlined,
  WarningOutlined,
  CheckCircleOutlined,
  HistoryOutlined,
  LoadingOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton } from '../../../../../shared/components/compact';
import { CategorySelect } from '../../../../../shared/components/CategorySelect';
import type { AnalysisProgress } from '../../../../../shared/types/analysis.types';
import type { CategoryInfo } from '../../../../../shared/types/category.types';

interface ScanViewProps {
  progress?: AnalysisProgress;
  scanning: boolean;
  categories: CategoryInfo[];
  selectedCategoryId?: string;
  onCategoryChange: (id?: string) => void;
  onStart: () => void;
  onPause: () => void;
  onViewHistory: () => void;
  sessionCount: number;
}

export const ScanView: React.FC<ScanViewProps> = ({
  progress, scanning, categories, selectedCategoryId, onCategoryChange, onStart, onPause, onViewHistory, sessionCount,
}) => {
  const { t } = useTranslation();
  const percent = progress && progress.total > 0 ? Math.round((progress.current / progress.total) * 100) : 0;

  // Not scanning → start screen
  if (!scanning) {
    return (
      <div className="mod-analyzer__scan-start">
        <div className="mod-analyzer__hero">
          <RadarChartOutlined className="mod-analyzer__hero-icon" />
          <div className="mod-analyzer__hero-title">{t('tools.modAnalyzer.title')}</div>
          <div className="mod-analyzer__hero-desc">{t('tools.modAnalyzer.scanHeroDesc')}</div>
          <div className="mod-analyzer__hero-controls">
            <CategorySelect
              categories={categories}
              value={selectedCategoryId}
              onChange={onCategoryChange}
              style={{ width: 200 }}
            />
            <CompactButton type="primary" icon={<PlayCircleOutlined />} onClick={onStart}>
              {t('tools.modAnalyzer.startScan')}
            </CompactButton>
            {sessionCount > 0 && (
              <CompactButton icon={<HistoryOutlined />} onClick={onViewHistory}>
                {t('tools.modAnalyzer.viewHistory')} ({sessionCount})
              </CompactButton>
            )}
          </div>
          <div className="mod-analyzer__hero-features">
            <FeatureTag icon={<CloseCircleOutlined />} label={t('tools.modAnalyzer.tabs.health')} />
            <FeatureTag icon={<WarningOutlined />} label={t('tools.modAnalyzer.staleOrMissing')} />
            <FeatureTag icon={<CheckCircleOutlined />} label={t('tools.modAnalyzer.tabs.duplicates')} />
            <FeatureTag icon={<RadarChartOutlined />} label={t('tools.modAnalyzer.tabs.conflicts')} />
          </div>
        </div>
      </div>
    );
  }

  // Active scanning
  return (
    <div className="mod-analyzer__scan-active">
      <div className="mod-analyzer__scan-top">
        <div className="mod-analyzer__scan-top-header">
          <div className="mod-analyzer__scan-top-left">
            {progress ? (
              <RadarChartOutlined className="mod-analyzer__scan-top-icon" spin />
            ) : (
              <LoadingOutlined className="mod-analyzer__scan-top-icon" />
            )}
            <span className="mod-analyzer__scan-top-title">
              {progress ? t('tools.modAnalyzer.scanRunning') : t('tools.modAnalyzer.preparing')}
            </span>
          </div>
          <div className="mod-analyzer__scan-top-actions">
            <CompactButton icon={<HistoryOutlined />} onClick={onViewHistory}>
              {t('tools.modAnalyzer.history')}
            </CompactButton>
            <CompactButton icon={<PauseCircleOutlined />} onClick={onPause} disabled={!progress}>
              {t('tools.modAnalyzer.pause')}
            </CompactButton>
          </div>
        </div>
        {progress && progress.total > 0 && (
          <>
            <div className="mod-analyzer__scan-progress-row">
              <span className="mod-analyzer__scan-progress-count">{progress.current} / {progress.total}</span>
              <Progress percent={percent} showInfo={false} strokeColor="var(--color-primary)" size={{ height: 6 }} className="mod-analyzer__scan-progress-bar" />
              <span className="mod-analyzer__scan-progress-pct">{percent}%</span>
            </div>
            {progress.currentModName && <div className="mod-analyzer__scan-current-mod">{progress.currentModName}</div>}
          </>
        )}
        {progress && (
          <div className="mod-analyzer__scan-stat-row">
            <StatPill icon={<CloseCircleOutlined />} color="var(--color-error)" value={progress.errorCount} label={t('tools.modAnalyzer.broken')} />
            <StatPill icon={<WarningOutlined />} color="var(--color-warning)" value={progress.warningCount} label={t('tools.modAnalyzer.warnings')} />
            <StatPill icon={<CheckCircleOutlined />} color="var(--color-success)" value={progress.healthyCount} label={t('tools.modAnalyzer.healthy')} />
          </div>
        )}
      </div>
      <div className="mod-analyzer__scan-feed">
        <div className="mod-analyzer__scan-feed-empty">{t('tools.modAnalyzer.scanFeedWaiting')}</div>
      </div>
    </div>
  );
};

const StatPill: React.FC<{ icon: React.ReactNode; color: string; value: number; label: string }> = ({ icon, color, value, label }) => (
  <div className="mod-analyzer__stat-pill">
    <span style={{ color }}>{icon}</span>
    <span className="mod-analyzer__stat-pill-value">{value}</span>
    <span className="mod-analyzer__stat-pill-label">{label}</span>
  </div>
);

const FeatureTag: React.FC<{ icon: React.ReactNode; label: string }> = ({ icon, label }) => (
  <span className="mod-analyzer__feature-tag">
    {icon}
    <span>{label}</span>
  </span>
);
