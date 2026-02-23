import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Checkbox, Input, Button, Space, Row, Col, Divider, Tag } from 'antd';
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons';
import { useSlideInDialog } from '../../../../shared/hooks/useSlideInDialog';
import { useTranslation } from 'react-i18next';
import './TagSelectDialog.css';

interface TagSelectDialogProps {
  visible: boolean;
  selectedTags: string[];
  availableTags?: string[];
  onSave: (tags: string[]) => void;
  onCancel: () => void;
}

/**
 * Dialog for selecting multiple tags with checkboxes
 * Supports adding custom tags
 */
export const TagSelectDialog: React.FC<TagSelectDialogProps> = ({
  visible,
  selectedTags,
  availableTags = [],
  onSave,
  onCancel,
}) => {
  const { t } = useTranslation();
  const [tempSelectedTags, setTempSelectedTags] = useState<string[]>([]);
  const [customTag, setCustomTag] = useState<string>('');
  const [allTags, setAllTags] = useState<string[]>([]);

  // Common predefined tags
  const commonTags = [
    'Character',
    'Weapon',
    'UI',
    'Effect',
    'Texture',
    'Sound',
    'Animation',
    'NSFW',
    'HD',
    '4K',
    'Recolor',
    'Redesign',
    'Work in Progress',
  ];

  // Initialize on open
  useEffect(() => {
    if (visible) {
      setTempSelectedTags([...selectedTags]);

      // Merge available tags with common tags, remove duplicates
      const uniqueSet = new Set([...commonTags, ...availableTags]);
      const merged = Array.from(uniqueSet);
      setAllTags(merged.sort());
    }
  }, [visible, selectedTags, availableTags]);

  const handleToggleTag = (tag: string) => {
    if (tempSelectedTags.includes(tag)) {
      setTempSelectedTags(tempSelectedTags.filter(t => t !== tag));
    } else {
      setTempSelectedTags([...tempSelectedTags, tag]);
    }
  };

  const handleAddCustomTag = () => {
    const trimmed = customTag.trim();

    if (!trimmed) {
      notification.warning(t('tagDialog.warnings.enterTagName'));
      return;
    }

    if (allTags.includes(trimmed)) {
      notification.warning(t('tagDialog.warnings.tagExists'));
      return;
    }

    if (trimmed.length > 50) {
      notification.warning(t('tagDialog.warnings.tagTooLong'));
      return;
    }

    // Add to available tags and select it
    setAllTags([...allTags, trimmed].sort());
    setTempSelectedTags([...tempSelectedTags, trimmed]);
    setCustomTag('');
    notification.success(t('tagDialog.notifications.tagAdded', { name: trimmed }));
  };

  const handleRemoveTag = (tag: string) => {
    // Remove from both selected and available
    setTempSelectedTags(tempSelectedTags.filter(t => t !== tag));
    setAllTags(allTags.filter(t => t !== tag));
    notification.info(t('tagDialog.notifications.tagRemoved', { name: tag }));
  };

  const handleSave = () => {
    onSave(tempSelectedTags);
    setCustomTag('');
  };

  const handleCancel = () => {
    setCustomTag('');
    onCancel();
  };

  const handleSelectAll = () => {
    setTempSelectedTags([...allTags]);
  };

  const handleClearAll = () => {
    setTempSelectedTags([]);
  };

  const content = (
    <div>
      <Space orientation="vertical" className="tag-select-container" size="large">
        {/* Add custom tag */}
        <div>
          <div className="tag-select-section-title">{t('tagDialog.addCustomTag')}</div>
          <Input.Group compact className="tag-select-input-group">
            <Input
              className="tag-select-input"
              placeholder={t('tagDialog.customTagPlaceholder')}
              value={customTag}
              onChange={(e) => setCustomTag(e.target.value)}
              onPressEnter={handleAddCustomTag}
              maxLength={50}
            />
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={handleAddCustomTag}
              className="tag-select-add-button"
            >
              {t('common.add')}
            </Button>
          </Input.Group>
        </div>

        <Divider className="tag-select-divider" />

        {/* Selected tags preview */}
        {tempSelectedTags.length > 0 && (
          <div>
            <div className="tag-select-section-title">
              {t('tagDialog.selectedTags', { count: tempSelectedTags.length })}
            </div>
            <Space wrap>
              {tempSelectedTags.map(tag => (
                <Tag
                  key={tag}
                  color="blue"
                  closable
                  onClose={() => handleToggleTag(tag)}
                >
                  {tag}
                </Tag>
              ))}
            </Space>
          </div>
        )}

        <Divider className="tag-select-divider" />

        {/* Available tags with checkboxes */}
        <div>
          <div className="tag-select-section-title">
            {t('tagDialog.availableTags')}
          </div>
          <div className="tag-select-available-container">
            <Row gutter={[16, 16]}>
              {allTags.map(tag => (
                <Col span={12} key={tag}>
                  <div className="tag-select-checkbox-row">
                    <Checkbox
                      checked={tempSelectedTags.includes(tag)}
                      onChange={() => handleToggleTag(tag)}
                    >
                      {tag}
                    </Checkbox>
                    {!commonTags.includes(tag) && (
                      <Button
                        type="text"
                        size="small"
                        danger
                        icon={<DeleteOutlined />}
                        onClick={() => handleRemoveTag(tag)}
                        className="tag-select-delete-button"
                      />
                    )}
                  </div>
                </Col>
              ))}
            </Row>
            {allTags.length === 0 && (
              <div className="tag-select-empty">
                {t('tagDialog.noTagsAvailable')}
              </div>
            )}
          </div>
        </div>
      </Space>

      {/* Footer with action buttons */}
      <div className="slide-in-screen-footer">
        <Space>
          <Button onClick={handleClearAll} size="large">
            {t('common.clearAll')}
          </Button>
          <Button onClick={handleSelectAll} size="large">
            {t('common.selectAll')}
          </Button>
          <Button onClick={handleCancel} size="large">
            {t('common.cancel')}
          </Button>
          <Button type="primary" onClick={handleSave} size="large">
            {t('tagDialog.okWithCount', { count: tempSelectedTags.length })}
          </Button>
        </Space>
      </div>
    </div>
  );

  useSlideInDialog({
    visible,
    title: t('tagDialog.title'),
    content,
    width: '55%',
    onClose: handleCancel,
  });

  return null;
};
