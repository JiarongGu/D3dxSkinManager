import React, { useState, useCallback, useMemo } from 'react';
import { Button, Checkbox, Empty, Result, Spin, Tag, Tooltip } from 'antd';
import {
  DeleteOutlined,
  ReloadOutlined,
  FolderOutlined,
  FolderOpenOutlined,
  FileOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../../../shared/context/ProfileContext';
import { api } from '../../../../../shared/services/ipc';
import { systemService } from '../../../../../shared/services/ipc';
import { handleError } from '../../../../../shared/utils/errorHandler';
import { formatBytes } from '../../../../../shared/utils/formatBytes';
import type { OrphanCategory, OrphanScanResult, OrphanedItem } from '../../../../../shared/types/cleanup.types';

interface CleanupTabProps {
  category: OrphanCategory;
  scanResult?: OrphanScanResult;
  scanning: boolean;
  onCleaned: () => void;
  emptyMessage: string;
  description: string;
}

export const CleanupTab: React.FC<CleanupTabProps> = ({
  category,
  scanResult,
  scanning,
  onCleaned,
  emptyMessage,
  description,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [selectedPaths, setSelectedPaths] = useState<Set<string>>(new Set());
  const [cleaning, setCleaning] = useState(false);
  const [lastResult, setLastResult] = useState<{ deleted: number; freed: number; failed: number } | undefined>();

  const items = scanResult?.items ?? [];

  const totalSize = useMemo(
    () => items.reduce((sum, item) => sum + item.sizeBytes, 0),
    [items]
  );

  const selectedSize = useMemo(
    () => items.filter(i => selectedPaths.has(i.path)).reduce((sum, i) => sum + i.sizeBytes, 0),
    [items, selectedPaths]
  );

  const allSelected = items.length > 0 && selectedPaths.size === items.length;

  const toggleItem = useCallback((path: string) => {
    setSelectedPaths(prev => {
      const next = new Set(prev);
      if (next.has(path)) next.delete(path);
      else next.add(path);
      return next;
    });
  }, []);

  const toggleAll = useCallback(() => {
    if (allSelected) {
      setSelectedPaths(new Set());
    } else {
      setSelectedPaths(new Set(items.map(i => i.path)));
    }
  }, [allSelected, items]);

  const handleClean = useCallback(async () => {
    if (!selectedProfileId || selectedPaths.size === 0) return;

    try {
      setCleaning(true);
      setLastResult(undefined);
      const result = await api.tool.cleanOrphans(
        selectedProfileId,
        category,
        Array.from(selectedPaths)
      );
      setLastResult({
        deleted: result.deletedCount,
        freed: result.freedBytes,
        failed: result.failedCount,
      });
      setSelectedPaths(new Set());
      onCleaned();
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setCleaning(false);
    }
  }, [selectedProfileId, selectedPaths, category, onCleaned]);

  if (scanning && !scanResult) {
    return (
      <div className="file-cleanup__tab-loading">
        <Spin />
      </div>
    );
  }

  return (
    <div className="file-cleanup__tab-content">
      {/* Description bar */}
      <div className="file-cleanup__description-bar">
        <InfoCircleOutlined className="file-cleanup__description-icon" />
        <span>{description}</span>
      </div>

      {/* Toolbar */}
      <div className="file-cleanup__toolbar">
        <div className="file-cleanup__toolbar-left">
          <Checkbox
            checked={allSelected}
            indeterminate={selectedPaths.size > 0 && !allSelected}
            onChange={toggleAll}
            disabled={items.length === 0}
          >
            {t('tools.fileCleanup.selectAll')}
          </Checkbox>
          <span className="file-cleanup__summary">
            {items.length} {t('tools.fileCleanup.items')} · {formatBytes(totalSize)}
          </span>
        </div>
        <div className="file-cleanup__toolbar-right">
          {selectedPaths.size > 0 && (
            <span className="file-cleanup__selected-info">
              {selectedPaths.size} {t('tools.fileCleanup.selected')} · {formatBytes(selectedSize)}
            </span>
          )}
          <Button
            type="primary"
            danger
            icon={<DeleteOutlined />}
            onClick={handleClean}
            loading={cleaning}
            disabled={selectedPaths.size === 0}
            size="small"
          >
            {t('tools.fileCleanup.cleanSelected')}
          </Button>
        </div>
      </div>

      {/* Result notification */}
      {lastResult && (
        <div className="file-cleanup__result-bar">
          <Result
            status={lastResult.failed === 0 ? 'success' : 'warning'}
            title={
              lastResult.failed === 0
                ? t('tools.fileCleanup.cleanupSuccess', { count: lastResult.deleted, size: formatBytes(lastResult.freed) })
                : t('tools.fileCleanup.cleanupPartial', { deleted: lastResult.deleted, failed: lastResult.failed })
            }
            className="file-cleanup__result-inline"
          />
        </div>
      )}

      {/* Items list */}
      {items.length === 0 ? (
        <div className="file-cleanup__empty">
          <Empty description={emptyMessage} />
        </div>
      ) : (
        <div className="file-cleanup__list">
          {items.map((item) => (
            <CleanupItem
              key={item.path}
              item={item}
              category={category}
              selected={selectedPaths.has(item.path)}
              onToggle={() => toggleItem(item.path)}
            />
          ))}
        </div>
      )}
    </div>
  );
};

const CleanupItem: React.FC<{
  item: OrphanedItem;
  category: OrphanCategory;
  selected: boolean;
  onToggle: () => void;
}> = ({ item, category, selected, onToggle }) => {
  const { t } = useTranslation();
  // The scanner reports what it saw on disk — never guess from the name (mod archives are
  // extensionless FILES and the old name-based heuristic misclassified them as directories,
  // breaking open-in-explorer for the archive category).
  const isDirectory = item.isDirectory;
  // MissingArchive items store mod ID in path — no file to open
  const canOpenInExplorer = category !== 'missingArchive';

  const handleOpenInExplorer = async (e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      if (isDirectory) {
        await systemService.openDirectory(item.path);
      } else {
        // Select-in-explorer works for extensionless archive files too (explorer /select).
        await systemService.openFileInExplorer(item.path);
      }
    } catch {
      // Silently ignore — file/folder may have been deleted
    }
  };

  return (
    <div className="file-cleanup__item" onClick={onToggle}>
      <Checkbox checked={selected} onClick={(e) => e.stopPropagation()} onChange={onToggle} />
      <span className="file-cleanup__item-icon">
        {isDirectory ? <FolderOutlined /> : <FileOutlined />}
      </span>
      <Tooltip title={item.path} placement="topLeft">
        <span className="file-cleanup__item-name">{item.name}</span>
      </Tooltip>
      {item.sizeBytes > 0 && (
        <span className="file-cleanup__item-size">
          <Tag>{formatBytes(item.sizeBytes)}</Tag>
        </span>
      )}
      <span className="file-cleanup__item-date">{item.lastModified}</span>
      {canOpenInExplorer && (
        <Tooltip title={t('tools.fileCleanup.openInExplorer')}>
          <Button
            type="text"
            size="small"
            icon={<FolderOpenOutlined />}
            onClick={handleOpenInExplorer}
            className="file-cleanup__item-open"
          />
        </Tooltip>
      )}
    </div>
  );
};

