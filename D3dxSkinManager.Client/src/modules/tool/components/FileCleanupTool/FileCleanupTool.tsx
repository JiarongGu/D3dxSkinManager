import React, { useState, useCallback, useEffect } from 'react';
import { Tabs } from 'antd';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { Module, ToolsEventType } from '../../../../shared/services/eventBus';
import { useEventSubscription } from '../../../../shared/hooks/useEventSubscription';
import { handleError } from '../../../../shared/utils/errorHandler';
import type { OrphanCategory, OrphanScanResult } from '../../../../shared/types/cleanup.types';
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

/** Tab definitions — category + i18n key suffixes (label/empty/description). */
const CLEANUP_TABS: { key: string; category: OrphanCategory; label: string; empty: string; description: string }[] = [
  { key: 'thumbnails', category: 'thumbnail', label: 'tabs.thumbnails', empty: 'noOrphanedThumbnails', description: 'thumbnailsDescription' },
  { key: 'previews', category: 'preview', label: 'tabs.previews', empty: 'noOrphanedPreviews', description: 'previewsDescription' },
  { key: 'temp', category: 'tempFile', label: 'tabs.tempFiles', empty: 'noTempFiles', description: 'tempFilesDescription' },
  { key: 'modcache', category: 'modCache', label: 'tabs.modFiles', empty: 'noOrphanedModFiles', description: 'modFilesDescription' },
  { key: 'orphanedArchive', category: 'orphanedArchive', label: 'tabs.orphanedArchives', empty: 'noOrphanedArchives', description: 'orphanedArchivesDescription' },
  { key: 'missingArchive', category: 'missingArchive', label: 'tabs.missingArchives', empty: 'noMissingArchives', description: 'missingArchivesDescription' },
  { key: 'remoteCache', category: 'remoteCache', label: 'tabs.remoteCache', empty: 'noRemoteCache', description: 'remoteCacheDescription' },
  { key: 'downloads', category: 'download', label: 'tabs.downloads', empty: 'noDownloads', description: 'downloadsDescription' },
];

const FileCleanupToolInner: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [scanResults, setScanResults] = useState<OrphanScanResult[]>([]);
  const [scanning, setScanning] = useState(false);

  // Fire-and-forget: the IPC acks immediately; results land via ORPHAN_SCAN_COMPLETE so the
  // UI never blocks on the walk (the old awaited scan froze the whole app on big libraries).
  const scanAll = useCallback(async () => {
    if (!selectedProfileId) return;

    try {
      setScanning(true);
      await api.tool.startOrphanScan(selectedProfileId);
    } catch (error: unknown) {
      setScanning(false);
      handleError(error);
    }
  }, [selectedProfileId]);

  useEventSubscription(Module.TOOL, ToolsEventType.ORPHAN_SCAN_COMPLETE, (payload) => {
    if (!payload) return;
    setScanning(false);
    if (payload.error) {
      handleError(new Error(payload.error));
      return;
    }
    setScanResults(payload.results ?? []);
  });

  useEffect(() => {
    void scanAll();
  }, [scanAll]);

  const getResultForCategory = (category: OrphanCategory): OrphanScanResult | undefined => {
    return scanResults.find(r => r.category === category);
  };

  const handleCleaned = useCallback(() => {
    void scanAll();
  }, [scanAll]);

  const items = CLEANUP_TABS.map(tab => ({
    key: tab.key,
    label: (
      <TabLabel
        text={t(`tools.fileCleanup.${tab.label}`)}
        count={getResultForCategory(tab.category)?.totalCount}
      />
    ),
    children: (
      <CleanupTab
        category={tab.category}
        scanResult={getResultForCategory(tab.category)}
        scanning={scanning}
        onCleaned={handleCleaned}
        onRescan={scanAll}
        emptyMessage={t(`tools.fileCleanup.${tab.empty}`)}
        description={t(`tools.fileCleanup.${tab.description}`)}
      />
    ),
  }));

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
