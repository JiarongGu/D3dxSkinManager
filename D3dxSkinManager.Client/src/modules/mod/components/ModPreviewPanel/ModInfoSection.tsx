/**
 * ModInfoSection Component
 * Displays mod metadata: author, tags, category, description
 */

import React from 'react';
import { Tag, Space } from 'antd';
import { UserOutlined, TagsOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactText, CompactParagraph } from '../../../../shared/components/compact';
import { ModInfo } from '../../../../shared/types/mod.types';

interface ModInfoSectionProps {
  mod: ModInfo;
}

export const ModInfoSection: React.FC<ModInfoSectionProps> = ({ mod }) => {
  const { t } = useTranslation();

  const showAuthor = mod.author && mod.author.trim() !== '';
  const showTags = mod.tags && mod.tags.length > 0;
  // The remote source backlink now lives as an icon at the end of the mod-detail title
  // (RemoteSourceLinkIcon) — saves space vs the old labeled row here.

  return (
    <div className="mod-preview-info">
      {(showAuthor || showTags) && (
        <div className="mod-preview-info-item">
          {/* Author */}
          {showAuthor && (
            <>
              <CompactText type="secondary" className="mod-preview-info-label">
                <UserOutlined className="mod-preview-info-icon" />
                {t("common.author")}
              </CompactText>
              <CompactText className="mod-preview-info-value">{mod.author}</CompactText>
            </>
          )}

          {/* Tags */}
          {showTags && (
            <>
              <CompactText type="secondary" className="mod-preview-info-label">
                <TagsOutlined className="mod-preview-info-icon" />
                {t("common.tags")}
              </CompactText>
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
          <CompactText type="secondary" className="mod-preview-info-label">
            {t("common.description")}
          </CompactText>
          <CompactParagraph className="mod-preview-description" style={{ marginBottom: 0 }}>
            {mod.description}
          </CompactParagraph>
        </div>
      )}

    </div>
  );
};
