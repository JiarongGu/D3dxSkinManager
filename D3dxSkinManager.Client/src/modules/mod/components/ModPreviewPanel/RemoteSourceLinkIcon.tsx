import React from 'react';
import { GlobalOutlined } from '@ant-design/icons';
import { Tooltip } from 'antd';
import { useTranslation } from 'react-i18next';
import { ModInfo } from '../../../../shared/types/mod.types';
import { parseModRemoteRef } from '../../../../shared/utils/modRemoteRef';
import { useSlideInScreenContext } from '../../../../shared/context/SlideInScreenContext';
import { CompactIconButton } from '../../../../shared/components/compact';
// Cross-module React import (same-process, no IPC) — the sanctioned way to open another module's
// screen (see context-menu-extension.md "Cross-Module Tool Opening").
import { RemoteModDetailScreen } from '../../../remote/components/RemoteModDetailScreen';

/**
 * Compact source backlink for a mod imported from a remote library: a single icon (with tooltip) meant
 * to sit at the end of the mod-detail title, opening the entry's remote detail page. Renders nothing
 * for mods with no remote identity. Replaces the old labeled "来源 / View source page" row (saves space).
 */
export const RemoteSourceLinkIcon: React.FC<{ mod: ModInfo }> = ({ mod }) => {
  const { t } = useTranslation();
  const { openScreen } = useSlideInScreenContext();

  const remoteRef = parseModRemoteRef(mod.metadata);
  if (!remoteRef) return null;

  const openRemotePage = () =>
    openScreen({
      title: mod.name || t('remote.detailTitle'),
      content: (
        <RemoteModDetailScreen
          sourceId={remoteRef.sourceId}
          listId={remoteRef.listId}
          entryId={remoteRef.entryId}
          detailUrl={remoteRef.detailUrl}
          fallbackTitle={mod.name}
          imported
          localModIds={[mod.id]}
        />
      ),
      width: 'min(1180px, 92vw)',
      headless: true,
    });

  return (
    <Tooltip title={t('mod.remoteSourceView')}>
      <CompactIconButton
        className="mod-preview-title__source"
        icon={<GlobalOutlined />}
        onClick={openRemotePage}
        aria-label={t('mod.remoteSourceView')}
      />
    </Tooltip>
  );
};
