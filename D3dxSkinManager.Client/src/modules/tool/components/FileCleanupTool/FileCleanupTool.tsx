import React, { useState, useCallback, useEffect } from 'react';
import { Tabs, Spin } from 'antd';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { useTaskStore } from '../../../../shared/store/taskStore';
import type { OrphanScanResult } from '../../../../shared/types/cleanup.types';
import { CleanupTab } from './components/CleanupTab';
import './FileCleanupTool.css';

interface FileCleanupToolProps {
  visible: boolean;
  onClose: () => void;
}

export const FileCleanupTool: React.FC<FileCleanupToolProps> = ({ visible, onClose }) => {
  const { t } = useTranslation();

  const content = <FileCleanupToolInner />;

  useSlideInScreen({
    visible,
    title: t('tools.fileCleanup.title'),
    content,
    width: '85%',
    onClose,
  });

  return null;
};

const FileCleanupToolInner: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [scanResults, setScanResults] = useState<OrphanScanResult[]>([]);
  const [scanning, setScanning] = useState(false);

  const scanAll = useCallback(async () => {
    if (!selectedProfileId) return;

    const taskId = 'file-cleanup-scan';
    try {
      setScanning(true);
      useTaskStore.getState().addTask({ id: taskId, label: t('statusBar.tasks.scanningFiles') });
      const results = await api.tool.scanAllOrphans(selectedProfileId);
      setScanResults(results);
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setScanning(false);
      useTaskStore.getState().removeTask(taskId);
    }
  }, [selectedProfileId, t]);

  useEffect(() => {
    void scanAll();
  }, [scanAll]);

  const getResultForCategory = (category: string): OrphanScanResult | undefined => {
    return scanResults.find(r => r.category === category);
  };

  const handleCleaned = useCallback(() => {
    void scanAll();
  }, [scanAll]);

  const items = [
    {
      key: 'thumbnails',
      label: (
        <TabLabel
          text={t('tools.fileCleanup.tabs.thumbnails')}
          count={getResultForCategory('thumbnail')?.totalCount}
        />
      ),
      children: (
        <CleanupTab
          category="thumbnail"
          scanResult={getResultForCategory('thumbnail')}
          scanning={scanning}
          onCleaned={handleCleaned}
          emptyMessage={t('tools.fileCleanup.noOrphanedThumbnails')}
          description={t('tools.fileCleanup.thumbnailsDescription')}
        />
      ),
    },
    {
      key: 'previews',
      label: (
        <TabLabel
          text={t('tools.fileCleanup.tabs.previews')}
          count={getResultForCategory('preview')?.totalCount}
        />
      ),
      children: (
        <CleanupTab
          category="preview"
          scanResult={getResultForCategory('preview')}
          scanning={scanning}
          onCleaned={handleCleaned}
          emptyMessage={t('tools.fileCleanup.noOrphanedPreviews')}
          description={t('tools.fileCleanup.previewsDescription')}
        />
      ),
    },
    {
      key: 'temp',
      label: (
        <TabLabel
          text={t('tools.fileCleanup.tabs.tempFiles')}
          count={getResultForCategory('tempFile')?.totalCount}
        />
      ),
      children: (
        <CleanupTab
          category="tempFile"
          scanResult={getResultForCategory('tempFile')}
          scanning={scanning}
          onCleaned={handleCleaned}
          emptyMessage={t('tools.fileCleanup.noTempFiles')}
          description={t('tools.fileCleanup.tempFilesDescription')}
        />
      ),
    },
    {
      key: 'modcache',
      label: (
        <TabLabel
          text={t('tools.fileCleanup.tabs.modFiles')}
          count={getResultForCategory('modCache')?.totalCount}
        />
      ),
      children: (
        <CleanupTab
          category="modCache"
          scanResult={getResultForCategory('modCache')}
          scanning={scanning}
          onCleaned={handleCleaned}
          emptyMessage={t('tools.fileCleanup.noOrphanedModFiles')}
          description={t('tools.fileCleanup.modFilesDescription')}
        />
      ),
    },
  ];

  if (scanning && scanResults.length === 0) {
    return (
      <div className="file-cleanup__loading">
        <Spin />
      </div>
    );
  }

  return (
    <div className="file-cleanup">
      <Tabs
        items={items}
        tabPlacement="start"
        className="file-cleanup__tabs"
      />
    </div>
  );
};

const TabLabel: React.FC<{ text: string; count?: number }> = ({ text, count }) => (
  <span className="file-cleanup__tab-label">
    <span>{text}</span>
    {count !== undefined && count > 0 && (
      <span className="file-cleanup__tab-count">{count}</span>
    )}
  </span>
);
