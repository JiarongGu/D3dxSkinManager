import React, { useState, useEffect } from "react";
import { Tag, Select } from 'antd';
import { modService } from "../../../../shared/services/ipc";
import { useProfile } from "../../../../shared/context/ProfileContext";
import type { Tag as TagType } from "../../../../shared/types/mod.types";
import "./MultiTagInput.css";
import { CompactSelect } from '../../../../shared/components/compact';

const { Option } = Select;

// Color palette matching backend TagRepository.cs
const COLOR_PALETTE = [
  '#1890ff', // Blue
  '#52c41a', // Green
  '#fa8c16', // Orange
  '#722ed1', // Purple
  '#eb2f96', // Magenta
  '#13c2c2', // Cyan
  '#faad14', // Gold
  '#2f54eb', // Geek Blue
  '#a0d911', // Lime
  '#f5222d', // Red
];

const getRandomColor = (): string => {
  return COLOR_PALETTE[Math.floor(Math.random() * COLOR_PALETTE.length)];
};

interface MultiTagInputProps {
  value?: string[];
  onChange?: (tags: string[]) => void;
  availableTags?: string[];
  placeholder?: string;
  maxTags?: number;
  disabled?: boolean;
  tagColorsMap?: Map<string, string>;
  setTagColorsMap?: (map: Map<string, string>) => void;
}

/**
 * MultiTagInput Component
 * A multi-tag input field using Select mode="tags"
 * Features:
 * - Visual tag chips inline
 * - Autocomplete from available tags
 * - Add tags by pressing Enter or comma
 * - Remove tags with close button
 * - Auto-creates tags in database with random colors
 */
export const MultiTagInput: React.FC<MultiTagInputProps> = ({
  value = [],
  onChange,
  availableTags = [],
  placeholder = "Type to add tags...",
  maxTags,
  disabled = false,
  tagColorsMap,
  setTagColorsMap,
}) => {
  const [searchValue, setSearchValue] = useState("");

  const handleChange = async (newTags: string[]) => {
    // Filter out empty strings and trim whitespace
    const cleanedTags = newTags
      .map((tag) => tag.trim())
      .filter((tag) => tag.length > 0 && tag.length <= 50);

    // Pre-generate colors for new tags that don't have a color yet
    if (tagColorsMap && setTagColorsMap) {
      const newlyAddedTags = cleanedTags.filter(
        (tag) => !tagColorsMap.has(tag)
      );

      if (newlyAddedTags.length > 0) {
        const updatedColorsMap = new Map(tagColorsMap);
        newlyAddedTags.forEach((tagName) => {
          updatedColorsMap.set(tagName, getRandomColor());
        });
        setTagColorsMap(updatedColorsMap);
      }
    }

    onChange?.(cleanedTags);
  };

  const handleSearch = (value: string) => {
    setSearchValue(value);
  };

  // Filter available tags that haven't been selected yet
  const filteredOptions = availableTags
    .filter((tag) => !value.includes(tag))
    .filter(
      (tag) =>
        !searchValue || tag.toLowerCase().includes(searchValue.toLowerCase()),
    );

  // Custom tag renderer with colors
  const tagRender = (props: any) => {
    const { label, closable, onClose } = props;
    // Use color from the shared tagColorsMap, or default
    const color = tagColorsMap?.get(label as string) || 'default';

    return (
      <Tag
        color={color}
        closable={closable}
        onClose={onClose}
        style={{ marginRight: 3 }}
      >
        {label}
      </Tag>
    );
  };

  return (
    <CompactSelect
      mode="tags"
      value={value}
      onChange={handleChange}
      onSearch={handleSearch}
      placeholder={placeholder}
      disabled={disabled}
      maxTagCount="responsive"
      tokenSeparators={[","]}
      tagRender={tagRender}
      showSearch={{
        filterOption: false,
      }}
      classNames={{
        root: "multi-tag-input",
        popup: {
          root: "multi-tag-input-dropdown",
        },
      }}
      notFoundContent={"Start typing to see suggestions..."}
    >
      {filteredOptions.map((tag) => (
        <Option key={tag} value={tag}>
          {tag}
        </Option>
      ))}
    </CompactSelect>
  );
};
