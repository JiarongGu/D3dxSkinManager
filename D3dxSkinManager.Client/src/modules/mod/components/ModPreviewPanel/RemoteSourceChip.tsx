import React from 'react';
import { GlobalOutlined } from '@ant-design/icons';
import { Tag, Tooltip } from 'antd';
import { useTranslation } from 'react-i18next';
import { ModInfo } from '../../../../shared/types/mod.types';
import { parseModRemoteRef } from '../../../../shared/utils/modRemoteRef';
import { useSlideInScreenContext } from '../../../../shared/context/SlideInScreenContext';
// Cross-module React import (same-process, no IPC) — the sanctioned way to open another module's
// screen (see context-menu-extension.md "Cross-Module Tool Opening").
import { RemoteModDetailScreen } from '../../../remote/components/RemoteModDetailScreen';

/**
 * Origin CHIP for a mod imported from a remote library — shows the library/source name in the mod
 * DETAIL panel (moved off the list rows, which stay uncluttered). Clickable when the mod still carries
 * its remote identity (opens the entry's remote detail page). Renders nothing for non-remote mods.
 */
export const RemoteSourceChip: React.FC<{ mod: ModInfo }> = ({ mod }) => {
  const { t } = useTranslation();
  const { openScreen } = useSlideInScreenContext();

  const remoteRef = parseModRemoteRef(mod.metadata);
  const label = mod.libraryName || (remoteRef ? t('remote.remoteLibrary') : undefined);
  if (!label) return null; // not a remote-sourced mod

  const openRemotePage = !remoteRef
    ? undefined
    : () =>
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

  const chip = (
    <Tag
      color="cyan"
      icon={<GlobalOutlined />}
      className={`mod-preview-source-chip${openRemotePage ? ' mod-preview-source-chip--clickable' : ''}`}
      onClick={openRemotePage}
    >
      {label}
    </Tag>
  );

  return openRemotePage ? <Tooltip title={t('mod.remoteSourceView')}>{chip}</Tooltip> : chip;
};
