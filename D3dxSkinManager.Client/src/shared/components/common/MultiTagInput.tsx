import React, { useState } from 'react';
import { Select, Space } from 'antd';
import { TagsOutlined } from '@ant-design/icons';
import { CompactButton } from '../compact/CompactButton';
import './MultiTagInput.css';

const { Option } = Select;

export interface MultiTagInputProps {
  value?: string[];
  onChange?: (tags: string[]) => void;
  availableTags?: string[];
  onOpenTagSelector?: () => void;
  placeholder?: string;
  maxTagTextLength?: number;
}

/**
 * Multi-tag input component with autocomplete and tag selector
 * Features:
 * - Type to add tags with autocomplete dropdown
 * - Create new tags by typing and pressing Enter
 * - Button to open full tag list selector
 * - Tags are saved when the parent form saves
 */
export const MultiTagInput: React.FC<MultiTagInputProps> = ({
  value = [],
  onChange,
  availableTags = [],
  onOpenTagSelector,
  placeholder = 'Type to add tags...',
  maxTagTextLength = 50,
}) => {
  const [inputValue, setInputValue] = useState('');

  const handleChange = (newTags: string[]) => {
    // Filter out empty strings and trim whitespace
    const cleanedTags = newTags
      .map(tag => tag.trim())
      .filter(tag => tag.length > 0)
      .filter(tag => tag.length <= maxTagTextLength);

    onChange?.(cleanedTags);
  };

  const handleSearch = (searchValue: string) => {
    setInputValue(searchValue);
  };

  // Filter options based on input value
  const filteredOptions = availableTags
    .filter(tag => !value.includes(tag)) // Exclude already selected tags
    .filter(tag =>
      inputValue.length === 0 ||
      tag.toLowerCase().includes(inputValue.toLowerCase())
    );

  return (
    <div className="multi-tag-input">
      <Space.Compact style={{ width: '100%' }}>
        <Select
          mode="tags"
          value={value}
          onChange={handleChange}
          onSearch={handleSearch}
          placeholder={placeholder}
          style={{ flex: 1 }}
          className="multi-tag-input-select"
          maxTagCount="responsive"
          maxTagTextLength={maxTagTextLength}
          tokenSeparators={[',']}
          showSearch
          filterOption={false}
          notFoundContent={
            inputValue.length > 0
              ? `Press Enter to create "${inputValue}"`
              : 'Start typing to see suggestions'
          }
        >
          {filteredOptions.map(tag => (
            <Option key={tag} value={tag}>
              {tag}
            </Option>
          ))}
        </Select>
        {onOpenTagSelector && (
          <CompactButton
            icon={<TagsOutlined />}
            onClick={onOpenTagSelector}
            title="Open tag selector"
            className="multi-tag-input-button"
          />
        )}
      </Space.Compact>
    </div>
  );
};
