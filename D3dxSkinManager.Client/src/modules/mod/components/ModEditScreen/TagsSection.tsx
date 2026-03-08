import React, { useState } from 'react';
import { Form, Space } from 'antd';
import { TagsOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { MultiTagInput } from '../MultiTagInput';
import { TagManagementDialog } from '../TagManagementDialog';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';

export interface TagsSectionProps {
  tags: string[];
  availableTags: string[];
  onTagsChange: (tags: string[]) => void;
  onTagDeleted?: () => void; // Callback to refresh available tags
  tagColorsMap: Map<string, string>;
  setTagColorsMap: (map: Map<string, string>) => void;
}

/**
 * Tags section for mod editing
 * Uses inline MultiTagInput with autocomplete and full tag selector dialog
 */
export const TagsSection: React.FC<TagsSectionProps> = ({
  tags,
  availableTags,
  onTagsChange,
  onTagDeleted,
  tagColorsMap,
  setTagColorsMap,
}) => {
  const { t } = useTranslation();
  const [tagSelectorVisible, setTagSelectorVisible] = useState(false);

  const handleTagsChange = (newTags: string[]) => {
    onTagsChange(newTags);
  };

  const handleOpenTagSelector = () => {
    setTagSelectorVisible(true);
  };

  const handleTagSelectorConfirm = (selectedTags: string[]) => {
    onTagsChange(selectedTags);
    setTagSelectorVisible(false);
  };

  const handleTagSelectorCancel = () => {
    setTagSelectorVisible(false);
  };

  return (
    <>
      <Form.Item
        label={t("common.tags")}
        tooltip={t('mods.edit.tagsTooltip')}
      >
        <Space.Compact style={{ width: '100%' }}>
          <MultiTagInput
            value={tags}
            onChange={handleTagsChange}
            availableTags={availableTags}
            placeholder={t('mods.edit.tagsPlaceholder')}
            tagColorsMap={tagColorsMap}
            setTagColorsMap={setTagColorsMap}
          />
          <CompactButton
            icon={<TagsOutlined />}
            onClick={handleOpenTagSelector}
            title={t('mods.edit.openTagSelector')}
          />
        </Space.Compact>
      </Form.Item>

      <TagManagementDialog
        visible={tagSelectorVisible}
        selectedTags={tags}
        onConfirm={handleTagSelectorConfirm}
        onCancel={handleTagSelectorCancel}
        onTagDeleted={onTagDeleted}
        title={t('mods.edit.manageTags')}
        tagColorsMap={tagColorsMap}
        setTagColorsMap={setTagColorsMap}
      />
    </>
  );
};
