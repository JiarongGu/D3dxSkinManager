import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, AutoComplete, Select, Space } from 'antd';
import { TagsOutlined } from '@ant-design/icons';
import { ImportTask } from '../../types/importTask.types';
import { ModInfo } from '../../../../shared/types/mod.types';
import { modService } from '../../services/modService';
import { MultiTagInput } from '../MultiTagInput';
import { TagManagementDialog } from '../TagManagementDialog';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useModsStore } from '../../store/modsStore';
import { useTranslation } from 'react-i18next';

const { TextArea } = Input;
const { Option } = Select;

interface TaskEditorProps {
  task: ImportTask;
  onSave: (modData: Partial<ModInfo>) => void;
  onCancel: () => void;
}

/**
 * Task Editor - Modal for editing import task metadata
 *
 * Allows user to edit mod metadata before import:
 * - Name, description, author, category, grading, tags
 * - Validates required fields (name, category)
 * - Supports tag management
 */
export const TaskEditor: React.FC<TaskEditorProps> = ({ task, onSave, onCancel }) => {
  const { t } = useTranslation();
  const { state: profileState } = useProfile();
  const [form] = Form.useForm();

  // Subscribe to Category tree for category options
  const CategoryTree = useModsStore(s => s.CategoryTree);

  // Local state
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [authors, setAuthors] = useState<string[]>([]);
  const [categoryOptions, setCategoryOptions] = useState<Array<{ id: string; name: string }>>([]);
  const [availableTags, setAvailableTags] = useState<string[]>([]);
  const [tagColorsMap, setTagColorsMap] = useState<Map<string, string>>(new Map());
  const [tagSelectorVisible, setTagSelectorVisible] = useState(false);

  // Age rating options
  const ageRatingOptions = [
    { value: 'G', label: t('ageRating.general') },
    { value: 'P', label: t('ageRating.parentalGuidance') },
    { value: 'R', label: t('ageRating.restricted') },
    { value: 'X', label: t('ageRating.adultsOnly') },
  ];

  // Build category options from Category tree
  useEffect(() => {
    const flattenTree = (nodes: typeof CategoryTree): Array<{ id: string; name: string }> => {
      const result: Array<{ id: string; name: string }> = [];
      for (const node of nodes) {
        result.push({ id: node.id, name: node.name });
        if (node.children) {
          result.push(...flattenTree(node.children));
        }
      }
      return result;
    };

    setCategoryOptions(flattenTree(CategoryTree));
  }, [CategoryTree]);

  // Load authors and tags for autocomplete
  useEffect(() => {
    const loadData = async () => {
      if (!profileState.selectedProfile?.id) return;

      try {
        const [authorsData, tagsFromTable] = await Promise.all([
          modService.getAuthors(profileState.selectedProfile.id),
          modService.getAllTags(profileState.selectedProfile.id),
        ]);
        setAuthors(authorsData);
        setAvailableTags(tagsFromTable.map(t => t.name));

        const colorsMap = new Map(tagsFromTable.map(t => [t.name, t.color]));
        setTagColorsMap(colorsMap);
      } catch (error) {
        console.error('Failed to load autocomplete data:', error);
      }
    };

    loadData();
  }, [profileState.selectedProfile?.id]);

  // Initialize form with task data
  useEffect(() => {
    form.setFieldsValue({
      name: task.modData.name || task.fileName.replace(/\.(zip|rar|7z|tar|gz)$/i, ''),
      description: task.modData.description || '',
      author: task.modData.author || '',
      category: task.modData.category || '',
      grading: task.modData.grading || '',
    });
    setSelectedTags(task.modData.tags || []);
  }, [task, form]);

  const handleSave = async () => {
    try {
      const values = await form.validateFields();

      const modData: Partial<ModInfo> = {
        ...values,
        tags: selectedTags,
      };

      onSave(modData);
    } catch (error) {
      console.error('Validation failed:', error);
    }
  };

  const handleTagDeleted = async () => {
    if (!profileState.selectedProfile?.id) return;

    try {
      const tagsFromTable = await modService.getAllTags(profileState.selectedProfile.id);
      setAvailableTags(tagsFromTable.map(t => t.name));
    } catch (error) {
      console.error('Failed to refresh tags:', error);
    }
  };

  return (
    <>
      <Modal
        title={t('importQueue.editTaskTitle', { fileName: task.fileName })}
        open={true}
        onOk={handleSave}
        onCancel={onCancel}
        width={600}
        okText={t('common.save')}
        cancelText={t('common.cancel')}
      >
        <Form
          form={form}
          layout="vertical"
          autoComplete="off"
        >
          {/* Name */}
          <Form.Item
            label={t('addMod.name')}
            name="name"
            rules={[{ required: true, message: t('addMod.nameRequired') }]}
          >
            <Input placeholder={t('addMod.namePlaceholder')} />
          </Form.Item>

          {/* Category */}
          <Form.Item
            label={t('addMod.category')}
            name="category"
            rules={[{ required: true, message: t('addMod.categoryRequired') }]}
          >
            <AutoComplete
              placeholder={t('addMod.categoryPlaceholder')}
              options={categoryOptions.map(cat => ({ value: cat.name }))}
              filterOption={(inputValue, option) =>
                option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
              }
            />
          </Form.Item>

          {/* Author */}
          <Form.Item
            label={t('addMod.author')}
            name="author"
          >
            <AutoComplete
              placeholder={t('addMod.authorPlaceholder')}
              options={authors.map(author => ({ value: author }))}
              filterOption={(inputValue, option) =>
                option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
              }
            />
          </Form.Item>

          {/* Description */}
          <Form.Item
            label={t('addMod.description')}
            name="description"
          >
            <TextArea
              placeholder={t('addMod.descriptionPlaceholder')}
              rows={3}
              showCount
              maxLength={500}
            />
          </Form.Item>

          {/* Age Rating */}
          <Form.Item
            label={t('addMod.grading')}
            name="grading"
          >
            <Select placeholder={t('addMod.gradingPlaceholder')}>
              {ageRatingOptions.map(option => (
                <Option key={option.value} value={option.value}>
                  {option.label}
                </Option>
              ))}
            </Select>
          </Form.Item>

          {/* Tags */}
          <Form.Item label={t('addMod.tags')}>
            <Space.Compact style={{ width: '100%' }}>
              <MultiTagInput
                value={selectedTags}
                onChange={setSelectedTags}
                availableTags={availableTags}
                placeholder={t('addMod.tagsPlaceholder')}
                tagColorsMap={tagColorsMap}
                setTagColorsMap={setTagColorsMap}
              />
              <CompactButton
                icon={<TagsOutlined />}
                onClick={() => setTagSelectorVisible(true)}
                title={t('addMod.manageTags')}
              />
            </Space.Compact>
          </Form.Item>

          {/* Source File (read-only) */}
          <Form.Item label={t('importQueue.sourceFile')}>
            <Input value={task.filePath} disabled />
          </Form.Item>
        </Form>
      </Modal>

      {/* Tag Management Dialog */}
      <TagManagementDialog
        visible={tagSelectorVisible}
        selectedTags={selectedTags}
        onConfirm={(tags) => {
          setSelectedTags(tags);
          setTagSelectorVisible(false);
        }}
        onCancel={() => setTagSelectorVisible(false)}
        onTagDeleted={handleTagDeleted}
        title={t('addMod.manageTags')}
        tagColorsMap={tagColorsMap}
        setTagColorsMap={setTagColorsMap}
      />
    </>
  );
};
