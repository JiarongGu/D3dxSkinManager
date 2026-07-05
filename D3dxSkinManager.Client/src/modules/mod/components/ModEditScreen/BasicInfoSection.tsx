import React from 'react';
import { Form } from 'antd';
import { useTranslation } from 'react-i18next';
import { CompactTextArea, CompactInput } from '../../../../shared/components/compact';


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
        rules={[
          { required: true, message: t('mods.edit.nameRequired') },
          { whitespace: true, message: t('mods.edit.nameRequired') }
        ]}
      >
        <CompactInput placeholder={t('mods.edit.namePlaceholder')} />
      </Form.Item>

      {/* Description */}
      <Form.Item
        label={t("common.description")}
        name="description"
        tooltip={t('mods.edit.descriptionTooltip')}
      >
        <CompactTextArea
          placeholder={t('mods.edit.descriptionPlaceholder')}
          rows={3}
          showCount
          maxLength={500}
        />
      </Form.Item>
    </>
  );
};
