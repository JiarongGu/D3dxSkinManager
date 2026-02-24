import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Form, Input, Space } from 'antd';
import { ModInfo } from '../../../../shared/types/mod.types';
import { modService } from '../../services/modService';
import { useSlideInDialog } from '../../../../shared/hooks/useSlideInDialog';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';
import { BasicInfoSection } from './BasicInfoSection';
import { MetadataSection } from './MetadataSection';
import { TagsSection } from './TagsSection';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import './ModEditScreen.css';

/**
 * Slide-in screen for editing mod properties
 * Single mod editing with full form
 * Refactored into smaller section components for better maintainability
 *
 * NEW ARCHITECTURE:
 * - Subscribes to its own state (visible, modToEdit) from useModsStore
 * - Calls operations directly via useMods()
 * - No props needed from parent!
 */
export const ModEditScreen: React.FC = () => {
  // Subscribe to state this component needs
  const visible = useModsStore(s => s.editDialogVisible);
  const mod = useModsStore(s => s.modToEdit);
  const classificationTree = useModsStore(s => s.classificationTree);

  // Get operations
  const { updateMod, closeEditDialog } = useMods();
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [authors, setAuthors] = useState<string[]>([]);
  const [categoryOptions, setCategoryOptions] = useState<Array<{ id: string; name: string }>>([]);
  const [availableTags, setAvailableTags] = useState<string[]>([]);
  const { state: profileState } = useProfile();

  // Build category options from classification tree
  useEffect(() => {
    const flattenTree = (nodes: typeof classificationTree): Array<{ id: string; name: string }> => {
      const result: Array<{ id: string; name: string }> = [];
      for (const node of nodes) {
        result.push({ id: node.id, name: node.name });
        if (node.children) {
          result.push(...flattenTree(node.children));
        }
      }
      return result;
    };

    setCategoryOptions(flattenTree(classificationTree));
  }, [classificationTree]);

  // Load authors and tags for autocomplete
  useEffect(() => {
    const loadAutocompleteData = async () => {
      if (!profileState.selectedProfile?.id) {
        return;
      }
      try {
        const [authorsData, tagsData] = await Promise.all([
          modService.getAuthors(profileState.selectedProfile.id),
          modService.getTags(profileState.selectedProfile.id)
        ]);
        setAuthors(authorsData);
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
    if (!mod) return;

    try {
      const values = await form.validateFields();
      setSaving(true);

      const modData: Partial<ModInfo> = {
        ...values,
        tags: selectedTags,
      };

      // Call operation directly - it handles everything
      await updateMod(mod.sha, modData);

      form.resetFields();
      setSelectedTags([]);
      closeEditDialog();
    } catch (error) {
      console.error('Validation failed:', error);
      notification.error('Please check all required fields');
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    form.resetFields();
    setSelectedTags([]);
    closeEditDialog();
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
          categoryOptions={categoryOptions}
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
            <Input value={mod.sha} disabled className="mod-edit-screen-sha-input" />
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
