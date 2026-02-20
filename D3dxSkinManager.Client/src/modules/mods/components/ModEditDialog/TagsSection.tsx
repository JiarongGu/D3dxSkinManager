import React, { useState } from 'react';
import { Form } from 'antd';
import { useTranslation } from 'react-i18next';
import { MultiTagInput } from '../../../../shared/components/common/MultiTagInput';
import { ModTagSelectorDialog } from './ModTagSelectorDialog';

export interface TagsSectionProps {
  tags: string[];
  availableTags: string[];
  onTagsChange: (tags: string[]) => void;
}

/**
 * Tags section for mod editing
 * Includes multi-tag input with autocomplete and full tag selector dialog
 */
export const TagsSection: React.FC<TagsSectionProps> = ({
  tags,
  availableTags,
  onTagsChange,
}) => {
  const { t } = useTranslation();
  const [tagSelectorVisible, setTagSelectorVisible] = useState(false);

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
        <MultiTagInput
          value={tags}
          onChange={onTagsChange}
          availableTags={availableTags}
          onOpenTagSelector={handleOpenTagSelector}
          placeholder={t('mods.edit.tagsPlaceholder')}
        />
      </Form.Item>

      <ModTagSelectorDialog
        visible={tagSelectorVisible}
        availableTags={availableTags}
        selectedTags={tags}
        onConfirm={handleTagSelectorConfirm}
        onCancel={handleTagSelectorCancel}
      />
    </>
  );
};
