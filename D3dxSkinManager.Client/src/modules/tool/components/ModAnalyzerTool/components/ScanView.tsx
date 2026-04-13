import React, { useState, useEffect, useRef } from 'react';
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
  StopOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton } from '../../../../../shared/components/compact';
import { CategorySelect } from '../../../../../shared/components/CategorySelect';
import { HealthStatusIcon } from '../../../../../shared/components/common/HealthStatusIcon';
import type { AnalysisProgress } from '../../../../../shared/types/analysis.types';
import type { CategoryInfo } from '../../../../../shared/types/category.types';

interface FeedEntry {
  name: string;
  status: string;
}

interface ScanViewProps {
  progress?: AnalysisProgress;
  scanning: boolean;
  cancelling?: boolean;
  loading?: boolean;
  initialFeed?: FeedEntry[];
  categories: CategoryInfo[];
  selectedCategoryId?: string;
  onCategoryChange: (id?: string) => void;
  onStart: () => void;
  onPause: () => void;
  onResume: () => void;
  onCancel: () => void;
  onViewHistory: () => void;
  sessionCount: number;
}

export const ScanView: React.FC<ScanViewProps> = ({
  progress, scanning, cancelling, loading, initialFeed, categories, selectedCategoryId, onCategoryChange, onStart, onPause, onResume, onCancel, onViewHistory, sessionCount,
}) => {
  const { t } = useTranslation();
  const percent = progress && progress.total > 0 ? Math.round((progress.current / progress.total) * 100) : 0;

  // Accumulate live findings feed (seeded from initialFeed when resuming a running session)
  const [feed, setFeed] = useState<FeedEntry[]>([]);
  const feedEndRef = useRef<HTMLDivElement>(null);
  const lastEntryRef = useRef<string | undefined>(undefined);

  // Seed feed from initial data (when navigating from history to a running session)
  useEffect(() => {
    if (initialFeed && initialFeed.length > 0) {
      setFeed(initialFeed);
    }
  }, [initialFeed]);

  useEffect(() => {
    if (progress?.lastModName && progress.lastHealthStatus) {
      const key = `${progress.lastModName}:${progress.current}`;
      if (key !== lastEntryRef.current) {
        lastEntryRef.current = key;
        setFeed(prev => [...prev, { name: progress.lastModName!, status: progress.lastHealthStatus! }]);
      }
    }
  }, [progress]);

  // Auto-scroll to bottom
  useEffect(() => {
    feedEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [feed.length]);

  // Reset feed and dedup key when scan stops
  useEffect(() => {
    if (!scanning) {
      setFeed([]);
      lastEntryRef.current = undefined;
    }
  }, [scanning]);

  // Not scanning and not loading → start screen
  if (!scanning && !loading) {
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
  const isPaused = progress?.status === 'paused';

  return (
    <div className="mod-analyzer__scan-active">
      <div className="mod-analyzer__scan-top">
        <div className="mod-analyzer__scan-top-header">
          <div className="mod-analyzer__scan-top-left">
            {!progress ? (
              <LoadingOutlined className="mod-analyzer__scan-top-icon" />
            ) : isPaused ? (
              <PauseCircleOutlined className="mod-analyzer__scan-top-icon" style={{ color: 'var(--color-warning)' }} />
            ) : (
              <RadarChartOutlined className="mod-analyzer__scan-top-icon" spin />
            )}
            <span className="mod-analyzer__scan-top-title">
              {!progress ? t('tools.modAnalyzer.preparing') : isPaused ? t('tools.modAnalyzer.scanPaused') : t('tools.modAnalyzer.scanRunning')}
            </span>
          </div>
          <div className="mod-analyzer__scan-top-actions">
            <CompactButton icon={<HistoryOutlined />} onClick={onViewHistory}>
              {t('tools.modAnalyzer.history')}
            </CompactButton>
            {isPaused ? (
              <>
                <CompactButton type="primary" icon={<PlayCircleOutlined />} onClick={onResume} disabled={cancelling}>
                  {t('tools.modAnalyzer.resume')}
                </CompactButton>
                <CompactButton danger icon={<StopOutlined />} onClick={onCancel} loading={cancelling}>
                  {t('tools.modAnalyzer.cancel')}
                </CompactButton>
              </>
            ) : (
              <>
                <CompactButton icon={<PauseCircleOutlined />} onClick={onPause} disabled={!progress || cancelling}>
                  {t('tools.modAnalyzer.pause')}
                </CompactButton>
                <CompactButton danger icon={<StopOutlined />} onClick={onCancel} disabled={!progress} loading={cancelling}>
                  {t('tools.modAnalyzer.cancel')}
                </CompactButton>
              </>
            )}
          </div>
        </div>
        {progress && progress.total > 0 && (
          <>
            <div className="mod-analyzer__scan-progress-row">
              <span className="mod-analyzer__scan-progress-count">{progress.current} / {progress.total}</span>
              <Progress percent={percent} showInfo={false} strokeColor={isPaused ? 'var(--color-warning)' : 'var(--color-primary)'} size={{ height: 6 }} className="mod-analyzer__scan-progress-bar" />
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
        {!progress ? (
          <div className="mod-analyzer__scan-feed-empty">
            <LoadingOutlined style={{ fontSize: 20, marginBottom: 8 }} />
            <span>{t('tools.modAnalyzer.preparing')}</span>
          </div>
        ) : feed.length === 0 ? (
          <div className="mod-analyzer__scan-feed-empty">{t('tools.modAnalyzer.scanFeedWaiting')}</div>
        ) : (
          feed.map((entry, i) => (
            <div key={i} className="mod-analyzer__scan-feed-item">
              <HealthStatusIcon status={entry.status} />
              <span className="mod-analyzer__scan-feed-name">{entry.name}</span>
            </div>
          ))
        )}
        <div ref={feedEndRef} />
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
