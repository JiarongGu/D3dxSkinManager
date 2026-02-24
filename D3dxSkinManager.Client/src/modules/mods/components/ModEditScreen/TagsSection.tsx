import React, { useState } from 'react';
import { Form, Space } from 'antd';
import { TagsOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { MultiTagInput } from '../MultiTagInput';
import { TagSelectorDialog } from '../TagSelectorDialog';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';

export interface TagsSectionProps {
  tags: string[];
  availableTags: string[];
  onTagsChange: (tags: string[]) => void;
}

/**
 * Tags section for mod editing
 * Uses inline MultiTagInput with autocomplete and full tag selector dialog
 */
export const TagsSection: React.FC<TagsSectionProps> = ({
  tags,
  availableTags,
  onTagsChange,
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
        label={t('mods.edit.tags')}
        tooltip={t('mods.edit.tagsTooltip')}
      >
        <Space.Compact style={{ width: '100%' }}>
          <MultiTagInput
            value={tags}
            onChange={handleTagsChange}
            availableTags={availableTags}
            placeholder={t('mods.edit.tagsPlaceholder')}
          />
          <CompactButton
            icon={<TagsOutlined />}
            onClick={handleOpenTagSelector}
            title="Open tag selector"
          />
        </Space.Compact>
      </Form.Item>

      <TagSelectorDialog
        visible={tagSelectorVisible}
        availableTags={availableTags}
        selectedTags={tags}
        onConfirm={handleTagSelectorConfirm}
        onCancel={handleTagSelectorCancel}
      />
    </>
  );
};
