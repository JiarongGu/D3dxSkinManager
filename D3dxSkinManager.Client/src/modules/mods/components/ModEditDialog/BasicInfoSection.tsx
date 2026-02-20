import React from 'react';
import { Form, Input } from 'antd';
import { useTranslation } from 'react-i18next';

const { TextArea } = Input;

/**
 * Basic information section for mod editing
 * Includes mod name and description fields
 */
export const BasicInfoSection: React.FC = () => {
  const { t } = useTranslation();

  return (
    <>
      {/* Name */}
      <Form.Item
        label={t('mods.edit.name')}
        name="name"
        rules={[{ required: true, message: t('mods.edit.nameRequired') }]}
      >
        <Input placeholder={t('mods.edit.namePlaceholder')} />
      </Form.Item>

      {/* Description */}
      <Form.Item
        label={t('mods.edit.description')}
        name="description"
        tooltip={t('mods.edit.descriptionTooltip')}
      >
        <TextArea
          placeholder={t('mods.edit.descriptionPlaceholder')}
          rows={3}
          showCount
          maxLength={500}
        />
      </Form.Item>
    </>
  );
};
