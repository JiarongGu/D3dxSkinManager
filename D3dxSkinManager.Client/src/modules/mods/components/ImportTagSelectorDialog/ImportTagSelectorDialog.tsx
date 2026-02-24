import React, { useState, useEffect, useMemo } from 'react';
import { Input, Checkbox, Empty, Space } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { useSlideInDialog } from '../../../../shared/hooks/useSlideInDialog';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import './ImportTagSelectorDialog.css';

const { Search } = Input;

/**
 * Dialog for selecting tags during import workflow
 * Used by AddModUnit and BatchEditUnit for tag selection during mod import
 * Features:
 * - Search/filter tags
 * - Checkbox selection
 * - Shows selected count
 * - Select All/Deselect All actions
 *
 * NEW ARCHITECTURE:
 * - Subscribes to its own state from useModsStore
 * - No props needed - gets everything from store
 */
export const ImportTagSelectorDialog: React.FC = () => {
  // Subscribe to state this component needs
  const visible = useModsStore(s => s.tagDialogVisible);
  const availableTags = useModsStore(s => s.availableTags);
  const selectedTags = useModsStore(s => s.currentTags);

  // Get operations
  const { saveTagsForImport, closeTagDialog } = useMods();
  const [searchTerm, setSearchTerm] = useState('');
  const [localSelectedTags, setLocalSelectedTags] = useState<string[]>(selectedTags);

  // Reset local state when dialog opens
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
    saveTagsForImport(localSelectedTags);
  };

  const handleSelectAll = () => {
    setLocalSelectedTags(filteredTags);
  };

  const handleDeselectAll = () => {
    setLocalSelectedTags([]);
  };

  const content = (
    <div className="import-tag-selector-dialog">
      {/* Search bar */}
      <div className="import-tag-selector-search">
        <Search
          placeholder="Search tags..."
          value={searchTerm}
          onChange={e => setSearchTerm(e.target.value)}
          prefix={<SearchOutlined />}
          allowClear
        />
      </div>

      {/* Selection actions */}
      <div className="import-tag-selector-actions">
        <Space size="small">
          <CompactButton size="small" onClick={handleSelectAll}>
            Select All ({filteredTags.length})
          </CompactButton>
          <CompactButton size="small" onClick={handleDeselectAll}>
            Deselect All
          </CompactButton>
        </Space>
        <div className="import-tag-selector-count">
          {localSelectedTags.length} selected
        </div>
      </div>

      {/* Tag list */}
      <div className="import-tag-selector-list">
        {filteredTags.length === 0 ? (
          <Empty
            description={searchTerm ? 'No tags found' : 'No tags available'}
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          />
        ) : (
          filteredTags.map(tag => (
            <div
              key={tag}
              className={`import-tag-selector-item ${
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

      {/* Footer with action buttons */}
      <div className="slide-in-screen-footer">
        <Space>
          <CompactButton onClick={closeTagDialog}>
            Cancel
          </CompactButton>
          <CompactButton type="primary" onClick={handleConfirm}>
            Confirm ({localSelectedTags.length})
          </CompactButton>
        </Space>
      </div>
    </div>
  );

  useSlideInDialog({
    visible,
    title: 'Select Tags for Import',
    content,
    width: '40%',
    onClose: closeTagDialog,
  });

  return null;
};
