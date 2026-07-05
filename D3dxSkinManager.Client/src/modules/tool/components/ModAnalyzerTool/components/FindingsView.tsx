import React, { useState, useMemo } from 'react';
import { Tag, Tooltip, Collapse, Empty, Dropdown } from 'antd';
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
  LoadingOutlined,
  AimOutlined,
  ToolFilled,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton, CompactInput, CompactIconButton } from '../../../../../shared/components/compact';
import { FormDialog } from '../../../../../shared/components/dialogs/FormDialog';
import { HealthStatusIcon } from '../../../../../shared/components/common/HealthStatusIcon';
import { StatusTag } from '../../../../../shared/components/common/StatusTag';
import { copyToClipboard } from '../../../../../shared/utils/clipboardHelper';
import { formatBytes } from '../../../../../shared/utils/formatBytes';
import type {
  FullAnalysisReport,
  ModAnalysisResult,
  DuplicateGroup,
  ModConflict,
} from '../../../../../shared/types/analysis.types';
import type { ModFixTool as FixToolInfo } from '../../../../../shared/types/modFix.types';

interface FindingsViewProps {
  report: FullAnalysisReport;
  scanning?: boolean;
  onNewScan: () => void;
  onRescan: () => void;
  onViewHistory: () => void;
  onDeleteMod?: (modId: string) => void;
  deletingModId?: string;
  onEditModName?: (modId: string, newName: string) => void;
  onLocateMods?: (modIds: string[]) => void;
  /** Dedup-assist: keep one mod of a duplicate group, delete the rest (staged + confirmed by the parent). */
  onResolveGroup?: (keep: ModAnalysisResult, groupMods: ModAnalysisResult[]) => void;
  /** Repair unbalanced if/endif in a mod's inis (needs an extracted cache). */
  onRepairIni?: (modId: string) => void;
  repairingModId?: string;
  /** Fix-tool library — enables the per-row "fix with…" dropdown (runs in place, analyzer stays open). */
  fixTools?: FixToolInfo[];
  onRunFix?: (toolName: string, entryPath: string, recompress: boolean, modId: string) => void;
  /** Controlled filter/search (persisted by the parent so close/reopen restores them). */
  filter?: FindingCategory;
  onFilterChange?: (f: FindingCategory) => void;
  search?: string;
  onSearchChange?: (s: string) => void;
}

export type FindingCategory = 'all' | 'broken' | 'stale' | 'duplicates' | 'conflicts' | 'healthy';

export const FindingsView: React.FC<FindingsViewProps> = ({ report, scanning, onNewScan, onRescan, onViewHistory, onDeleteMod, deletingModId, onEditModName, onLocateMods, onResolveGroup, onRepairIni, repairingModId, fixTools, onRunFix, filter, onFilterChange, search, onSearchChange }) => {
  const { t } = useTranslation();
  // Controlled when the parent persists them; internal state otherwise (tests, standalone use).
  const [internalSearch, setInternalSearch] = useState('');
  const [internalCategory, setInternalCategory] = useState<FindingCategory>('all');
  const searchText = search ?? internalSearch;
  const setSearchText = onSearchChange ?? setInternalSearch;
  const category = filter ?? internalCategory;
  const setCategory = onFilterChange ?? setInternalCategory;
  const [editingMod, setEditingMod] = useState<{ modId: string; currentName: string }>();
  const [editName, setEditName] = useState('');

  const brokenMods = useMemo(() => report.results.filter(r => r.healthStatus === 'error'), [report]);
  // "Needs attention": every warning-status mod (missing resources, unbalanced if/endif, …) plus
  // info-level stale-hash / missing-plugin hits — previously warning-only mods had NO section at all.
  const staleMods = useMemo(() => report.results.filter(r =>
    r.healthStatus === 'warning' ||
    r.issues.some(i => i.type === 'staleHash' || i.type === 'missingPlugin')
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
            {filteredContent.broken.map(mod => <ModRow key={mod.modId} mod={mod} onLocate={onLocateMods} onRepairIni={onRepairIni} repairing={repairingModId === mod.modId} fixTools={fixTools} onRunFix={onRunFix} />)}
          </FindingSection>
        )}

        {/* Stale / missing plugin */}
        {(category === 'all' || category === 'stale') && filteredContent.stale.length > 0 && (
          <FindingSection
            icon={<WarningOutlined style={{ color: 'var(--color-warning)' }} />}
            title={t('tools.modAnalyzer.staleHashOrPlugin')}
            count={filteredContent.stale.length}
          >
            {filteredContent.stale.map(mod => <ModRow key={mod.modId} mod={mod} onLocate={onLocateMods} onRepairIni={onRepairIni} repairing={repairingModId === mod.modId} fixTools={fixTools} onRunFix={onRunFix} />)}
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
                label: <DuplicateHeader group={g} onLocate={onLocateMods} />,
                children: <DuplicateDetail group={g} onDeleteMod={onDeleteMod} deletingModId={deletingModId} onStartEdit={(modId, name) => { setEditingMod({ modId, currentName: name }); setEditName(name); }} onResolveGroup={onResolveGroup} />,
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
            {filteredContent.conflicts.map((c, i) => <ConflictRow key={i} conflict={c} onLocate={onLocateMods} />)}
          </FindingSection>
        )}

        {/* Healthy */}
        {category === 'healthy' && filteredContent.healthy.length > 0 && (
          <FindingSection
            icon={<CheckCircleOutlined style={{ color: 'var(--color-success)' }} />}
            title={t('tools.modAnalyzer.healthyMods')}
            count={filteredContent.healthy.length}
          >
            {filteredContent.healthy.map(mod => <ModRow key={mod.modId} mod={mod} onLocate={onLocateMods} onRepairIni={onRepairIni} repairing={repairingModId === mod.modId} fixTools={fixTools} onRunFix={onRunFix} />)}
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
        <CompactInput
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

/** Short i18n'd label chip for an issue type — makes the issue list scannable by kind. */
const IssueTypeChip: React.FC<{ type: string }> = ({ type }) => {
  const { t } = useTranslation();
  return <Tag className="mod-analyzer__issue-type">{t(`tools.modAnalyzer.issueType.${type}`, { defaultValue: type })}</Tag>;
};

const ModRow: React.FC<{
  mod: ModAnalysisResult;
  onLocate?: (modIds: string[]) => void;
  onRepairIni?: (modId: string) => void;
  repairing?: boolean;
  fixTools?: FixToolInfo[];
  onRunFix?: (toolName: string, entryPath: string, recompress: boolean, modId: string) => void;
}> = ({ mod, onLocate, onRepairIni, repairing, fixTools, onRunFix }) => {
  const [expanded, setExpanded] = useState(false);
  const { t } = useTranslation();

  // "Fix with…" — the mod-list right-click Fix submenu, flattened, runnable IN PLACE from the
  // finding row (the fix is fire-and-forget → Activity panel; the analyzer never closes).
  type FixMenuItem = { key: string; label: string; disabled?: boolean; onClick?: () => void };
  const fixItems: FixMenuItem[] = (fixTools ?? []).flatMap((tf): FixMenuItem[] => {
    if (tf.entries.length === 0)
      return [{ key: `fix-${tf.id}`, label: `${tf.name} — ${t('tools.modFix.setEntryFirst')}`, disabled: true }];
    if (tf.entries.length === 1)
      return [{ key: `fix-${tf.id}`, label: tf.name, onClick: () => onRunFix?.(tf.name, tf.entries[0].path, tf.recompressDefault, mod.modId) }];
    return tf.entries.map((e) => ({
      key: `fix-${tf.id}-${e.name}`,
      label: `${tf.name} — ${e.name}`,
      onClick: () => onRunFix?.(tf.name, e.path, tf.recompressDefault, mod.modId),
    }));
  });
  if (fixItems.length === 0) fixItems.push({ key: 'fix-none', label: t('contextMenu.noFixTools'), disabled: true });

  return (
    <div className="mod-analyzer__mod-row">
      <div className="mod-analyzer__mod-row-main" onClick={() => mod.issues.length > 0 && setExpanded(!expanded)}>
        <HealthStatusIcon status={mod.healthStatus} />
        <span className="mod-analyzer__mod-row-name">{mod.modName}</span>
        <Tag>{mod.categoryName || 'Unclassified'}</Tag>
        {mod.isLoaded && <StatusTag tone="success" label={t('tools.modAnalyzer.loaded')} />}
        {mod.pluginDependencies.length > 0 && <Tag color="purple">{mod.pluginDependencies.join(', ')}</Tag>}
        {mod.issues.length > 0 && <StatusTag tone="error" icon={null} label={mod.issues.length} />}
        <span className="mod-analyzer__mod-row-meta">{mod.textureOverrideCount} {t('tools.modAnalyzer.overrides')}</span>
        <CopyIdButton modId={mod.modId} />
        {onRunFix && (
          <Dropdown menu={{ items: fixItems }} trigger={['click']}>
            <span onClick={e => e.stopPropagation()}>
              <Tooltip title={t('tools.modAnalyzer.fixWith')}>
                <CompactIconButton tone="primary" size={22} icon={<ThunderboltOutlined />} />
              </Tooltip>
            </span>
          </Dropdown>
        )}
        {onLocate && (
          <Tooltip title={t('tools.modAnalyzer.locateInModPanel')}>
            <AimOutlined
              className="mod-analyzer__locate-btn"
              onClick={e => { e.stopPropagation(); onLocate([mod.modId]); }}
            />
          </Tooltip>
        )}
      </div>
      {expanded && mod.issues.length > 0 && (
        <div className="mod-analyzer__mod-row-issues">
          {mod.issues.map((issue, idx) => (
            <div key={idx} className="mod-analyzer__issue-row">
              <HealthStatusIcon status={issue.severity} />
              <IssueTypeChip type={issue.type} />
              <span>{issue.message}</span>
              {issue.filePath && (
                <Tooltip title={issue.filePath}>
                  <span className="mod-analyzer__issue-file">{issue.filePath.split(/[\\/]/).pop()}</span>
                </Tooltip>
              )}
              {/* One-click repair for unbalanced if/endif (needs the extracted cache) */}
              {issue.type === 'unbalancedCondition' && onRepairIni &&
                idx === mod.issues.findIndex(i => i.type === 'unbalancedCondition') && (
                <Tooltip title={mod.hasCache ? t('tools.modAnalyzer.repairIni') : t('tools.modAnalyzer.repairNeedsCache')}>
                  <CompactButton
                    size="small"
                    icon={<ToolFilled />}
                    loading={repairing}
                    disabled={!mod.hasCache || repairing}
                    onClick={(e) => { e.stopPropagation(); onRepairIni(mod.modId); }}
                  >
                    {t('tools.modAnalyzer.repair')}
                  </CompactButton>
                </Tooltip>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

const DuplicateHeader: React.FC<{ group: DuplicateGroup; onLocate?: (modIds: string[]) => void }> = ({ group, onLocate }) => {
  const { t } = useTranslation();
  const isExactClone = group.type === 'identical' && group.allHashesMatch;
  const typeIcon = group.type === 'identical' ? <CopyOutlined /> : <BgColorsOutlined />;
  const typeColor = isExactClone ? 'red'
    : group.type === 'identical' ? 'volcano'
    : group.type === 'similar' ? 'gold'
    : group.type === 'iniVariant' ? 'geekblue'
    : 'orange';
  const typeLabel = isExactClone
    ? t('tools.modAnalyzer.exactClone')
    : group.type === 'identical' ? t('tools.modAnalyzer.identical')
    : group.type === 'similar' ? t('tools.modAnalyzer.similar')
    : group.type === 'iniVariant' ? t('tools.modAnalyzer.iniVariant')
    : t('tools.modAnalyzer.textureVariant');

  return (
    <div className="mod-analyzer__group-header">
      <Tag color={typeColor}>
        {typeIcon} {typeLabel}
        {group.type === 'similar' && group.similarityScore != null && ` ~${Math.round(group.similarityScore * 100)}%`}
      </Tag>
      {/* iniVariant: say WHAT differs between the copies (hash fix / keybinds / defaults / logic) */}
      {group.type === 'iniVariant' && (group.iniDifferences ?? []).map(d => (
        <Tag key={d} className="mod-analyzer__inidiff-tag">{t(`tools.modAnalyzer.iniDiff.${d}`, { defaultValue: d })}</Tag>
      ))}
      <span className="mod-analyzer__group-label">{group.groupLabel || t('tools.modAnalyzer.unknownGroup')}</span>
      <span className="mod-analyzer__group-count">{group.mods.length} {t('tools.modAnalyzer.mods')}</span>
      {group.sharedHashes.slice(0, 2).map(h => <Tag key={h} className="mod-analyzer__hash-tag">{h}</Tag>)}
      {group.sharedHashes.length > 2 && <Tag>+{group.sharedHashes.length - 2}</Tag>}
      {onLocate && (
        <Tooltip title={t('tools.modAnalyzer.locateGroupInModPanel')}>
          <AimOutlined
            className="mod-analyzer__locate-btn"
            onClick={e => { e.stopPropagation(); onLocate(group.mods.map(m => m.modId)); }}
          />
        </Tooltip>
      )}
    </div>
  );
};

const DuplicateDetail: React.FC<{ group: DuplicateGroup; onDeleteMod?: (modId: string) => void; deletingModId?: string; onStartEdit?: (modId: string, currentName: string) => void; onResolveGroup?: (keep: ModAnalysisResult, groupMods: ModAnalysisResult[]) => void }> = ({ group, onDeleteMod, deletingModId, onStartEdit, onResolveGroup }) => {
  const { t } = useTranslation();
  return (
    <div className="mod-analyzer__mod-cards">
      {group.mods.map(mod => {
        const isDeleting = deletingModId === mod.modId;
        return (
        <div key={mod.modId} className="mod-analyzer__mod-card">
          <div className={`mod-analyzer__mod-preview ${!mod.previewPath ? 'mod-analyzer__mod-preview--empty' : ''}`}>
            {mod.previewPath ? (
              <img src={`app://${encodeURIComponent(mod.previewPath)}`} alt={mod.modName} className="mod-analyzer__mod-preview-img" />
            ) : (
              <BgColorsOutlined />
            )}
            {isDeleting && (
              <div className="mod-analyzer__mod-preview-overlay">
                <LoadingOutlined style={{ fontSize: 24 }} spin />
                <span>{t('tools.modAnalyzer.deleting')}</span>
              </div>
            )}
            {!isDeleting && (
              <div className="mod-analyzer__mod-preview-actions">
                <CopyIdButton modId={mod.modId} />
                {onStartEdit && (
                  <Tooltip title={t('tools.modAnalyzer.editModName')}>
                    <EditOutlined
                      className="mod-analyzer__mod-edit"
                      onClick={e => { e.stopPropagation(); if (!deletingModId) onStartEdit(mod.modId, mod.modName); }}
                    />
                  </Tooltip>
                )}
                {onResolveGroup && group.mods.length > 1 && (
                  <Tooltip title={t('tools.modAnalyzer.keepThisOne')}>
                    <CheckCircleOutlined
                      className="mod-analyzer__mod-keep"
                      onClick={e => { e.stopPropagation(); if (!deletingModId) onResolveGroup(mod, group.mods); }}
                    />
                  </Tooltip>
                )}
                {onDeleteMod && (
                  <Tooltip title={t('tools.modAnalyzer.deleteDuplicate')}>
                    <DeleteOutlined
                      className="mod-analyzer__mod-delete"
                      onClick={e => { e.stopPropagation(); if (!deletingModId) onDeleteMod(mod.modId); }}
                    />
                  </Tooltip>
                )}
              </div>
            )}
          </div>
          <div className="mod-analyzer__mod-info">
            <div className="mod-analyzer__mod-name" title={mod.modName}>{mod.modName}</div>
            <div className="mod-analyzer__mod-meta">
              {mod.isLoaded && <StatusTag tone="success" icon={null} className="mod-analyzer__mod-tag" label={t('tools.modAnalyzer.loaded')} />}
              <Tag className="mod-analyzer__mod-tag">{formatBytes(mod.bufferSizeBytes)} buf</Tag>
              <Tag className="mod-analyzer__mod-tag">{formatBytes(mod.textureSizeBytes)} tex</Tag>
            </div>
          </div>
        </div>
        );
      })}
    </div>
  );
};

const ConflictRow: React.FC<{ conflict: ModConflict; onLocate?: (modIds: string[]) => void }> = ({ conflict, onLocate }) => {
  const { t } = useTranslation();
  return (
    <div className="mod-analyzer__conflict-item">
      <div className="mod-analyzer__conflict-header">
        <ThunderboltOutlined style={{ color: 'var(--color-error)' }} />
        <code className="mod-analyzer__hash-code">{conflict.hash}</code>
        <StatusTag tone="error" icon={null} label={`${conflict.mods.length} ${t('tools.modAnalyzer.mods')}`} />
        {onLocate && (
          <Tooltip title={t('tools.modAnalyzer.locateGroupInModPanel')}>
            <AimOutlined
              className="mod-analyzer__locate-btn"
              onClick={e => { e.stopPropagation(); onLocate(conflict.mods.map(m => m.modId)); }}
            />
          </Tooltip>
        )}
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


