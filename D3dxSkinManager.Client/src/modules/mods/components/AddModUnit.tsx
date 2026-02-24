import { notification } from '../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Select, Button, Space, Card, Image } from 'antd';
import { TagsOutlined, FolderOutlined, FileZipOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../shared/types/mod.types';
import { ImportTask } from './AddModWindow';
import { useTranslation } from 'react-i18next';
import { useModsStore } from '../store/modsStore';
import { useMods } from '../hooks/useMods';
import './AddModUnit.css';

const { TextArea } = Input;
const { Option } = Select;

/**
 * Single mod import form component
 * Allows editing all properties for a single import task
 * Similar to ModEditDialog but for new imports
 *
 * NEW ARCHITECTURE:
 * - Subscribes to its own state from useModsStore
 * - No props needed - gets everything from store
 */
export const AddModUnit: React.FC = () => {
  // Subscribe to state this component needs
  const visible = useModsStore(s => s.addModUnitVisible);
  const task = useModsStore(s => s.currentEditTask);

  // Get operations
  const { saveAddModUnit, closeAddModUnit, openTagDialog } = useMods();
  const { t } = useTranslation();
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

      // Update task with new modData
      const updatedTask: ImportTask = {
        ...task,
        modData,
      };

      saveAddModUnit(updatedTask);

      notification.success(t('addMod.taskUpdated'));
    } catch (error) {
      console.error('Validation failed:', error);
      notification.error(t('addMod.checkFields'));
    } finally {
      setSaving(false);
    }
  };

  const handleOpenTagSelector = () => {
    openTagDialog('import', selectedTags, task);
  };

  // Update tags from parent (called by TagSelectDialog)
  useEffect(() => {
    if (visible && task && task.modData.tags) {
      setSelectedTags(task.modData.tags);
    }
  }, [visible, task]);

  // Grading options
  const gradingOptions = [
    { value: 0, label: t('grading.notRated') },
    { value: 1, label: t('grading.poor') },
    { value: 2, label: t('grading.fair') },
    { value: 3, label: t('grading.good') },
    { value: 4, label: t('grading.veryGood') },
    { value: 5, label: t('grading.excellent') },
  ];

  if (!task) return null;

  return (
    <Modal
      title={t('addMod.title', { id: task.id })}
      open={visible}
      onCancel={closeAddModUnit}
      width={700}
      footer={[
        <Button key="cancel" onClick={closeAddModUnit}>
          {t('common.cancel')}
        </Button>,
        <Button key="save" type="primary" onClick={handleSave} loading={saving}>
          {t('addMod.saveChanges')}
        </Button>,
      ]}
    >
      <Space orientation="vertical" className="add-mod-unit-container" size="large">
        {/* File Info Card */}
        <Card size="small" className="add-mod-unit-file-card">
          <Space orientation="vertical" className="add-mod-unit-file-info">
            <Space>
              {task.fileType === 'archive' ? <FileZipOutlined /> : <FolderOutlined />}
              <strong>{t('addMod.sourceFile')}</strong>
              <span className="add-mod-unit-file-name">{task.fileName}</span>
            </Space>
            <div className="add-mod-unit-file-path">
              {t('addMod.path')} {task.filePath}
            </div>
          </Space>
        </Card>

        {/* Preview Thumbnail */}
        {task.thumbnailUrl && (
          <div className="add-mod-unit-preview-container">
            <Image
              src={task.thumbnailUrl}
              alt="Mod Preview"
              className="add-mod-unit-preview-image"
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
            label={t('addMod.modName')}
            name="name"
            rules={[{ required: true, message: t('addMod.modNameRequired') }]}
            tooltip={t('addMod.modNameTooltip')}
          >
            <Input placeholder={t('addMod.modNamePlaceholder')} />
          </Form.Item>

          {/* Category - Required */}
          <Form.Item
            label={t('addMod.category')}
            name="category"
            rules={[{ required: true, message: t('addMod.categoryRequired') }]}
            tooltip={t('addMod.categoryTooltip')}
          >
            <Input placeholder={t('addMod.categoryPlaceholder')} />
          </Form.Item>

          {/* Description */}
          <Form.Item
            label={t('addMod.description')}
            name="description"
            tooltip={t('addMod.descriptionTooltip')}
          >
            <TextArea
              placeholder={t('addMod.descriptionPlaceholder')}
              rows={3}
              showCount
              maxLength={500}
            />
          </Form.Item>

          {/* Author */}
          <Form.Item
            label={t('addMod.author')}
            name="author"
            tooltip={t('addMod.authorTooltip')}
          >
            <Input placeholder={t('addMod.authorPlaceholder')} />
          </Form.Item>

          {/* Grading */}
          <Form.Item
            label={t('addMod.grading')}
            name="grading"
            tooltip={t('addMod.gradingTooltip')}
          >
            <Select placeholder={t('addMod.gradingPlaceholder')}>
              {gradingOptions.map(option => (
                <Option key={option.value} value={option.value}>
                  {option.label}
                </Option>
              ))}
            </Select>
          </Form.Item>

          {/* Tags */}
          <Form.Item
            label={t('addMod.tags')}
            tooltip={t('addMod.tagsTooltip')}
          >
            <Space orientation="vertical" className="add-mod-unit-tags-container">
              <Button
                icon={<TagsOutlined />}
                onClick={handleOpenTagSelector}
                block
              >
                {selectedTags.length > 0
                  ? t('addMod.selectedTags', { tags: selectedTags.join(', ') })
                  : t('addMod.selectTags')}
              </Button>
              {selectedTags.length > 0 && (
                <div className="add-mod-unit-tags-count">
                  {t('addMod.tagsCount', { count: selectedTags.length })}
                </div>
              )}
            </Space>
          </Form.Item>
        </Form>
      </Space>
    </Modal>
  );
};
