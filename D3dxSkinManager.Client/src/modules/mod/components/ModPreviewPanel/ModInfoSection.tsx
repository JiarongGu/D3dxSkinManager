/**
 * ModInfoSection Component
 * Displays mod metadata: author, tags, category, description
 */

import React from 'react';
import { Typography, Tag, Space } from 'antd';
import { UserOutlined, TagsOutlined, GlobalOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { ModInfo } from '../../../../shared/types/mod.types';
import { parseModRemoteRef } from '../../../../shared/utils/modRemoteRef';
import { useSlideInScreenContext } from '../../../../shared/context/SlideInScreenContext';
import { CompactButton } from '../../../../shared/components/compact';
// Cross-module React import (same-process, no IPC) — the sanctioned way to open another
// module's screen (see context-menu-extension.md "Cross-Module Tool Opening").
import { RemoteModDetailScreen } from '../../../remote/components/RemoteModDetailScreen';

const { Text, Paragraph } = Typography;

interface ModInfoSectionProps {
  mod: ModInfo;
}

export const ModInfoSection: React.FC<ModInfoSectionProps> = ({ mod }) => {
  const { t } = useTranslation();
  const { openScreen } = useSlideInScreenContext();

  const showAuthor = mod.author && mod.author.trim() !== '';
  const showTags = mod.tags && mod.tags.length > 0;

  // Mods imported from a remote library carry their source identity in metadata — offer the
  // jump back to the entry's remote detail page.
  const remoteRef = parseModRemoteRef(mod.metadata);
  const openRemotePage = () => {
    if (!remoteRef) return;
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
      headless: true, // the info card carries the title (same presentation as the remote browse view)
    });
  };

  return (
    <div className="mod-preview-info">
      {(showAuthor || showTags) && (
        <div className="mod-preview-info-item">
          {/* Author */}
          {showAuthor && (
            <>
              <Text type="secondary" className="mod-preview-info-label">
                <UserOutlined className="mod-preview-info-icon" />
                {t("common.author")}
              </Text>
              <Text className="mod-preview-info-value">{mod.author}</Text>
            </>
          )}

          {/* Tags */}
          {showTags && (
            <>
              <Text type="secondary" className="mod-preview-info-label">
                <TagsOutlined className="mod-preview-info-icon" />
                {t("common.tags")}
              </Text>
              <Space size={[4, 4]} wrap className="mod-preview-info-value">
                {mod.tags.map((tag) => (
                  <Tag key={tag}>
                    {tag}
                  </Tag>
                ))}
              </Space>
            </>
          )}
        </div>
      )}

      {/* Description */}
      {mod.description && mod.description.trim() !== '' && (
        <div className="mod-preview-info-item">
          <Text type="secondary" className="mod-preview-info-label">
            {t("common.description")}
          </Text>
          <Paragraph className="mod-preview-description">
            {mod.description}
          </Paragraph>
        </div>
      )}

      {/* Remote source backlink (mods imported from a remote library) */}
      {remoteRef && (
        <div className="mod-preview-info-item">
          <Text type="secondary" className="mod-preview-info-label">
            <GlobalOutlined className="mod-preview-info-icon" />
            {t('mod.remoteSource')}
          </Text>
          <CompactButton size="small" onClick={openRemotePage}>
            {t('mod.remoteSourceView')}
          </CompactButton>
        </div>
      )}
    </div>
  );
};
