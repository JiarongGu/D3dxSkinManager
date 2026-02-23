import { notification } from '../../../shared/utils/notification';
import React, { useState } from 'react';
import { Modal, Form, Input, Select, Button, Space, Checkbox, Divider, Alert } from 'antd';
import { TagsOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../shared/types/mod.types';
import { ImportTask } from './AddModWindow';
import { useTranslation } from 'react-i18next';
import './BatchEditUnit.css';

const { TextArea } = Input;
const { Option } = Select;

interface BatchEditUnitProps {
  visible: boolean;
  selectedTasks: ImportTask[];
  onSave: (taskIds: string[], modData: Partial<ModInfo>, fieldMask: string[]) => void;
  onCancel: () => void;
  onOpenTagSelector?: (currentTags: string[]) => void;
}

/**
 * Batch edit component for import tasks
 * Similar to BatchEditDialog but for import tasks
 * Uses checkboxes to select which fields to update
 */
export const BatchEditUnit: React.FC<BatchEditUnitProps> = ({
  visible,
  selectedTasks,
  onSave,
  onCancel,
  onOpenTagSelector,
}) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [selectedTags, setSelectedTags] = useState<string[]>([]);

  // Field enable/disable state
  const [enabledFields, setEnabledFields] = useState({
    description: false,
    grading: false,
    author: false,
    category: false,
    tags: false,
  });

  const handleFieldToggle = (field: keyof typeof enabledFields) => {
    setEnabledFields(prev => ({ ...prev, [field]: !prev[field] }));
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();

      // Only include enabled fields
      const modData: Partial<ModInfo> = {};
      const fieldMask: string[] = [];

      if (enabledFields.description) {
        modData.description = values.description;
        fieldMask.push('description');
      }
      if (enabledFields.grading) {
        modData.grading = values.grading;
        fieldMask.push('grading');
      }
      if (enabledFields.author) {
        modData.author = values.author;
        fieldMask.push('author');
      }
      if (enabledFields.category) {
        modData.category = values.category;
        fieldMask.push('category');
      }
      if (enabledFields.tags) {
        modData.tags = selectedTags;
        fieldMask.push('tags');
      }

      if (fieldMask.length === 0) {
        notification.warning(t('batchEdit.selectOneField'));
        return;
      }

      setSaving(true);
      const taskIds = selectedTasks.map(task => task.id);
      onSave(taskIds, modData, fieldMask);

      notification.success(t('batchEdit.tasksUpdated', { count: selectedTasks.length }));
      handleReset();
      onCancel();
    } catch (error) {
      console.error('Validation failed:', error);
      notification.error(t('batchEdit.checkFields'));
    } finally {
      setSaving(false);
    }
  };

  const handleReset = () => {
    form.resetFields();
    setSelectedTags([]);
    setEnabledFields({
      description: false,
      grading: false,
      author: false,
      category: false,
      tags: false,
    });
  };

  const handleCancel = () => {
    handleReset();
    onCancel();
  };

  const handleOpenTagSelector = () => {
    if (onOpenTagSelector) {
      onOpenTagSelector(selectedTags);
    } else {
      notification.info(t('addMod.tagSelectorNotImplemented'));
    }
  };

  // Grading options
  const gradingOptions = [
    { value: 0, label: t('grading.notRated') },
    { value: 1, label: t('grading.poor') },
    { value: 2, label: t('grading.fair') },
    { value: 3, label: t('grading.good') },
    { value: 4, label: t('grading.veryGood') },
    { value: 5, label: t('grading.excellent') },
  ];

  return (
    <Modal
      title={t('batchEdit.title', { count: selectedTasks.length })}
      open={visible}
      onCancel={handleCancel}
      width={700}
      footer={[
        <Button key="reset" onClick={handleReset}>
          {t('batchEdit.reset')}
        </Button>,
        <Button key="cancel" onClick={handleCancel}>
          {t('common.cancel')}
        </Button>,
        <Button key="save" type="primary" onClick={handleSave} loading={saving}>
          {t('batchEdit.applyTo', { count: selectedTasks.length })}
        </Button>,
      ]}
    >
      <Space orientation="vertical" className="batch-edit-unit-container" size="large">
        <Alert
          title={t('batchEdit.alertTitle')}
          description={t('batchEdit.alertDescription', { count: selectedTasks.length })}
          type="info"
          showIcon
        />

        <Form
          form={form}
          layout="vertical"
          autoComplete="off"
        >
          {/* Description */}
          <Space align="start" className="batch-edit-unit-field-row">
            <Checkbox
              checked={enabledFields.description}
              onChange={() => handleFieldToggle('description')}
              className="batch-edit-unit-checkbox"
            />
            <Form.Item
              label={t('addMod.description')}
              name="description"
              className="batch-edit-unit-field"
            >
              <TextArea
                placeholder={t('batchEdit.descriptionPlaceholder')}
                rows={3}
                disabled={!enabledFields.description}
                showCount
                maxLength={500}
              />
            </Form.Item>
          </Space>

          <Divider className="batch-edit-unit-divider" />

          {/* Author */}
          <Space align="start" className="batch-edit-unit-field-row">
            <Checkbox
              checked={enabledFields.author}
              onChange={() => handleFieldToggle('author')}
              className="batch-edit-unit-checkbox"
            />
            <Form.Item
              label={t('addMod.author')}
              name="author"
              className="batch-edit-unit-field"
            >
              <Input
                placeholder={t('batchEdit.authorPlaceholder')}
                disabled={!enabledFields.author}
              />
            </Form.Item>
          </Space>

          <Divider className="batch-edit-unit-divider" />

          {/* Category */}
          <Space align="start" className="batch-edit-unit-field-row">
            <Checkbox
              checked={enabledFields.category}
              onChange={() => handleFieldToggle('category')}
              className="batch-edit-unit-checkbox"
            />
            <Form.Item
              label={t('addMod.category')}
              name="category"
              className="batch-edit-unit-field"
            >
              <Input
                placeholder={t('batchEdit.categoryPlaceholder')}
                disabled={!enabledFields.category}
              />
            </Form.Item>
          </Space>

          <Divider className="batch-edit-unit-divider" />

          {/* Grading */}
          <Space align="start" className="batch-edit-unit-field-row">
            <Checkbox
              checked={enabledFields.grading}
              onChange={() => handleFieldToggle('grading')}
              className="batch-edit-unit-checkbox"
            />
            <Form.Item
              label={t('addMod.grading')}
              name="grading"
              className="batch-edit-unit-field"
            >
              <Select placeholder={t('addMod.gradingPlaceholder')} disabled={!enabledFields.grading}>
                {gradingOptions.map(option => (
                  <Option key={option.value} value={option.value}>
                    {option.label}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </Space>

          <Divider className="batch-edit-unit-divider" />

          {/* Tags */}
          <Space align="start" className="batch-edit-unit-field-row">
            <Checkbox
              checked={enabledFields.tags}
              onChange={() => handleFieldToggle('tags')}
              className="batch-edit-unit-checkbox"
            />
            <Form.Item
              label={t('addMod.tags')}
              className="batch-edit-unit-field"
            >
              <Button
                icon={<TagsOutlined />}
                onClick={handleOpenTagSelector}
                disabled={!enabledFields.tags}
                block
              >
                {selectedTags.length > 0
                  ? t('addMod.selectedTags', { tags: selectedTags.join(', ') })
                  : t('addMod.selectTags')}
              </Button>
            </Form.Item>
          </Space>
        </Form>

        {/* Summary */}
        <Alert
          title={
            Object.values(enabledFields).filter(Boolean).length > 0
              ? t('batchEdit.summaryFields', { count: Object.values(enabledFields).filter(Boolean).length, taskCount: selectedTasks.length })
              : t('batchEdit.summaryNoFields')
          }
          type={Object.values(enabledFields).filter(Boolean).length > 0 ? 'success' : 'warning'}
        />
      </Space>
    </Modal>
  );
};
