import React, { useState } from 'react';
import { Modal, Form, Input, Select, Button, Space, Checkbox, Divider, Alert, message } from 'antd';
import { TagsOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../shared/types/mod.types';
import { ImportTask } from './AddModWindow';

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
        message.warning('Please select at least one field to update');
        return;
      }

      setSaving(true);
      const taskIds = selectedTasks.map(task => task.id);
      onSave(taskIds, modData, fieldMask);

      message.success(`${selectedTasks.length} task(s) updated successfully`);
      handleReset();
      onCancel();
    } catch (error) {
      console.error('Validation failed:', error);
      message.error('Please check all required fields');
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
      message.info('Tag selector not implemented yet');
    }
  };

  // Grading options
  const gradingOptions = [
    { value: 0, label: 'Not Rated' },
    { value: 1, label: '�?Poor' },
    { value: 2, label: '★★ Fair' },
    { value: 3, label: '★★�?Good' },
    { value: 4, label: '★★★★ Very Good' },
    { value: 5, label: '★★★★�?Excellent' },
  ];

  return (
    <Modal
      title={`Batch Edit ${selectedTasks.length} Import Task(s)`}
      open={visible}
      onCancel={handleCancel}
      width={700}
      footer={[
        <Button key="reset" onClick={handleReset}>
          Reset
        </Button>,
        <Button key="cancel" onClick={handleCancel}>
          Cancel
        </Button>,
        <Button key="save" type="primary" onClick={handleSave} loading={saving}>
          Apply to {selectedTasks.length} Task(s)
        </Button>,
      ]}
    >
      <Space orientation="vertical" style={{ width: '100%' }} size="large">
        <Alert
          title="Batch Edit Mode"
          description={`Check the fields you want to update for all ${selectedTasks.length} selected import task(s). Unchecked fields will remain unchanged.`}
          type="info"
          showIcon
        />

        <Form
          form={form}
          layout="vertical"
          autoComplete="off"
        >
          {/* Description */}
          <Space align="start" style={{ width: '100%' }}>
            <Checkbox
              checked={enabledFields.description}
              onChange={() => handleFieldToggle('description')}
              style={{ marginTop: '30px' }}
            />
            <Form.Item
              label="Description"
              name="description"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <TextArea
                placeholder="Enter description for all selected tasks..."
                rows={3}
                disabled={!enabledFields.description}
                showCount
                maxLength={500}
              />
            </Form.Item>
          </Space>

          <Divider style={{ margin: '12px 0' }} />

          {/* Author */}
          <Space align="start" style={{ width: '100%' }}>
            <Checkbox
              checked={enabledFields.author}
              onChange={() => handleFieldToggle('author')}
              style={{ marginTop: '30px' }}
            />
            <Form.Item
              label="Author"
              name="author"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <Input
                placeholder="Set author for all selected tasks"
                disabled={!enabledFields.author}
              />
            </Form.Item>
          </Space>

          <Divider style={{ margin: '12px 0' }} />

          {/* Category */}
          <Space align="start" style={{ width: '100%' }}>
            <Checkbox
              checked={enabledFields.category}
              onChange={() => handleFieldToggle('category')}
              style={{ marginTop: '30px' }}
            />
            <Form.Item
              label="Category"
              name="category"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <Input
                placeholder="Set category for all selected tasks"
                disabled={!enabledFields.category}
              />
            </Form.Item>
          </Space>

          <Divider style={{ margin: '12px 0' }} />

          {/* Grading */}
          <Space align="start" style={{ width: '100%' }}>
            <Checkbox
              checked={enabledFields.grading}
              onChange={() => handleFieldToggle('grading')}
              style={{ marginTop: '30px' }}
            />
            <Form.Item
              label="Grading"
              name="grading"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <Select placeholder="Select grading" disabled={!enabledFields.grading}>
                {gradingOptions.map(option => (
                  <Option key={option.value} value={option.value}>
                    {option.label}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </Space>

          <Divider style={{ margin: '12px 0' }} />

          {/* Tags */}
          <Space align="start" style={{ width: '100%' }}>
            <Checkbox
              checked={enabledFields.tags}
              onChange={() => handleFieldToggle('tags')}
              style={{ marginTop: '30px' }}
            />
            <Form.Item
              label="Tags"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <Button
                icon={<TagsOutlined />}
                onClick={handleOpenTagSelector}
                disabled={!enabledFields.tags}
                block
              >
                {selectedTags.length > 0
                  ? `Selected Tags: ${selectedTags.join(', ')}`
                  : 'Select Tags...'}
              </Button>
            </Form.Item>
          </Space>
        </Form>

        {/* Summary */}
        <Alert
          title={
            Object.values(enabledFields).filter(Boolean).length > 0
              ? `${Object.values(enabledFields).filter(Boolean).length} field(s) will be updated for ${selectedTasks.length} task(s)`
              : 'No fields selected for update'
          }
          type={Object.values(enabledFields).filter(Boolean).length > 0 ? 'success' : 'warning'}
        />
      </Space>
    </Modal>
  );
};
