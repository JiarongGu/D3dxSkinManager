import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Checkbox, Input, Button, Space, Row, Col, Divider, Tag } from 'antd';
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons';
import { useSlideInDialog } from '../../../../shared/hooks/useSlideInDialog';

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
      notification.warning('Please enter a tag name');
      return;
    }

    if (allTags.includes(trimmed)) {
      notification.warning('This tag already exists');
      return;
    }

    if (trimmed.length > 50) {
      notification.warning('Tag name is too long (max 50 characters)');
      return;
    }

    // Add to available tags and select it
    setAllTags([...allTags, trimmed].sort());
    setTempSelectedTags([...tempSelectedTags, trimmed]);
    setCustomTag('');
    notification.success(`Tag "${trimmed}" added`);
  };

  const handleRemoveTag = (tag: string) => {
    // Remove from both selected and available
    setTempSelectedTags(tempSelectedTags.filter(t => t !== tag));
    setAllTags(allTags.filter(t => t !== tag));
    notification.info(`Tag "${tag}" removed`);
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
      <Space orientation="vertical" style={{ width: '100%' }} size="large">
        {/* Add custom tag */}
        <div>
          <div style={{ marginBottom: '8px', fontWeight: 500 }}>Add Custom Tag:</div>
          <Input.Group compact>
            <Input
              style={{ width: 'calc(100% - 100px)' }}
              placeholder="Enter custom tag name..."
              value={customTag}
              onChange={(e) => setCustomTag(e.target.value)}
              onPressEnter={handleAddCustomTag}
              maxLength={50}
            />
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={handleAddCustomTag}
              style={{ width: '100px' }}
            >
              Add
            </Button>
          </Input.Group>
        </div>

        <Divider style={{ margin: '12px 0' }} />

        {/* Selected tags preview */}
        {tempSelectedTags.length > 0 && (
          <div>
            <div style={{ marginBottom: '8px', fontWeight: 500 }}>
              Selected Tags ({tempSelectedTags.length}):
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

        <Divider style={{ margin: '12px 0' }} />

        {/* Available tags with checkboxes */}
        <div>
          <div style={{ marginBottom: '12px', fontWeight: 500 }}>
            Available Tags:
          </div>
          <div
            style={{
              maxHeight: '300px',
              overflowY: 'auto',
              border: '1px solid var(--color-border-secondary)',
              borderRadius: '4px',
              padding: '12px',
              background: 'var(--color-bg-elevated)',
            }}
          >
            <Row gutter={[16, 16]}>
              {allTags.map(tag => (
                <Col span={12} key={tag}>
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                    }}
                  >
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
                        style={{ marginLeft: '8px' }}
                      />
                    )}
                  </div>
                </Col>
              ))}
            </Row>
            {allTags.length === 0 && (
              <div style={{ textAlign: 'center', color: 'var(--color-text-tertiary)', padding: '24px' }}>
                No tags available. Add a custom tag above.
              </div>
            )}
          </div>
        </div>
      </Space>

      {/* Footer with action buttons */}
      <div className="slide-in-screen-footer">
        <Space>
          <Button onClick={handleClearAll} size="large">
            Clear All
          </Button>
          <Button onClick={handleSelectAll} size="large">
            Select All
          </Button>
          <Button onClick={handleCancel} size="large">
            Cancel
          </Button>
          <Button type="primary" onClick={handleSave} size="large">
            OK ({tempSelectedTags.length} selected)
          </Button>
        </Space>
      </div>
    </div>
  );

  useSlideInDialog({
    visible,
    title: 'Select Tags',
    content,
    width: '55%',
    onClose: handleCancel,
  });

  return null;
};
