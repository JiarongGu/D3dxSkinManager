import React from 'react';
import { Form, Space, Select, AutoComplete } from 'antd';

const { Option } = Select;

export interface MetadataSectionProps {
  authors: string[];
  categories: string[];
}

// Age rating options
const ageRatingOptions = [
  { value: 'G', label: 'G - General', description: 'Suitable for all ages' },
  { value: 'P', label: 'P - Parental Guidance', description: 'Parental guidance suggested, some content may not be suitable for children' },
  { value: 'R', label: 'R - Restricted', description: 'Restricted to mature audiences, contains adult themes' },
  { value: 'X', label: 'X - Adults Only', description: 'Strictly for adults 18+, explicit content' },
];

// Tooltip content for age rating
const ageRatingTooltip = (
  <div>
    <div style={{ marginBottom: '8px' }}><strong>Age Rating Guidelines:</strong></div>
    {ageRatingOptions.map(option => (
      <div key={option.value} style={{ marginBottom: '4px' }}>
        <strong>{option.label}:</strong> {option.description}
      </div>
    ))}
  </div>
);

/**
 * Metadata section for mod editing
 * Includes author, category, and age rating fields
 */
export const MetadataSection: React.FC<MetadataSectionProps> = ({
  authors,
  categories,
}) => {
  return (
    <Space style={{ width: '100%' }} size="middle">
      <Form.Item
        label="Author"
        name="author"
        style={{ flex: 1, minWidth: '200px' }}
      >
        <AutoComplete
          placeholder="Mod author name"
          options={authors.map(author => ({ value: author }))}
          filterOption={(inputValue, option) =>
            option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
          }
        />
      </Form.Item>

      <Form.Item
        label="Category"
        name="category"
        style={{ flex: 1, minWidth: '200px' }}
      >
        <AutoComplete
          placeholder="e.g., Character, Weapon"
          options={categories.map(cat => ({ value: cat }))}
          filterOption={(inputValue, option) =>
            option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
          }
        />
      </Form.Item>

      <Form.Item
        label="Age Rating"
        name="grading"
        tooltip={ageRatingTooltip}
        style={{ width: '180px' }}
      >
        <Select placeholder="Select rating">
          {ageRatingOptions.map(option => (
            <Option key={option.value} value={option.value}>
              {option.label}
            </Option>
          ))}
        </Select>
      </Form.Item>
    </Space>
  );
};
