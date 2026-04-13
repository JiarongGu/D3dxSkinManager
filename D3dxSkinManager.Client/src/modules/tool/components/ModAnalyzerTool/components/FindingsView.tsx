import React, { useState, useMemo } from 'react';
import { Input, Tag, Tooltip, Collapse, Empty } from 'antd';
import {
  SearchOutlined,
  CloseCircleOutlined,
  WarningOutlined,
  CopyOutlined,
  BgColorsOutlined,
  ThunderboltOutlined,
  ReloadOutlined,
  PlayCircleOutlined,
  CheckCircleOutlined,
  HistoryOutlined,
  DeleteOutlined,
  EditOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton, CompactInput } from '../../../../../shared/components/compact';
import { FormDialog } from '../../../../../shared/components/dialogs/FormDialog';
import { HealthStatusIcon } from '../../../../../shared/components/common/HealthStatusIcon';
import { copyToClipboard } from '../../../../../shared/utils/clipboardHelper';
import { formatBytes } from '../../../../../shared/utils/formatBytes';
import type {
  FullAnalysisReport,
  ModAnalysisResult,
  DuplicateGroup,
  ModConflict,
} from '../../../../../shared/types/analysis.types';

interface FindingsViewProps {
  report: FullAnalysisReport;
  scanning?: boolean;
  onNewScan: () => void;
  onRescan: () => void;
  onViewHistory: () => void;
  onDeleteMod?: (modId: string) => void;
  onEditModName?: (modId: string, newName: string) => void;
}

type FindingCategory = 'all' | 'broken' | 'stale' | 'duplicates' | 'conflicts' | 'healthy';

export const FindingsView: React.FC<FindingsViewProps> = ({ report, scanning, onNewScan, onRescan, onViewHistory, onDeleteMod, onEditModName }) => {
  const { t } = useTranslation();
  const [searchText, setSearchText] = useState('');
  const [category, setCategory] = useState<FindingCategory>('all');
  const [editingMod, setEditingMod] = useState<{ modId: string; currentName: string }>();
  const [editName, setEditName] = useState('');

  const brokenMods = useMemo(() => report.results.filter(r => r.healthStatus === 'error'), [report]);
  const staleMods = useMemo(() => report.results.filter(r =>
    r.issues.some(i => i.type === 'staleHash')
  ), [report]);
  const healthyMods = useMemo(() => report.results.filter(r => r.healthStatus === 'healthy'), [report]);

  const filteredContent = useMemo(() => {
    const lower = searchText.toLowerCase();
    const filterMod = (m: ModAnalysisResult) =>
      !searchText || m.modName.toLowerCase().includes(lower) || m.categoryName.toLowerCase().includes(lower);
    const filterGroup = (g: DuplicateGroup) =>
      !searchText || g.mods.some(m => filterMod(m));
    const filterConflict = (c: ModConflict) =>
      !searchText || c.hash.toLowerCase().includes(lower) || c.mods.some(m => filterMod(m));

    return {
      broken: brokenMods.filter(filterMod),
      stale: staleMods.filter(filterMod),
      duplicates: report.duplicateGroups.filter(filterGroup),
      conflicts: report.conflicts.filter(filterConflict),
      healthy: healthyMods.filter(filterMod),
    };
  }, [report, brokenMods, staleMods, healthyMods, searchText]);

  return (
    <div className="mod-analyzer__findings">
      {/* Header bar */}
      <div className="mod-analyzer__findings-header">
        <div className="mod-analyzer__findings-header-left">
          <span className="mod-analyzer__findings-summary">
            {report.analyzedCount}/{report.totalMods} {t('tools.modAnalyzer.modsAnalyzed')}
          </span>
          <CompactButton icon={<PlayCircleOutlined />} onClick={onNewScan} disabled={scanning}>
            {t('tools.modAnalyzer.newScan')}
          </CompactButton>
          <CompactButton icon={<ReloadOutlined />} onClick={onRescan} disabled={scanning}>
            {t('tools.modAnalyzer.rescan')}
          </CompactButton>
          <CompactButton icon={<HistoryOutlined />} onClick={onViewHistory}>
            {t('tools.modAnalyzer.history')}
          </CompactButton>
        </div>
        <CompactInput
          prefix={<SearchOutlined />}
          placeholder={t('tools.modAnalyzer.searchFindings')}
          value={searchText}
          onChange={e => setSearchText(e.target.value)}
          allowClear
          style={{ width: 240 }}
        />
      </div>

      {/* Category filter chips */}
      <div className="mod-analyzer__findings-filters">
        <FilterChip active={category === 'all'} onClick={() => setCategory('all')} label={t('tools.modAnalyzer.filterAll')} count={report.results.length} />
        <FilterChip active={category === 'broken'} onClick={() => setCategory('broken')} label={t('tools.modAnalyzer.broken')} count={brokenMods.length} color="error" />
        <FilterChip active={category === 'stale'} onClick={() => setCategory('stale')} label={t('tools.modAnalyzer.staleOrMissing')} count={staleMods.length} color="warning" />
        <FilterChip active={category === 'duplicates'} onClick={() => setCategory('duplicates')} label={t('tools.modAnalyzer.duplicateGroups')} count={report.duplicateGroups.length} color="processing" />
        <FilterChip active={category === 'conflicts'} onClick={() => setCategory('conflicts')} label={t('tools.modAnalyzer.conflicts')} count={report.conflictCount} color="error" />
        <FilterChip active={category === 'healthy'} onClick={() => setCategory('healthy')} label={t('tools.modAnalyzer.healthy')} count={healthyMods.length} color="success" />
      </div>

      {/* Findings list */}
      <div className="mod-analyzer__findings-list">
        {/* Broken mods */}
        {(category === 'all' || category === 'broken') && filteredContent.broken.length > 0 && (
          <FindingSection
            icon={<CloseCircleOutlined style={{ color: 'var(--color-error)' }} />}
            title={t('tools.modAnalyzer.brokenMods')}
            count={filteredContent.broken.length}
          >
            {filteredContent.broken.map(mod => <ModRow key={mod.modId} mod={mod} />)}
          </FindingSection>
        )}

        {/* Stale / missing plugin */}
        {(category === 'all' || category === 'stale') && filteredContent.stale.length > 0 && (
          <FindingSection
            icon={<WarningOutlined style={{ color: 'var(--color-warning)' }} />}
            title={t('tools.modAnalyzer.staleHashOrPlugin')}
            count={filteredContent.stale.length}
          >
            {filteredContent.stale.map(mod => <ModRow key={mod.modId} mod={mod} />)}
          </FindingSection>
        )}

        {/* Duplicate groups */}
        {(category === 'all' || category === 'duplicates') && filteredContent.duplicates.length > 0 && (
          <FindingSection
            icon={<CopyOutlined style={{ color: 'var(--color-info, #1890ff)' }} />}
            title={t('tools.modAnalyzer.duplicateGroups')}
            count={filteredContent.duplicates.length}
          >
            <Collapse
              className="mod-analyzer__groups"
              items={filteredContent.duplicates.map((g, i) => ({
                key: i,
                label: <DuplicateHeader group={g} />,
                children: <DuplicateDetail group={g} onDeleteMod={onDeleteMod} onStartEdit={(modId, name) => { setEditingMod({ modId, currentName: name }); setEditName(name); }} />,
              }))}
            />
          </FindingSection>
        )}

        {/* Conflicts */}
        {(category === 'all' || category === 'conflicts') && filteredContent.conflicts.length > 0 && (
          <FindingSection
            icon={<ThunderboltOutlined style={{ color: 'var(--color-error)' }} />}
            title={t('tools.modAnalyzer.hashConflicts')}
            count={filteredContent.conflicts.length}
          >
            {filteredContent.conflicts.map((c, i) => <ConflictRow key={i} conflict={c} />)}
          </FindingSection>
        )}

        {/* Healthy */}
        {category === 'healthy' && filteredContent.healthy.length > 0 && (
          <FindingSection
            icon={<CheckCircleOutlined style={{ color: 'var(--color-success)' }} />}
            title={t('tools.modAnalyzer.healthyMods')}
            count={filteredContent.healthy.length}
          >
            {filteredContent.healthy.map(mod => <ModRow key={mod.modId} mod={mod} />)}
          </FindingSection>
        )}

        {/* Empty state — per active filter */}
        {(() => {
          let isEmpty = false;
          switch (category) {
            case 'all':
              isEmpty = filteredContent.broken.length === 0 && filteredContent.stale.length === 0 &&
                        filteredContent.duplicates.length === 0 && filteredContent.conflicts.length === 0;
              break;
            case 'broken': isEmpty = filteredContent.broken.length === 0; break;
            case 'stale': isEmpty = filteredContent.stale.length === 0; break;
            case 'duplicates': isEmpty = filteredContent.duplicates.length === 0; break;
            case 'conflicts': isEmpty = filteredContent.conflicts.length === 0; break;
            case 'healthy': isEmpty = filteredContent.healthy.length === 0; break;
          }
          return isEmpty ? (
            <div className="mod-analyzer__empty"><Empty description={t('tools.modAnalyzer.noFindings')} /></div>
          ) : null;
        })()}
      </div>

      {/* Edit mod name dialog */}
      <FormDialog
        visible={!!editingMod}
        title={t('tools.modAnalyzer.editModNameTitle')}
        onOk={async () => {
          if (editingMod && editName.trim() && onEditModName) {
            onEditModName(editingMod.modId, editName.trim());
          }
          setEditingMod(undefined);
        }}
        onCancel={() => setEditingMod(undefined)}
      >
        <Input
          value={editName}
          onChange={e => setEditName(e.target.value)}
          onPressEnter={() => {
            if (editingMod && editName.trim() && onEditModName) {
              onEditModName(editingMod.modId, editName.trim());
            }
            setEditingMod(undefined);
          }}
          autoFocus
        />
      </FormDialog>
    </div>
  );
};

// ===== Sub-components =====

const FilterChip: React.FC<{ active: boolean; onClick: () => void; label: string; count: number; color?: string }> = ({
  active, onClick, label, count, color,
}) => (
  <Tag
    className={`mod-analyzer__filter-chip ${active ? 'mod-analyzer__filter-chip--active' : ''}`}
    color={active ? color || 'blue' : undefined}
    onClick={onClick}
  >
    {label} ({count})
  </Tag>
);

const FindingSection: React.FC<{ icon: React.ReactNode; title: string; count: number; children: React.ReactNode }> = ({
  icon, title, count, children,
}) => (
  <div className="mod-analyzer__finding-section">
    <div className="mod-analyzer__finding-section-header">
      {icon}
      <span className="mod-analyzer__finding-section-title">{title}</span>
      <Tag>{count}</Tag>
    </div>
    <div className="mod-analyzer__finding-section-content">{children}</div>
  </div>
);

const CopyIdButton: React.FC<{ modId: string }> = ({ modId }) => {
  const { t } = useTranslation();
  return (
    <Tooltip title={modId}>
      <CopyOutlined
        className="mod-analyzer__copy-id"
        onClick={e => { e.stopPropagation(); void copyToClipboard(modId, t('mods.notifications.idCopied')); }}
      />
    </Tooltip>
  );
};

const ModRow: React.FC<{ mod: ModAnalysisResult }> = ({ mod }) => {
  const [expanded, setExpanded] = useState(false);
  const { t } = useTranslation();

  return (
    <div className="mod-analyzer__mod-row">
      <div className="mod-analyzer__mod-row-main" onClick={() => mod.issues.length > 0 && setExpanded(!expanded)}>
        <HealthStatusIcon status={mod.healthStatus} />
        <span className="mod-analyzer__mod-row-name">{mod.modName}</span>
        <Tag>{mod.categoryName || 'Unclassified'}</Tag>
        {mod.isLoaded && <Tag color="green">{t('tools.modAnalyzer.loaded')}</Tag>}
        {mod.pluginDependencies.length > 0 && <Tag color="purple">{mod.pluginDependencies.join(', ')}</Tag>}
        {mod.issues.length > 0 && <Tag color="error">{mod.issues.length}</Tag>}
        <span className="mod-analyzer__mod-row-meta">{mod.textureOverrideCount} {t('tools.modAnalyzer.overrides')}</span>
        <CopyIdButton modId={mod.modId} />
      </div>
      {expanded && mod.issues.length > 0 && (
        <div className="mod-analyzer__mod-row-issues">
          {mod.issues.map((issue, idx) => (
            <div key={idx} className="mod-analyzer__issue-row">
              <HealthStatusIcon status={issue.severity} />
              <span>{issue.message}</span>
              {issue.filePath && (
                <Tooltip title={issue.filePath}>
                  <span className="mod-analyzer__issue-file">{issue.filePath.split(/[\\/]/).pop()}</span>
                </Tooltip>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

const DuplicateHeader: React.FC<{ group: DuplicateGroup }> = ({ group }) => {
  const { t } = useTranslation();
  const isExactClone = group.type === 'identical' && group.allHashesMatch;
  const typeIcon = group.type === 'identical' ? <CopyOutlined /> : <BgColorsOutlined />;
  const typeColor = isExactClone ? 'red' : group.type === 'identical' ? 'volcano' : 'orange';
  const typeLabel = isExactClone
    ? t('tools.modAnalyzer.exactClone')
    : group.type === 'identical' ? t('tools.modAnalyzer.identical') : t('tools.modAnalyzer.textureVariant');

  return (
    <div className="mod-analyzer__group-header">
      <Tag color={typeColor}>{typeIcon} {typeLabel}</Tag>
      <span className="mod-analyzer__group-label">{group.groupLabel || t('tools.modAnalyzer.unknownGroup')}</span>
      <span className="mod-analyzer__group-count">{group.mods.length} {t('tools.modAnalyzer.mods')}</span>
      {group.sharedHashes.slice(0, 2).map(h => <Tag key={h} className="mod-analyzer__hash-tag">{h}</Tag>)}
      {group.sharedHashes.length > 2 && <Tag>+{group.sharedHashes.length - 2}</Tag>}
    </div>
  );
};

const DuplicateDetail: React.FC<{ group: DuplicateGroup; onDeleteMod?: (modId: string) => void; onStartEdit?: (modId: string, currentName: string) => void }> = ({ group, onDeleteMod, onStartEdit }) => {
  const { t } = useTranslation();
  return (
    <div className="mod-analyzer__mod-cards">
      {group.mods.map(mod => (
        <div key={mod.modId} className="mod-analyzer__mod-card">
          <div className={`mod-analyzer__mod-preview ${!mod.previewPath ? 'mod-analyzer__mod-preview--empty' : ''}`}>
            {mod.previewPath ? (
              <img src={`app://${encodeURIComponent(mod.previewPath)}`} alt={mod.modName} className="mod-analyzer__mod-preview-img" />
            ) : (
              <BgColorsOutlined />
            )}
            <div className="mod-analyzer__mod-preview-actions">
              <CopyIdButton modId={mod.modId} />
              {onStartEdit && (
                <Tooltip title={t('tools.modAnalyzer.editModName')}>
                  <EditOutlined
                    className="mod-analyzer__mod-edit"
                    onClick={e => { e.stopPropagation(); onStartEdit(mod.modId, mod.modName); }}
                  />
                </Tooltip>
              )}
              {onDeleteMod && (
                <Tooltip title={t('tools.modAnalyzer.deleteDuplicate')}>
                  <DeleteOutlined
                    className="mod-analyzer__mod-delete"
                    onClick={e => { e.stopPropagation(); onDeleteMod(mod.modId); }}
                  />
                </Tooltip>
              )}
            </div>
          </div>
          <div className="mod-analyzer__mod-info">
            <div className="mod-analyzer__mod-name" title={mod.modName}>{mod.modName}</div>
            <div className="mod-analyzer__mod-meta">
              {mod.isLoaded && <Tag color="green" className="mod-analyzer__mod-tag">Loaded</Tag>}
              <Tag className="mod-analyzer__mod-tag">{formatBytes(mod.bufferSizeBytes)} buf</Tag>
              <Tag className="mod-analyzer__mod-tag">{formatBytes(mod.textureSizeBytes)} tex</Tag>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
};

const ConflictRow: React.FC<{ conflict: ModConflict }> = ({ conflict }) => {
  const { t } = useTranslation();
  return (
    <div className="mod-analyzer__conflict-item">
      <div className="mod-analyzer__conflict-header">
        <ThunderboltOutlined style={{ color: 'var(--color-error)' }} />
        <code className="mod-analyzer__hash-code">{conflict.hash}</code>
        <Tag color="error">{conflict.mods.length} {t('tools.modAnalyzer.mods')}</Tag>
      </div>
      <div className="mod-analyzer__conflict-mods">
        {conflict.mods.map(mod => (
          <div key={mod.modId} className="mod-analyzer__conflict-mod">
            {mod.previewPath ? (
              <img src={`app://${encodeURIComponent(mod.previewPath)}`} alt={mod.modName} className="mod-analyzer__conflict-preview" />
            ) : (
              <div className="mod-analyzer__conflict-preview mod-analyzer__conflict-preview--empty"><ThunderboltOutlined /></div>
            )}
            <div className="mod-analyzer__conflict-mod-info">
              <span className="mod-analyzer__conflict-mod-name">{mod.modName}</span>
              <CopyIdButton modId={mod.modId} />
              <Tag>{mod.categoryName || 'Unclassified'}</Tag>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};


