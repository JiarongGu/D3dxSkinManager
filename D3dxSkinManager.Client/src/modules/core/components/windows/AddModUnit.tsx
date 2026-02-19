import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Select, Button, Space, Card, Image, message } from 'antd';
import { TagsOutlined, FolderOutlined, FileZipOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../../shared/types/mod.types';
import { ImportTask } from './AddModWindow';

const { TextArea } = Input;
const { Option } = Select;

interface AddModUnitProps {
  visible: boolean;
  task: ImportTask | null;
  onSave: (taskId: string, modData: Partial<ModInfo>) => void;
  onCancel: () => void;
  onOpenTagSelector?: (currentTags: string[]) => void;
}

/**
 * Single mod import form component
 * Allows editing all properties for a single import task
 * Similar to ModEditDialog but for new imports
 */
export const AddModUnit: React.FC<AddModUnitProps> = ({
  visible,
  task,
  onSave,
  onCancel,
  onOpenTagSelector,
}) => {
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [selectedTags, setSelectedTags] = useState<string[]>([]);

  // Initialize form when task changes
  useEffect(() => {
    if (visible && task) {
      form.setFieldsValue({
        name: task.modData.name || '',
        description: task.modData.description || '',
        grading: task.modData.grading || '',
        author: task.modData.author || '',
        category: task.modData.category || '',
      });
      setSelectedTags(task.modData.tags || []);
    }
  }, [visible, task, form]);

  const handleSave = async () => {
    if (!task) return;

    try {
      const values = await form.validateFields();

      const modData: Partial<ModInfo> = {
        name: values.name,
        description: values.description || '',
        grading: values.grading || '',
        author: values.author || '',
        category: values.category || '',
        tags: selectedTags,
      };

      setSaving(true);
      onSave(task.id, modData);

      message.success('Task updated successfully');
      onCancel();
    } catch (error) {
      console.error('Validation failed:', error);
      message.error('Please check all required fields');
    } finally {
      setSaving(false);
    }
  };

  const handleOpenTagSelector = () => {
    if (onOpenTagSelector) {
      onOpenTagSelector(selectedTags);
    } else {
      message.info('Tag selector not implemented yet');
    }
  };

  // Update tags from parent (called by TagSelectDialog)
  useEffect(() => {
    if (visible && task && task.modData.tags) {
      setSelectedTags(task.modData.tags);
    }
  }, [visible, task]);

  // Grading options
  const gradingOptions = [
    { value: 0, label: 'Not Rated' },
    { value: 1, label: '�?Poor' },
    { value: 2, label: '★★ Fair' },
    { value: 3, label: '★★�?Good' },
    { value: 4, label: '★★★★ Very Good' },
    { value: 5, label: '★★★★�?Excellent' },
  ];

  if (!task) return null;

  return (
    <Modal
      title={`Edit Import Task - ${task.id}`}
      open={visible}
      onCancel={onCancel}
      width={700}
      footer={[
        <Button key="cancel" onClick={onCancel}>
          Cancel
        </Button>,
        <Button key="save" type="primary" onClick={handleSave} loading={saving}>
          Save Changes
        </Button>,
      ]}
    >
      <Space orientation="vertical" style={{ width: '100%' }} size="large">
        {/* File Info Card */}
        <Card size="small" style={{ background: '#f5f5f5' }}>
          <Space orientation="vertical" style={{ width: '100%' }}>
            <Space>
              {task.fileType === 'archive' ? <FileZipOutlined /> : <FolderOutlined />}
              <strong>Source File:</strong>
              <span style={{ fontSize: '12px', color: '#595959' }}>{task.fileName}</span>
            </Space>
            <div style={{ fontSize: '11px', color: '#8c8c8c' }}>
              Path: {task.filePath}
            </div>
          </Space>
        </Card>

        {/* Preview Thumbnail */}
        {task.thumbnailUrl && (
          <div style={{ textAlign: 'center' }}>
            <Image
              src={task.thumbnailUrl}
              alt="Mod Preview"
              style={{ maxWidth: '200px', maxHeight: '200px', borderRadius: '4px' }}
              fallback="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mN8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg=="
            />
          </div>
        )}

        {/* Mod Information Form */}
        <Form
          form={form}
          layout="vertical"
          autoComplete="off"
        >
          {/* Name - Required */}
          <Form.Item
            label="Mod Name"
            name="name"
            rules={[{ required: true, message: 'Please enter mod name' }]}
            tooltip="Display name for this mod"
          >
            <Input placeholder="Enter mod name" />
          </Form.Item>

          {/* Category - Required */}
          <Form.Item
            label="Category"
            name="category"
            rules={[{ required: true, message: 'Please enter category' }]}
            tooltip="Character name or object type (e.g., Character, Weapon)"
          >
            <Input placeholder="e.g., Character, Weapon, UI" />
          </Form.Item>

          {/* Description */}
          <Form.Item
            label="Description"
            name="description"
            tooltip="Optional description of what this mod changes"
          >
            <TextArea
              placeholder="Describe what this mod changes..."
              rows={3}
              showCount
              maxLength={500}
            />
          </Form.Item>

          {/* Author */}
          <Form.Item
            label="Author"
            name="author"
            tooltip="Mod creator's name"
          >
            <Input placeholder="Enter author name" />
          </Form.Item>

          {/* Grading */}
          <Form.Item
            label="Grading"
            name="grading"
            tooltip="Your rating of this mod's quality"
          >
            <Select placeholder="Select grading">
              {gradingOptions.map(option => (
                <Option key={option.value} value={option.value}>
                  {option.label}
                </Option>
              ))}
            </Select>
          </Form.Item>

          {/* Tags */}
          <Form.Item
            label="Tags"
            tooltip="Categorize this mod with tags"
          >
            <Space orientation="vertical" style={{ width: '100%' }}>
              <Button
                icon={<TagsOutlined />}
                onClick={handleOpenTagSelector}
                block
              >
                {selectedTags.length > 0
                  ? `Selected Tags: ${selectedTags.join(', ')}`
                  : 'Select Tags...'}
              </Button>
              {selectedTags.length > 0 && (
                <div style={{ fontSize: '12px', color: '#8c8c8c' }}>
                  {selectedTags.length} tag(s) selected
                </div>
              )}
            </Space>
          </Form.Item>
        </Form>
      </Space>
    </Modal>
  );
};

// Export callback type for tag selection
export type TagSelectionCallback = (tags: string[]) => void;
