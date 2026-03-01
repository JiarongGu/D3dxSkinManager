/**
 * ModImport Metadata Dialog Component
 *
 * Compact form dialog for editing mod import metadata.
 * Uses FormDialog and compact components for consistent styling.
 * Supports editing metadata during compression or before final import confirmation.
 */

import React, { useEffect, useState } from 'react';
import { Form } from 'antd';
import { useTranslation } from 'react-i18next';
import { FormDialog } from '../../../../shared/components/dialogs/FormDialog';
import { CompactInput, CompactTextArea, CompactSelect } from '../../../../shared/components/compact';
import { WorkflowInfo, ModImportWorkflowContext } from '../../types/workflow.types';
import { categoryService } from '../../../../shared/services/categoryService';
import { useProfile } from '../../../../shared/context/ProfileContext';
import './ModImportMetadataDialog.css';

export interface ModImportMetadataFormValues {
  name: string;
  author?: string;
  description?: string;
  category?: string;
  grading?: string;
}

interface ModImportMetadataDialogProps {
  visible: boolean;
  workflow: WorkflowInfo | null;
  context: ModImportWorkflowContext | null;
  onCancel: () => void;
  onSubmit: (values: ModImportMetadataFormValues) => Promise<void>;
}

export const ModImportMetadataDialog: React.FC<ModImportMetadataDialogProps> = ({
  visible,
  workflow,
  context,
  onCancel,
  onSubmit,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [form] = Form.useForm();
  const [categoryOptions, setCategoryOptions] = useState<{ value: string; label: string }[]>([]);
  const [loadingCategories, setLoadingCategories] = useState(false);

  const ageRatingOptions = [
    { value: 'G', label: t('mods.edit.ageRating.general') },
    { value: 'P', label: t('mods.edit.ageRating.parentalGuidance') },
    { value: 'R', label: t('mods.edit.ageRating.restricted') },
    { value: 'X', label: t('mods.edit.ageRating.adultsOnly') },
  ];

  // Load categories when modal opens
  useEffect(() => {
    if (visible && selectedProfileId) {
      setLoadingCategories(true);
      categoryService.getCategoryTree(selectedProfileId)
        .then(tree => {
          // Flatten the tree and create options
          const flatCategories = categoryService.flattenTree(tree);
          const options = flatCategories.map(cat => ({
            value: cat.name,
            label: cat.name,
          }));
          setCategoryOptions(options);
        })
        .catch(error => {
          console.error('[ModImportMetadataDialog] Failed to load categories:', error);
        })
        .finally(() => {
          setLoadingCategories(false);
        });
    }
  }, [visible, selectedProfileId]);

  // Pre-fill form when context changes
  useEffect(() => {
    if (visible && context) {
      form.setFieldsValue({
        name: context.name || context.folderName || '',
        author: context.author || '',
        description: context.description || '',
        category: context.category || '',
        grading: context.grading || 'G',
      });
    }
  }, [visible, context, form]);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      await onSubmit(values);
    } catch (error) {
      console.error('[ModImportMetadataDialog] Form validation failed:', error);
      // Don't close modal if validation fails
      throw error;
    }
  };

  const handleCancel = () => {
    form.resetFields();
    onCancel();
  };

  return (
    <FormDialog
      visible={visible}
      title={t('workflow.modImport.provideMetadata')}
      onCancel={handleCancel}
      onOk={handleSubmit}
      okText={t('common.save')}
      cancelText={t('common.cancel')}
      width={600}
    >
      <Form form={form} layout="vertical" className="mod-import-metadata-dialog-form">
        {/* Mod Name - Full Width */}
        <Form.Item
          name="name"
          label={t('mods.edit.name')}
          rules={[{ required: true, message: t('mods.edit.nameRequired') }]}
        >
          <CompactInput placeholder={t('mods.edit.namePlaceholder')} />
        </Form.Item>

        {/* Category, Author, Age Rating - 3 Columns */}
        <div className="mod-import-metadata-dialog-row">
          <Form.Item
            name="category"
            label={t('mods.edit.category')}
            tooltip={t('mods.edit.categoryTooltip') || 'Leave empty for Unclassified'}
            className="mod-import-metadata-dialog-col"
          >
            <CompactSelect
              showSearch
              allowClear
              placeholder={t('mods.edit.categoryPlaceholder') || 'Select or type category name'}
              options={categoryOptions}
              loading={loadingCategories}
              filterOption={(input, option) =>
                String(option?.label ?? '').toLowerCase().includes(input.toLowerCase())
              }
            />
          </Form.Item>

          <Form.Item
            name="author"
            label={t('mods.edit.author')}
            className="mod-import-metadata-dialog-col"
          >
            <CompactInput placeholder={t('mods.edit.authorPlaceholder')} />
          </Form.Item>

          <Form.Item
            name="grading"
            label={t('mods.edit.ageRating.label')}
            className="mod-import-metadata-dialog-col"
          >
            <CompactSelect options={ageRatingOptions} />
          </Form.Item>
        </div>

        {/* Description - Full Width */}
        <Form.Item name="description" label={t('mods.edit.description')}>
          <CompactTextArea rows={3} placeholder={t('mods.edit.descriptionPlaceholder')} />
        </Form.Item>
      </Form>
    </FormDialog>
  );
};
