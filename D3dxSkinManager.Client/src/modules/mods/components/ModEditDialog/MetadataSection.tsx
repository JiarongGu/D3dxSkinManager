import React from 'react';
import { Form, Space, Select, AutoComplete } from 'antd';
import { useTranslation } from 'react-i18next';

const { Option } = Select;

export interface MetadataSectionProps {
  authors: string[];
  categories: string[];
}

/**
 * Metadata section for mod editing
 * Includes author, category, and age rating fields
 */
export const MetadataSection: React.FC<MetadataSectionProps> = ({
  authors,
  categories,
}) => {
  const { t } = useTranslation();

  // Age rating options
  const ageRatingOptions = [
    { value: 'G', label: t('mods.edit.ageRating.general'), description: t('mods.edit.ageRating.generalDesc') },
    { value: 'P', label: t('mods.edit.ageRating.parentalGuidance'), description: t('mods.edit.ageRating.parentalGuidanceDesc') },
    { value: 'R', label: t('mods.edit.ageRating.restricted'), description: t('mods.edit.ageRating.restrictedDesc') },
    { value: 'X', label: t('mods.edit.ageRating.adultsOnly'), description: t('mods.edit.ageRating.adultsOnlyDesc') },
  ];

  // Tooltip content for age rating
  const ageRatingTooltip = (
    <div>
      <div style={{ marginBottom: '8px' }}><strong>{t('mods.edit.ageRating.guidelines')}</strong></div>
      {ageRatingOptions.map(option => (
        <div key={option.value} style={{ marginBottom: '4px' }}>
          <strong>{option.label}:</strong> {option.description}
        </div>
      ))}
    </div>
  );

  return (
    <Space style={{ width: '100%' }} size="middle">
      <Form.Item
        label={t('mods.edit.author')}
        name="author"
        style={{ flex: 1, minWidth: '200px' }}
      >
        <AutoComplete
          placeholder={t('mods.edit.authorPlaceholder')}
          options={authors.map(author => ({ value: author }))}
          filterOption={(inputValue, option) =>
            option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
          }
        />
      </Form.Item>

      <Form.Item
        label={t('mods.edit.category')}
        name="category"
        style={{ flex: 1, minWidth: '200px' }}
      >
        <AutoComplete
          placeholder={t('mods.edit.categoryPlaceholder')}
          options={categories.map(cat => ({ value: cat }))}
          filterOption={(inputValue, option) =>
            option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
          }
        />
      </Form.Item>

      <Form.Item
        label={t('mods.edit.ageRating.label')}
        name="grading"
        tooltip={ageRatingTooltip}
        style={{ width: '180px' }}
      >
        <Select placeholder={t('mods.edit.ageRating.placeholder')}>
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
