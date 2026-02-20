import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Select, Space, Divider, Alert,  AutoComplete } from 'antd';
import { ModInfo } from '../../../../shared/types/mod.types';
import { modService } from '../../services/modService';
import { MultiTagInput } from '../../../../shared/components/common/MultiTagInput';
import { ModTagSelectorDialog } from '../ModEditDialog/ModTagSelectorDialog';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';
import { FieldRow } from './FieldRow';
import { useProfile } from '../../../../shared/context/ProfileContext';

const { TextArea } = Input;
const { Option } = Select;

export interface BatchEditDialogProps {
  visible: boolean;
  selectedMods: ModInfo[];
  onSave: (modData: Partial<ModInfo>, fieldMask: string[]) => Promise<void>;
  onCancel: () => void;
}

// Age rating options
const ageRatingOptions = [
  { value: 'G', label: 'G - General' },
  { value: 'P', label: 'P - Parental Guidance' },
  { value: 'R', label: 'R - Restricted' },
  { value: 'X', label: 'X - Adults Only' },
];

/**
 * Dialog for batch editing multiple mods
 * Uses checkboxes to select which fields to update
 * Refactored to use CompactButton and modern components
 */
export const BatchEditDialog: React.FC<BatchEditDialogProps> = ({
  visible,
  selectedMods,
  onSave,
  onCancel,
}) => {
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [availableTags, setAvailableTags] = useState<string[]>([]);
  const [tagSelectorVisible, setTagSelectorVisible] = useState(false);
  const [authors, setAuthors] = useState<string[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const { state: profileState } = useProfile();

  // Field enable/disable state
  const [enabledFields, setEnabledFields] = useState({
    description: false,
    grading: false,
    author: false,
    category: false,
    tags: false,
  });

  // Load autocomplete data when dialog becomes visible
  useEffect(() => {
    const loadData = async () => {
      if (!profileState.selectedProfile?.id) {
        return;
      }
      const profileId = profileState.selectedProfile.id;
      try {
        const [tagsData, authorsData, categoriesData] = await Promise.all([
          modService.getTags(profileId),
          modService.getAuthors(profileId),
          modService.getObjectNames(profileId),
        ]);
        setAvailableTags(tagsData);
        setAuthors(authorsData);
        setCategories(categoriesData);
      } catch (error) {
        console.error('Failed to load autocomplete data:', error);
      }
    };

    if (visible) {
      loadData();
    }
  }, [visible, profileState.selectedProfile?.id]);

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
        notification.warning('Please select at least one field to update');
        return;
      }

      setSaving(true);
      await onSave(modData, fieldMask);

      notification.success(`${selectedMods.length} mod(s) updated successfully`);
      handleReset();
      onCancel();
    } catch (error) {
      console.error('Validation failed:', error);
      notification.error('Please check all required fields');
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
    setTagSelectorVisible(true);
  };

  const handleTagSelectorConfirm = (tags: string[]) => {
    setSelectedTags(tags);
    setTagSelectorVisible(false);
  };

  const handleTagSelectorCancel = () => {
    setTagSelectorVisible(false);
  };

  return (
    <Modal
      title={`Batch Edit ${selectedMods.length} Mod(s)`}
      open={visible}
      onCancel={handleCancel}
      width={700}
      footer={[
        <CompactButton key="reset" onClick={handleReset}>
          Reset
        </CompactButton>,
        <CompactButton key="cancel" onClick={handleCancel}>
          Cancel
        </CompactButton>,
        <CompactButton key="save" type="primary" onClick={handleSave} loading={saving}>
          Apply to {selectedMods.length} Mod(s)
        </CompactButton>,
      ]}
    >
      <Space orientation="vertical" style={{ width: '100%' }} size="large">
        <Alert
          message="Batch Edit Mode"
          description={`Check the fields you want to update for all ${selectedMods.length} selected mod(s). Unchecked fields will remain unchanged.`}
          type="info"
          showIcon
        />

        <Form
          form={form}
          layout="vertical"
          autoComplete="off"
        >
          {/* Description */}
          <FieldRow
            checked={enabledFields.description}
            onToggle={() => handleFieldToggle('description')}
          >
            <Form.Item
              label="Description"
              name="description"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <TextArea
                placeholder="Enter description for all selected mods..."
                rows={3}
                disabled={!enabledFields.description}
                showCount
                maxLength={500}
              />
            </Form.Item>
          </FieldRow>

          <Divider style={{ margin: '12px 0' }} />

          {/* Author */}
          <FieldRow
            checked={enabledFields.author}
            onToggle={() => handleFieldToggle('author')}
          >
            <Form.Item
              label="Author"
              name="author"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <AutoComplete
                placeholder="Set author for all selected mods"
                disabled={!enabledFields.author}
                options={authors.map(author => ({ value: author }))}
                filterOption={(inputValue, option) =>
                  option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
                }
              />
            </Form.Item>
          </FieldRow>

          <Divider style={{ margin: '12px 0' }} />

          {/* Category */}
          <FieldRow
            checked={enabledFields.category}
            onToggle={() => handleFieldToggle('category')}
          >
            <Form.Item
              label="Category"
              name="category"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <AutoComplete
                placeholder="Set category for all selected mods"
                disabled={!enabledFields.category}
                options={categories.map(cat => ({ value: cat }))}
                filterOption={(inputValue, option) =>
                  option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
                }
              />
            </Form.Item>
          </FieldRow>

          <Divider style={{ margin: '12px 0' }} />

          {/* Age Rating */}
          <FieldRow
            checked={enabledFields.grading}
            onToggle={() => handleFieldToggle('grading')}
          >
            <Form.Item
              label="Age Rating"
              name="grading"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <Select placeholder="Select age rating" disabled={!enabledFields.grading}>
                {ageRatingOptions.map(option => (
                  <Option key={option.value} value={option.value}>
                    {option.label}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </FieldRow>

          <Divider style={{ margin: '12px 0' }} />

          {/* Tags */}
          <FieldRow
            checked={enabledFields.tags}
            onToggle={() => handleFieldToggle('tags')}
          >
            <Form.Item
              label="Tags"
              style={{ flex: 1, marginBottom: 0 }}
            >
              <MultiTagInput
                value={selectedTags}
                onChange={setSelectedTags}
                availableTags={availableTags}
                onOpenTagSelector={handleOpenTagSelector}
                placeholder="Type to add tags..."
              />
            </Form.Item>
          </FieldRow>

        </Form>

        {/* Summary */}
        <Alert
          message={
            Object.values(enabledFields).filter(Boolean).length > 0
              ? `${Object.values(enabledFields).filter(Boolean).length} field(s) will be updated`
              : 'No fields selected for update'
          }
          type={Object.values(enabledFields).filter(Boolean).length > 0 ? 'success' : 'warning'}
        />
      </Space>

      {/* Tag Selector Dialog */}
      <ModTagSelectorDialog
        visible={tagSelectorVisible}
        availableTags={availableTags}
        selectedTags={selectedTags}
        onConfirm={handleTagSelectorConfirm}
        onCancel={handleTagSelectorCancel}
      />
    </Modal>
  );
};
