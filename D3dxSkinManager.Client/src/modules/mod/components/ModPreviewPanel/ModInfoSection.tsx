/**
 * ModInfoSection Component
 * Displays mod metadata: author, tags, category, description
 */

import React from 'react';
import { Typography, Tag, Space } from 'antd';
import { UserOutlined, TagsOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { ModInfo } from '../../../../shared/types/mod.types';

const { Text, Paragraph } = Typography;

interface ModInfoSectionProps {
  mod: ModInfo;
}

export const ModInfoSection: React.FC<ModInfoSectionProps> = ({ mod }) => {
  const { t } = useTranslation();

  const showAuthor = mod.author && mod.author.trim() !== '';
  const showTags = mod.tags && mod.tags.length > 0;

  return (
    <div className="mod-preview-info">
      {(showAuthor || showTags) && (
        <div className="mod-preview-info-item">
          {/* Author */}
          {showAuthor && (
            <>
              <Text type="secondary" className="mod-preview-info-label">
                <UserOutlined className="mod-preview-info-icon" />
                {t('mods.details.author')}
              </Text>
              <Text className="mod-preview-info-value">{mod.author}</Text>
            </>
          )}

          {/* Tags */}
          {showTags && (
            <>
              <Text type="secondary" className="mod-preview-info-label">
                <TagsOutlined className="mod-preview-info-icon" />
                {t('mods.details.tags')}
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
            {t('mods.details.description')}
          </Text>
          <Paragraph className="mod-preview-description">
            {mod.description}
          </Paragraph>
        </div>
      )}
    </div>
  );
};
