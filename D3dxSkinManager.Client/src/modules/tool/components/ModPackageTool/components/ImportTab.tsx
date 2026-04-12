import React, { useCallback, useMemo, useState } from 'react';
import { Input, Checkbox, Tag, Select, Progress, Row, Col, List, Empty, Descriptions } from 'antd';
import { FolderOpenOutlined, InboxOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton, CompactSpace, CompactCard, CompactAlert, CompactDivider } from '../../../../../shared/components/compact';
import { toAppUrl } from '../../../../../shared/utils/imageUrlHelper';
import { useModPackage } from '../context/ModPackageContext';
import { useProfile } from '../../../../../shared/context/ProfileContext';
import { api } from '../../../../../shared/services/ipc';
import logger from '../../../../../shared/utils/logger';
import type { AnalyzedModEntry } from '../../../../../shared/types/modPackage.types';

const { Search } = Input;

const statusColors: Record<string, string> = { new: 'green', update: 'orange' };
const statusKeys: Record<string, string> = {
  new: 'tools.modPackage.import.new',
  update: 'tools.modPackage.import.update',
};

const changedFieldLabels: Record<string, string> = {
  name: 'tools.modPackage.import.fieldName',
  author: 'tools.modPackage.import.fieldAuthor',
  description: 'tools.modPackage.import.fieldDescription',
  tags: 'tools.modPackage.import.fieldTags',
  grading: 'tools.modPackage.import.fieldGrading',
  category: 'tools.modPackage.import.fieldCategory',
  archive: 'tools.modPackage.import.fieldArchive',
};

export const ImportTab: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const {
    packagePath, setPackagePath,
    analysis, setAnalysis,
    selectedImportIds, setSelectedImportIds,
    importResult, importStatus,
    progress, loading, setLoading,
    startImport, resetImport,
  } = useModPackage();

  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<string>();
  const [statusFilter, setStatusFilter] = useState<string>();
  const [selectedModId, setSelectedModId] = useState<string>();
  const [updateExisting, setUpdateExisting] = useState(true);
  const [importPreviews, setImportPreviews] = useState(true);
  const [createCategories, setCreateCategories] = useState(true);

  const handleBrowse = useCallback(async () => {
    if (!selectedProfileId) return;
    const dialogResult = await api.system.openFolderDialog({
      title: t('tools.modPackage.import.selectPackageFolder'),
      rememberPathKey: 'mod_import',
    });
    if (!dialogResult.success || !dialogResult.filePath) return;

    setPackagePath(dialogResult.filePath);
    setLoading(true);
    setSelectedModId(undefined);
    try {
      const result = await api.tool.analyzeModPackage(selectedProfileId, dialogResult.filePath);
      setAnalysis(result);
      if (result.isValid) {
        setSelectedImportIds(new Set(
          result.mods.filter(m => m.status === 'new' || m.status === 'update').map(m => m.id),
        ));
      }
    } catch (error) {
      logger.error('[ModPackage] Analysis failed', error);
    } finally {
      setLoading(false);
    }
  }, [selectedProfileId, setPackagePath, setAnalysis, setLoading, setSelectedImportIds, t]);

  // Build category options from the package's category paths
  const categoryOptions = useMemo(() => {
    if (!analysis?.mods) return [];
    const paths = new Set(analysis.mods.map(m => m.categoryPath).filter(Boolean));
    return [
      { value: '__all__', label: t('tools.modPackage.export.allCategories') },
      ...Array.from(paths).sort().map(p => ({ value: p, label: p })),
    ];
  }, [analysis, t]);

  const statusOptions = useMemo(() => [
    { value: '__all__', label: t('tools.modPackage.import.allStatus') },
    { value: 'new', label: t('tools.modPackage.import.new') },
    { value: 'update', label: t('tools.modPackage.import.update') },
  ], [t]);

  const filteredMods = useMemo(() => {
    if (!analysis?.mods) return [];
    const lowerSearch = search.toLowerCase();
    return analysis.mods.filter(m => {
      if (statusFilter && statusFilter !== '__all__' && m.status !== statusFilter) return false;
      if (categoryFilter && categoryFilter !== '__all__' && m.categoryPath !== categoryFilter) return false;
      if (lowerSearch) {
        return m.name.toLowerCase().includes(lowerSearch)
          || m.author?.toLowerCase().includes(lowerSearch)
          || m.tags?.some(tag => tag.toLowerCase().includes(lowerSearch))
          || m.categoryPath?.toLowerCase().includes(lowerSearch);
      }
      return true;
    });
  }, [analysis, search, categoryFilter, statusFilter]);

  const selectedMod = useMemo(() => {
    if (!selectedModId || !analysis) return undefined;
    return analysis.mods.find(m => m.id === selectedModId);
  }, [selectedModId, analysis]);

  const handleToggleMod = useCallback((modId: string, checked: boolean) => {
    setSelectedImportIds(prev => {
      const next = new Set(prev);
      if (checked) next.add(modId);
      else next.delete(modId);
      return next;
    });
  }, [setSelectedImportIds]);

  const handleSelectAll = useCallback(() => {
    if (!analysis) return;
    setSelectedImportIds(new Set(analysis.mods.map(m => m.id)));
  }, [analysis, setSelectedImportIds]);

  const handleDeselectAll = useCallback(() => {
    setSelectedImportIds(new Set());
  }, [setSelectedImportIds]);

  const handleStartImport = useCallback(() => {
    void startImport({ updateExisting, importPreviews, createCategories });
  }, [startImport, updateExisting, importPreviews, createCategories]);

  // Running state
  if (importStatus === 'running') {
    const percent = progress ? Math.round((progress.current / progress.total) * 100) : 0;
    return (
      <div className="mod-transfer__status">
        <CompactSpace vertical className="mod-transfer__status-inner">
          <CompactAlert title={t('tools.modPackage.import.importing')} type="info" showIcon extraCompact />
          <CompactCard extraCompact>
            <Progress percent={percent} status="active" strokeColor={{ '0%': '#108ee9', '100%': '#87d068' }} />
            {progress && (
              <div className="mod-transfer__status-detail">
                {t('tools.modPackage.progressDetail', { current: progress.current, total: progress.total, name: progress.currentModName })}
              </div>
            )}
          </CompactCard>
        </CompactSpace>
      </div>
    );
  }

  // Done state
  if (importStatus === 'done' && importResult) {
    const hasErrors = importResult.failedCount > 0;
    return (
      <div className="mod-transfer__status">
        <CompactSpace vertical className="mod-transfer__status-inner">
          <CompactAlert
            title={hasErrors ? t('tools.modPackage.import.partialSuccess') : t('tools.modPackage.import.success')}
            description={t('tools.modPackage.import.successDetail', {
              imported: importResult.importedCount,
              updated: importResult.updatedCount,
              skipped: importResult.skippedCount,
            })}
            type={hasErrors ? 'warning' : 'success'}
            showIcon
            extraCompact
          />
          <CompactCard title={t('tools.modPackage.import.resultSummary')} extraCompact className="mod-transfer__result-card">
            <Row gutter={8}>
              <Col span={6}>
                <div className="mod-transfer__stat">
                  <div className="mod-transfer__stat-label">{t('tools.modPackage.import.imported')}</div>
                  <div className="mod-transfer__stat-value">{importResult.importedCount}</div>
                </div>
              </Col>
              <Col span={6}>
                <div className="mod-transfer__stat">
                  <div className="mod-transfer__stat-label">{t('tools.modPackage.import.updated')}</div>
                  <div className="mod-transfer__stat-value">{importResult.updatedCount}</div>
                </div>
              </Col>
              <Col span={6}>
                <div className="mod-transfer__stat">
                  <div className="mod-transfer__stat-label">{t('tools.modPackage.import.skipped')}</div>
                  <div className="mod-transfer__stat-value">{importResult.skippedCount}</div>
                </div>
              </Col>
              <Col span={6}>
                <div className="mod-transfer__stat">
                  <div className="mod-transfer__stat-label">{t('tools.modPackage.import.failed')}</div>
                  <div className="mod-transfer__stat-value">{importResult.failedCount}</div>
                </div>
              </Col>
            </Row>
            {importResult.errors.length > 0 && (
              <>
                <CompactDivider extraCompact />
                <CompactAlert
                  title={t('tools.modPackage.errors') + ` (${importResult.errors.length})`}
                  description={
                    <List
                      size="small"
                      dataSource={importResult.errors.slice(0, 5)}
                      renderItem={(error) => <List.Item>{error}</List.Item>}
                    />
                  }
                  type="error"
                  showIcon
                  extraCompact
                />
              </>
            )}
          </CompactCard>
          <CompactButton onClick={resetImport}>{t('tools.modPackage.import.importMore')}</CompactButton>
        </CompactSpace>
      </div>
    );
  }

  // No package selected — empty state
  if (!analysis) {
    return (
      <div className="mod-transfer__import">
        <div className="mod-transfer__browse-bar">
          <CompactButton icon={<FolderOpenOutlined />} onClick={() => void handleBrowse()} loading={loading}>
            {t('tools.modPackage.import.browseFolder')}
          </CompactButton>
          {packagePath && <code className="mod-transfer__path">{packagePath}</code>}
        </div>
        <div className="mod-transfer__empty-state">
          <Empty description={t('tools.modPackage.import.noPackageSelected')} />
        </div>
      </div>
    );
  }

  // Invalid package
  if (!analysis.isValid) {
    return (
      <div className="mod-transfer__import">
        <div className="mod-transfer__browse-bar">
          <CompactButton icon={<FolderOpenOutlined />} onClick={() => void handleBrowse()} loading={loading}>
            {t('tools.modPackage.import.browseFolder')}
          </CompactButton>
          {packagePath && <code className="mod-transfer__path">{packagePath}</code>}
        </div>
        <div className="mod-transfer__status">
          <CompactAlert
            title={analysis.errorMessage || t('tools.modPackage.import.invalidPackage')}
            type="error"
            showIcon
          />
        </div>
      </div>
    );
  }

  // Main import UI — two-panel layout
  const newCount = analysis.mods.filter(m => m.status === 'new').length;
  const updateCount = analysis.mods.filter(m => m.status === 'update').length;

  return (
    <div className="mod-transfer__import">
      {/* Browse bar + options */}
      <div className="mod-transfer__browse-bar">
        <CompactButton icon={<FolderOpenOutlined />} onClick={() => void handleBrowse()} loading={loading}>
          {t('tools.modPackage.import.browseFolder')}
        </CompactButton>
        <code className="mod-transfer__path">{packagePath}</code>
        <span className="mod-transfer__config-divider" />
        <Checkbox checked={updateExisting} onChange={e => setUpdateExisting(e.target.checked)}>
          {t('tools.modPackage.import.updateExisting')}
        </Checkbox>
        <Checkbox checked={importPreviews} onChange={e => setImportPreviews(e.target.checked)}>
          {t('tools.modPackage.import.importPreviews')}
        </Checkbox>
        <Checkbox checked={createCategories} onChange={e => setCreateCategories(e.target.checked)}>
          {t('tools.modPackage.import.createCategories')}
        </Checkbox>
      </div>

      {/* Two-panel layout */}
      <div className="mod-transfer__panels">
        {/* Left: Package mod list */}
        <div className="mod-transfer__panel">
          <div className="mod-transfer__panel-header">
            <span className="mod-transfer__panel-title">
              {t('tools.modPackage.import.packageMods')} ({analysis.mods.length})
            </span>
            <CompactSpace>
              <CompactButton size="small" onClick={handleSelectAll}>{t('tools.modPackage.selectAll')}</CompactButton>
              <CompactButton size="small" onClick={handleDeselectAll}>{t('tools.modPackage.deselectAll')}</CompactButton>
            </CompactSpace>
          </div>
          <div className="mod-transfer__panel-filters">
            <Search
              placeholder={t('tools.modPackage.searchMods')}
              value={search}
              onChange={e => setSearch(e.target.value)}
              allowClear
              size="middle"
              className="mod-transfer__search"
            />
            <Select
              value={categoryFilter || '__all__'}
              onChange={v => setCategoryFilter(v === '__all__' ? undefined : v)}
              options={categoryOptions}
              size="middle"
              className="mod-transfer__category-filter"
              popupMatchSelectWidth={false}
            />
            <Select
              value={statusFilter || '__all__'}
              onChange={v => setStatusFilter(v === '__all__' ? undefined : v)}
              options={statusOptions}
              size="middle"
              className="mod-transfer__status-filter"
              popupMatchSelectWidth={false}
            />
          </div>
          <div className="mod-transfer__panel-list">
            {filteredMods.map(mod => (
              <div
                key={mod.id}
                className={`mod-transfer__item ${selectedModId === mod.id ? 'mod-transfer__item--active' : ''}`}
                onClick={() => setSelectedModId(mod.id)}
              >
                <Checkbox
                  checked={selectedImportIds.has(mod.id)}
                  onChange={e => {
                    e.stopPropagation();
                    handleToggleMod(mod.id, e.target.checked);
                  }}
                  onClick={e => e.stopPropagation()}
                />
                <div className="mod-transfer__item-info">
                  <span className="mod-transfer__item-name">{mod.name}</span>
                  <span className="mod-transfer__item-meta">
                    {mod.author && <span>{mod.author}</span>}
                    {mod.author && mod.categoryPath && <span className="mod-transfer__meta-sep">/</span>}
                    {mod.categoryPath && <span>{mod.categoryPath}</span>}
                  </span>
                </div>
                <Tag color={statusColors[mod.status]}>{t(statusKeys[mod.status])}</Tag>
              </div>
            ))}
          </div>
        </div>

        {/* Right: Mod detail */}
        <div className="mod-transfer__panel">
          <div className="mod-transfer__panel-header">
            <span className="mod-transfer__panel-title">
              {t('tools.modPackage.import.modDetail')}
            </span>
          </div>
          <div className="mod-transfer__panel-list">
            {selectedMod ? (
              <ModDetailView mod={selectedMod} />
            ) : (
              <div className="mod-transfer__empty-drop">
                <InboxOutlined className="mod-transfer__empty-icon" />
                <span>{t('tools.modPackage.import.selectModToPreview')}</span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Action bar */}
      <div className="mod-transfer__action-bar">
        <CompactButton
          type="primary"
          onClick={handleStartImport}
          disabled={selectedImportIds.size === 0}
        >
          {t('tools.modPackage.import.startImport')} ({selectedImportIds.size})
        </CompactButton>
      </div>
    </div>
  );
};

/** Detail view for a single mod from the package */
const ModDetailView: React.FC<{ mod: AnalyzedModEntry }> = ({ mod }) => {
  const { t } = useTranslation();

  return (
    <div className="mod-transfer__detail">
      <div className="mod-transfer__detail-header">
        <span className="mod-transfer__detail-name">{mod.name}</span>
        <Tag color={statusColors[mod.status]}>{t(statusKeys[mod.status])}</Tag>
      </div>

      <Descriptions column={1} size="small" className="mod-transfer__detail-desc">
        <Descriptions.Item label={t('tools.modPackage.import.fieldAuthor')}>{mod.author || '—'}</Descriptions.Item>
        <Descriptions.Item label={t('tools.modPackage.import.fieldCategory')}>{mod.categoryPath || '—'}</Descriptions.Item>
        <Descriptions.Item label={t('tools.modPackage.import.fieldGrading')}>{mod.grading}</Descriptions.Item>
        <Descriptions.Item label={t('tools.modPackage.import.fieldTags')}>
          {mod.tags.length > 0 ? mod.tags.map(tag => <Tag key={tag}>{tag}</Tag>) : '—'}
        </Descriptions.Item>
        {mod.description && (
          <Descriptions.Item label={t('tools.modPackage.import.fieldDescription')}>{mod.description}</Descriptions.Item>
        )}
        <Descriptions.Item label={t('tools.modPackage.import.fieldArchive')}>
          {mod.hasArchive ? <Tag color="blue">{t('tools.modPackage.import.yes')}</Tag> : <Tag>{t('tools.modPackage.import.no')}</Tag>}
        </Descriptions.Item>
        <Descriptions.Item label={t('tools.modPackage.import.fieldPreviews')}>
          {mod.hasPreviews ? <Tag color="green">{t('tools.modPackage.import.yes')}</Tag> : <Tag>{t('tools.modPackage.import.no')}</Tag>}
        </Descriptions.Item>
      </Descriptions>

      {mod.status === 'update' && mod.changedFields.length > 0 && (
        <>
          <CompactDivider extraCompact />
          <div className="mod-transfer__detail-changes">
            <div className="mod-transfer__detail-changes-title">{t('tools.modPackage.import.changesDetected')}</div>
            {mod.changedFields.map(field => (
              <Tag key={field} color="orange">
                {t(changedFieldLabels[field] || `tools.modPackage.import.field${field.charAt(0).toUpperCase() + field.slice(1)}`)}
              </Tag>
            ))}
            {mod.localName && (
              <div className="mod-transfer__detail-compare">
                <span className="mod-transfer__detail-compare-label">{t('tools.modPackage.import.localVersion')}</span>
                <span>{mod.localName}{mod.localAuthor ? ` — ${mod.localAuthor}` : ''}</span>
              </div>
            )}
          </div>
        </>
      )}

      {mod.previewPaths.length > 0 && (
        <>
          <CompactDivider extraCompact />
          <div className="mod-transfer__detail-previews">
            {mod.previewPaths.map((path, i) => (
              <img
                key={i}
                src={toAppUrl(path)}
                alt={`${mod.name} preview ${i + 1}`}
                className="mod-transfer__detail-preview-img"
              />
            ))}
          </div>
        </>
      )}
    </div>
  );
};
