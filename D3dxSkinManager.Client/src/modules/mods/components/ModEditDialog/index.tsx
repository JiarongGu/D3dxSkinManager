import React, { useState, useEffect } from 'react';
import { Form, Input, Space, message } from 'antd';
import { ModInfo } from '../../../../shared/types/mod.types';
import { modService } from '../../services/modService';
import { useSlideInDialog } from '../../../../shared/hooks/useSlideInDialog';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';
import { BasicInfoSection } from './BasicInfoSection';
import { MetadataSection } from './MetadataSection';
import { TagsSection } from './TagsSection';
import { useProfile } from '../../../../shared/context/ProfileContext';

export interface ModEditDialogProps {
  visible: boolean;
  mod: ModInfo | null;
  onSave: (modData: Partial<ModInfo>) => Promise<void>;
  onCancel: () => void;
}

/**
 * Dialog for editing mod properties
 * Single mod editing with full form
 * Refactored into smaller section components for better maintainability
 */
export const ModEditDialog: React.FC<ModEditDialogProps> = ({
  visible,
  mod,
  onSave,
  onCancel,
}) => {
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [authors, setAuthors] = useState<string[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [availableTags, setAvailableTags] = useState<string[]>([]);
  const { state: profileState } = useProfile();

  // Load authors, categories, and tags for autocomplete
  useEffect(() => {
    const loadAutocompleteData = async () => {
      if (!profileState.selectedProfile?.id) {
        return;
      }
      try {
        const [authorsData, categoriesData, tagsData] = await Promise.all([
          modService.getAuthors(profileState.selectedProfile.id),
          modService.getObjectNames(profileState.selectedProfile.id),
          modService.getTags(profileState.selectedProfile.id)
        ]);
        setAuthors(authorsData);
        setCategories(categoriesData);
        setAvailableTags(tagsData);
      } catch (error) {
        console.error('Failed to load autocomplete data:', error);
      }
    };

    if (visible) {
      loadAutocompleteData();
    }
  }, [visible, profileState.selectedProfile?.id]);

  // Initialize form when mod changes
  useEffect(() => {
    if (mod && visible) {
      form.setFieldsValue({
        name: mod.name,
        description: mod.description || '',
        grading: mod.grading || '',
        author: mod.author || '',
        category: mod.category || '',
      });
      setSelectedTags(mod.tags || []);
    }
  }, [mod, visible, form]);

  const handleSave = async () => {
    try {
      const values = await form.validateFields();

      setSaving(true);

      const modData: Partial<ModInfo> = {
        ...values,
        tags: selectedTags,
      };

      await onSave(modData);

      message.success('Mod updated successfully');
      form.resetFields();
      setSelectedTags([]);
      onCancel();
    } catch (error) {
      console.error('Validation failed:', error);
      message.error('Please check all required fields');
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    form.resetFields();
    setSelectedTags([]);
    onCancel();
  };

  // Render form content
  const formContent = (
    <div>
      <Form
        form={form}
        layout="vertical"
        autoComplete="off"
      >
        {/* Basic Information Section */}
        <BasicInfoSection />

        {/* Metadata Section */}
        <MetadataSection
          authors={authors}
          categories={categories}
        />

        {/* Tags Section */}
        <TagsSection
          tags={selectedTags}
          availableTags={availableTags}
          onTagsChange={setSelectedTags}
        />

        {/* Read-only SHA display */}
        {mod && (
          <Form.Item label="SHA Hash" tooltip="Unique identifier (read-only)">
            <Input value={mod.sha} disabled style={{ fontFamily: 'monospace' }} />
          </Form.Item>
        )}
      </Form>

      {/* Footer with action buttons */}
      <div className="slide-in-screen-footer">
        <Space>
          <CompactButton onClick={handleCancel}>
            Cancel
          </CompactButton>
          <CompactButton type="primary" onClick={handleSave} loading={saving}>
            Save Changes
          </CompactButton>
        </Space>
      </div>
    </div>
  );

  // Use slide-in screen
  useSlideInDialog({
    visible,
    title: mod ? `Edit Mod: ${mod.name}` : 'Edit Mod',
    content: formContent,
    width: '55%',
    onClose: handleCancel,
  });

  return null;
};
