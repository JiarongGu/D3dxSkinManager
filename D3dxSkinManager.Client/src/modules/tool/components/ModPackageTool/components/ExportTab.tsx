import React, { useCallback, useMemo, useState } from 'react';
import { Progress, Row, Col, List, Spin } from 'antd';
import {
  RightOutlined,
  CloseOutlined,
  ClearOutlined,
  InboxOutlined,
  FolderOpenOutlined,
  SearchOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton, CompactInput, CompactCard, CompactAlert, CompactSpace, CompactDivider, CompactSelect, CompactCheckbox } from '../../../../../shared/components/compact';
import { toPercent } from '../../../../../shared/utils/toPercent';
import { api } from '../../../../../shared/services/ipc';
import { useModPackage } from '../context/ModPackageContext';
import type { ModInfo } from '../../../../../shared/types/mod.types';
import type { CategoryInfo } from '../../../../../shared/types/category.types';

import { formatBytes } from '../../../../../shared/utils/formatBytes';
import { flattenCategoryOptions } from '../../../../../shared/utils/categoryTree';

function buildCategoryNameMap(cats: CategoryInfo[]): Map<string, string> {
  const map = new Map<string, string>();
  const walk = (nodes: CategoryInfo[]) => {
    for (const n of nodes) {
      map.set(n.id, n.name);
      walk(n.children);
    }
  };
  walk(cats);
  return map;
}

export const ExportTab: React.FC = () => {
  const { t } = useTranslation();
  const {
    mods, categories,
    selectedModIds, setSelectedModIds,
    exportOpts, setExportOpts,
    exportResult, exportStatus,
    progress, loading,
    startExport, resetExport,
  } = useModPackage();

  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<string>();

  const categoryOptions = useMemo(() => {
    return [{ value: '__all__', label: t('tools.modPackage.export.allCategories') }, ...flattenCategoryOptions(categories)];
  }, [categories, t]);

  const categoryNameMap = useMemo(() => buildCategoryNameMap(categories), [categories]);

  const availableMods = useMemo(() => {
    const lowerSearch = search.toLowerCase();
    return mods.filter(mod => {
      if (selectedModIds.has(mod.id)) return false;
      if (categoryFilter && categoryFilter !== '__all__' && mod.category !== categoryFilter) return false;
      if (lowerSearch) {
        return mod.name.toLowerCase().includes(lowerSearch)
          || mod.author?.toLowerCase().includes(lowerSearch)
          || mod.tags?.some(tag => tag.toLowerCase().includes(lowerSearch));
      }
      return true;
    });
  }, [mods, selectedModIds, search, categoryFilter]);

  const selectedMods = useMemo(() => {
    return mods.filter(mod => selectedModIds.has(mod.id));
  }, [mods, selectedModIds]);

  const handleAdd = useCallback((id: string) => {
    setSelectedModIds(prev => new Set([...prev, id]));
  }, [setSelectedModIds]);

  const handleAddAll = useCallback(() => {
    setSelectedModIds(prev => {
      const next = new Set(prev);
      availableMods.forEach(m => next.add(m.id));
      return next;
    });
  }, [availableMods, setSelectedModIds]);

  const handleRemove = useCallback((id: string) => {
    setSelectedModIds(prev => {
      const next = new Set(prev);
      next.delete(id);
      return next;
    });
  }, [setSelectedModIds]);

  const handleClearAll = useCallback(() => {
    setSelectedModIds(new Set());
  }, [setSelectedModIds]);

  const getCategoryLabel = (mod: ModInfo) => categoryNameMap.get(mod.category) || '';

  const handleOpenFolder = useCallback(() => {
    if (exportResult?.outputPath) {
      void api.system.openDirectory(exportResult.outputPath);
    }
  }, [exportResult]);

  // Running state
  if (exportStatus === 'running') {
    const percent = progress ? toPercent(progress.current, progress.total) : 0;
    return (
      <div className="mod-transfer__status">
        <CompactSpace vertical className="mod-transfer__status-inner">
          <CompactAlert
            title={t('tools.modPackage.export.exporting')}
            type="info"
            showIcon
            extraCompact
          />
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
  if (exportStatus === 'done' && exportResult) {
    return (
      <div className="mod-transfer__status">
        <CompactSpace vertical className="mod-transfer__status-inner">
          <CompactAlert
            title={exportResult.success ? t('tools.modPackage.export.success') : t('tools.modPackage.export.failed')}
            description={exportResult.success
              ? t('tools.modPackage.export.successDetail', { count: exportResult.exportedCount, size: formatBytes(exportResult.totalSizeBytes) })
              : undefined}
            type={exportResult.success ? 'success' : 'error'}
            showIcon
            extraCompact
          />

          <CompactCard title={t('tools.modPackage.export.resultSummary')} extraCompact className="mod-transfer__result-card">
            <Row gutter={8}>
              <Col span={8}>
                <div className="mod-transfer__stat">
                  <div className="mod-transfer__stat-label">{t('tools.modPackage.export.modsExported')}</div>
                  <div className="mod-transfer__stat-value">{exportResult.exportedCount}</div>
                </div>
              </Col>
              <Col span={8}>
                <div className="mod-transfer__stat">
                  <div className="mod-transfer__stat-label">{t('tools.modPackage.export.totalSize')}</div>
                  <div className="mod-transfer__stat-value">{formatBytes(exportResult.totalSizeBytes)}</div>
                </div>
              </Col>
              <Col span={8}>
                <div className="mod-transfer__stat">
                  <div className="mod-transfer__stat-label">{t('tools.modPackage.errors')}</div>
                  <div className="mod-transfer__stat-value">{exportResult.errors.length}</div>
                </div>
              </Col>
            </Row>

            {exportResult.outputPath && (
              <>
                <CompactDivider extraCompact />
                <div className="mod-transfer__result-path">
                  <span className="mod-transfer__result-label">{t('tools.modPackage.export.outputPath')}</span>
                  <code>{exportResult.outputPath}</code>
                </div>
              </>
            )}

            {exportResult.errors.length > 0 && (
              <>
                <CompactDivider extraCompact />
                <CompactAlert
                  title={t('tools.modPackage.errors') + ` (${exportResult.errors.length})`}
                  description={
                    <List
                      size="small"
                      dataSource={exportResult.errors.slice(0, 5)}
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

          <CompactSpace>
            {exportResult.outputPath && (
              <CompactButton icon={<FolderOpenOutlined />} onClick={handleOpenFolder}>
                {t('tools.modPackage.export.openFolder')}
              </CompactButton>
            )}
            <CompactButton onClick={resetExport}>{t('tools.modPackage.export.exportMore')}</CompactButton>
          </CompactSpace>
        </CompactSpace>
      </div>
    );
  }

  return (
    <div className="mod-transfer__export">
      {/* Config bar — single flex row, no Form */}
      <div className="mod-transfer__config-bar">
        <span className="mod-transfer__config-label">{t('tools.modPackage.export.folderName')}</span>
        <CompactInput
          size="medium"
          placeholder={t('tools.modPackage.export.packageNamePlaceholder')}
          style={{ width: 180 }}
          value={exportOpts.packageName}
          onChange={e => setExportOpts(prev => ({ ...prev, packageName: e.target.value }))}
        />
        <span className="mod-transfer__config-label">{t('tools.modPackage.export.descriptionLabel')}</span>
        <CompactInput
          size="medium"
          placeholder={t('tools.modPackage.export.packageDescriptionPlaceholder')}
          style={{ width: 200 }}
          value={exportOpts.packageDescription}
          onChange={e => setExportOpts(prev => ({ ...prev, packageDescription: e.target.value }))}
        />
        <span className="mod-transfer__config-divider" />
        <CompactCheckbox
          checked={exportOpts.includePreviews}
          onChange={e => setExportOpts(prev => ({ ...prev, includePreviews: e.target.checked }))}
        >{t('tools.modPackage.export.includePreviews')}</CompactCheckbox>
      </div>

      {/* Two-panel transfer */}
      <div className="mod-transfer__panels">
        {/* Left: Available */}
        <div className="mod-transfer__panel">
          <div className="mod-transfer__panel-header">
            <span className="mod-transfer__panel-title">
              {t('tools.modPackage.export.available')} ({availableMods.length})
            </span>
            <CompactButton size="small" onClick={handleAddAll} disabled={availableMods.length === 0}>
              {t('tools.modPackage.selectAll')}
            </CompactButton>
          </div>
          <div className="mod-transfer__panel-filters">
            <CompactInput
              placeholder={t('tools.modPackage.searchMods')}
              value={search}
              onChange={e => setSearch(e.target.value)}
              allowClear
              prefix={<SearchOutlined />}
              className="mod-transfer__search"
            />
            <CompactSelect
              value={categoryFilter || '__all__'}
              onChange={v => setCategoryFilter(v === '__all__' ? undefined : v)}
              options={categoryOptions}
              className="mod-transfer__category-filter"
              popupMatchSelectWidth={false}
            />
          </div>
          <div className="mod-transfer__panel-list">
            {loading ? (
              <div className="mod-transfer__loading"><Spin /></div>
            ) : (
              availableMods.map(mod => (
                <div key={mod.id} className="mod-transfer__item" onDoubleClick={() => handleAdd(mod.id)}>
                  <div className="mod-transfer__item-info">
                    <span className="mod-transfer__item-name">{mod.name}</span>
                    <span className="mod-transfer__item-meta">
                      {mod.author && <span>{mod.author}</span>}
                      {mod.author && getCategoryLabel(mod) && <span className="mod-transfer__meta-sep">/</span>}
                      {getCategoryLabel(mod) && <span>{getCategoryLabel(mod)}</span>}
                    </span>
                  </div>
                  <span className="mod-transfer__item-action">
                    <RightOutlined />
                  </span>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Right: Selected */}
        <div className="mod-transfer__panel">
          <div className="mod-transfer__panel-header">
            <span className="mod-transfer__panel-title">
              {t('tools.modPackage.export.selected')} ({selectedMods.length})
            </span>
            <CompactButton size="small" onClick={handleClearAll} disabled={selectedMods.length === 0}>
              {t('tools.modPackage.deselectAll')}
            </CompactButton>
          </div>
          <div className="mod-transfer__panel-list">
            {selectedMods.length > 0 ? (
              selectedMods.map(mod => (
                <div key={mod.id} className="mod-transfer__item" onDoubleClick={() => handleRemove(mod.id)}>
                  <div className="mod-transfer__item-info">
                    <span className="mod-transfer__item-name">{mod.name}</span>
                    <span className="mod-transfer__item-meta">
                      {mod.author && <span>{mod.author}</span>}
                      {mod.author && getCategoryLabel(mod) && <span className="mod-transfer__meta-sep">/</span>}
                      {getCategoryLabel(mod) && <span>{getCategoryLabel(mod)}</span>}
                    </span>
                  </div>
                  <span className="mod-transfer__item-action mod-transfer__item-action--remove">
                    <CloseOutlined />
                  </span>
                </div>
              ))
            ) : (
              <div className="mod-transfer__empty-drop">
                <InboxOutlined className="mod-transfer__empty-icon" />
                <span>{t('tools.modPackage.export.noModsSelected')}</span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Bottom action bar */}
      <div className="mod-transfer__action-bar">
        <CompactButton
          type="primary"
          onClick={() => void startExport()}
          disabled={selectedModIds.size === 0 || !exportOpts.packageName}
        >
          {t('tools.modPackage.export.startExport')} ({selectedModIds.size})
        </CompactButton>
      </div>
    </div>
  );
};
