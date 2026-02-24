import React, { useState, useEffect, useMemo } from 'react';
import { Input, Checkbox, Empty, Space } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { FormDialog } from '../../../../shared/components/dialogs/FormDialog';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';
import './TagSelectorDialog.css';

const { Search } = Input;

export interface TagSelectorDialogProps {
  visible: boolean;
  availableTags: string[];
  selectedTags: string[];
  onConfirm: (tags: string[]) => void;
  onCancel: () => void;
  title?: string;
}

/**
 * Dialog for selecting multiple tags with checkboxes
 * Features:
 * - Search/filter tags
 * - Checkbox selection
 * - Shows selected count
 * - Select All/Deselect All actions
 * - Uses FormDialog for consistent UI
 *
 * Usage: Can be used in import workflow, mod editing, batch operations, etc.
 */
export const TagSelectorDialog: React.FC<TagSelectorDialogProps> = ({
  visible,
  availableTags,
  selectedTags,
  onConfirm,
  onCancel,
  title = 'Select Tags',
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [localSelectedTags, setLocalSelectedTags] = useState<string[]>(selectedTags);

  // Reset local state when dialog opens or selectedTags changes
  useEffect(() => {
    if (visible) {
      setLocalSelectedTags(selectedTags);
      setSearchTerm('');
    }
  }, [visible, selectedTags]);

  // Filter tags based on search term
  const filteredTags = useMemo(() => {
    if (!searchTerm) {
      return availableTags;
    }
    const lowerSearch = searchTerm.toLowerCase();
    return availableTags.filter(tag =>
      tag.toLowerCase().includes(lowerSearch)
    );
  }, [availableTags, searchTerm]);

  const handleToggleTag = (tag: string) => {
    setLocalSelectedTags(prev =>
      prev.includes(tag)
        ? prev.filter(t => t !== tag)
        : [...prev, tag]
    );
  };

  const handleConfirm = () => {
    onConfirm(localSelectedTags);
  };

  const handleSelectAll = () => {
    setLocalSelectedTags(filteredTags);
  };

  const handleDeselectAll = () => {
    setLocalSelectedTags([]);
  };

  return (
    <FormDialog
      visible={visible}
      title={title}
      onOk={handleConfirm}
      onCancel={onCancel}
      okText={`Confirm (${localSelectedTags.length})`}
      cancelText="Cancel"
      width={600}
      destroyOnHidden
    >
      <div className="tag-selector-dialog">
        {/* Search bar */}
        <div className="tag-selector-search">
          <Search
            placeholder="Search tags..."
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            prefix={<SearchOutlined />}
            allowClear
          />
        </div>

        {/* Selection actions */}
        <div className="tag-selector-actions">
          <Space size="small">
            <CompactButton size="small" onClick={handleSelectAll}>
              Select All ({filteredTags.length})
            </CompactButton>
            <CompactButton size="small" onClick={handleDeselectAll}>
              Deselect All
            </CompactButton>
          </Space>
          <div className="tag-selector-count">
            {localSelectedTags.length} selected
          </div>
        </div>

        {/* Tag list */}
        <div className="tag-selector-list">
          {filteredTags.length === 0 ? (
            <Empty
              description={searchTerm ? 'No tags found' : 'No tags available'}
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          ) : (
            filteredTags.map(tag => (
              <div
                key={tag}
                className={`tag-selector-item ${
                  localSelectedTags.includes(tag) ? 'selected' : ''
                }`}
                onClick={() => handleToggleTag(tag)}
              >
                <Checkbox
                  checked={localSelectedTags.includes(tag)}
                  onChange={() => handleToggleTag(tag)}
                >
                  {tag}
                </Checkbox>
              </div>
            ))
          )}
        </div>
      </div>
    </FormDialog>
  );
};
