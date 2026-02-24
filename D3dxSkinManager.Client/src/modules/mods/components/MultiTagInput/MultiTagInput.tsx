import React, { useState } from "react";
import { Select } from "antd";
import "./MultiTagInput.css";

const { Option } = Select;

interface MultiTagInputProps {
  value?: string[];
  onChange?: (tags: string[]) => void;
  availableTags?: string[];
  placeholder?: string;
  maxTags?: number;
  disabled?: boolean;
}

/**
 * MultiTagInput Component
 * A multi-tag input field using Select mode="tags"
 * Features:
 * - Visual tag chips inline
 * - Autocomplete from available tags
 * - Add tags by pressing Enter or comma
 * - Remove tags with close button
 */
export const MultiTagInput: React.FC<MultiTagInputProps> = ({
  value = [],
  onChange,
  availableTags = [],
  placeholder = "Type to add tags...",
  maxTags,
  disabled = false,
}) => {
  const [searchValue, setSearchValue] = useState("");

  const handleChange = (newTags: string[]) => {
    // Filter out empty strings and trim whitespace
    const cleanedTags = newTags
      .map((tag) => tag.trim())
      .filter((tag) => tag.length > 0 && tag.length <= 50);

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

  return (
    <Select
      mode="tags"
      value={value}
      onChange={handleChange}
      onSearch={handleSearch}
      placeholder={placeholder}
      disabled={disabled}
      maxTagCount="responsive"
      tokenSeparators={[","]}
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
    </Select>
  );
};
