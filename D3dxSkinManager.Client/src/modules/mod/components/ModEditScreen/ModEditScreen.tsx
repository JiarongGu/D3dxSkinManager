import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Form, Input, Space } from 'antd';
import { ModInfo } from '../../../../shared/types/mod.types';
import { modService } from '../../../../shared/services/ipc';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';
import { BasicInfoSection } from './BasicInfoSection';
import { MetadataSection } from './MetadataSection';
import { TagsSection } from './TagsSection';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import logger from '../../../../shared/utils/logger';
import './ModEditScreen.css';

/**
 * Form content component - contains all state and logic for editing a mod
 */
const ModEditFormContent: React.FC<{ mod?: ModInfo }> = ({ mod }) => {
  // Subscribe to state from store
  const visible = useModsStore(s => s.editDialogVisible);
  const CategoryTree = useModsStore(s => s.categoryTree);
  const { state: profileState } = useProfile();
  const { updateMod, closeEditDialog } = useMods();

  // Local state
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [authors, setAuthors] = useState<string[]>([]);
  const [categoryOptions, setCategoryOptions] = useState<Array<{ id: string; name: string }>>([]);
  const [availableTags, setAvailableTags] = useState<string[]>([]);
  // Map to store all tag colors (both from database and newly generated)
  const [tagColorsMap, setTagColorsMap] = useState<Map<string, string>>(new Map());

  // Wrapper for setSelectedTags
  const handleTagsChange = (newTags: string[]) => {
    setSelectedTags(newTags);
  };

  // Refresh available tags when a tag is deleted
  const handleTagDeleted = async () => {
    if (!profileState.selectedProfile?.id) return;

    try {
      const tagsFromTable = await modService.getAllTags(profileState.selectedProfile.id);
      setAvailableTags(tagsFromTable.map(t => t.name));
    } catch (error: unknown) {
          }
  };

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
    const loadAutocompleteData = async () => {
      if (!profileState.selectedProfile?.id) {
        return;
      }
      try {
        const [authorsData, tagsFromTable] = await Promise.all([
          modService.getAuthors(profileState.selectedProfile.id),
          modService.getAllTags(profileState.selectedProfile.id) // Get tags from Tags table only
        ]);
        setAuthors(authorsData);
        setAvailableTags(tagsFromTable.map(t => t.name));

        // Initialize tag colors map with existing tags from database
        const colorsMap = new Map(tagsFromTable.map(t => [t.name, t.color]));
        setTagColorsMap(colorsMap);
      } catch (error: unknown) {
              }
    };

    if (visible) {
      loadAutocompleteData();
    }
  }, [visible, profileState.selectedProfile?.id]);

  // Initialize form when mod is available
  useEffect(() => {
    if (mod) {
      form.setFieldsValue({
        name: mod.name,
        description: mod.description || '',
        grading: mod.grading || '',
        author: mod.author || '',
        category: mod.category || '',
        disablePreview: mod.disablePreview ?? false,
      });
      setSelectedTags(mod.tags || []);
    }
  }, [mod, form]);

  const handleSave = async () => {
    if (!mod || !profileState.selectedProfile?.id) return;

    try {
      const values = await form.validateFields();
      setSaving(true);

      // Build modData, only including category if it actually changed
      const modData: Partial<ModInfo> = {
        name: values.name,
        description: values.description,
        grading: values.grading,
        author: values.author,
        disablePreview: values.disablePreview,
        tags: selectedTags,
      };

      // Only include category if it changed to avoid unloading the mod
      if (values.category !== mod.category) {
        modData.category = values.category;
      }

      // Get all existing tags from database
      const existingTags = await modService.getAllTags(profileState.selectedProfile.id);
      const existingTagNames = new Set(existingTags.map(t => t.name));

      // Find new tags that need to be saved to Tags table
      const newTags = selectedTags.filter(tagName => !existingTagNames.has(tagName));

      // Save new tags with their pre-generated colors to Tags table
      if (newTags.length > 0) {
        try {
          await Promise.all(
            newTags.map(tagName => {
              const color = tagColorsMap.get(tagName) || '#1890ff'; // Fallback color
              return modService.upsertTag(profileState.selectedProfile!.id, tagName, color);
            })
          );
        } catch (tagSaveError: unknown) {
          // Continue with mod save even if tag save fails
          logger.warn('Failed to save some tags:', tagSaveError);
        }
      }

      // Save mod metadata
      await updateMod(mod.sha, modData);

      form.resetFields();
      setSelectedTags([]);
      setTagColorsMap(new Map());
      closeEditDialog();
    } catch (error: unknown) {
            notification.error('Please check all required fields');
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    form.resetFields();
    setSelectedTags([]);
    setTagColorsMap(new Map());
    closeEditDialog();
  };

  return (
    <>
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
          hasPreviewFolder={mod?.hasPreviewFolder}
        />

        {/* Tags Section */}
        <TagsSection
          tags={selectedTags}
          availableTags={availableTags}
          onTagsChange={handleTagsChange}
          onTagDeleted={handleTagDeleted}
          tagColorsMap={tagColorsMap}
          setTagColorsMap={setTagColorsMap}
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
    </>
  );
};

/**
 * Slide-in screen for editing mod properties
 * Lightweight wrapper that manages the slide-in dialog
 */
export const ModEditScreen: React.FC = () => {
  const visible = useModsStore(s => s.editDialogVisible);
  const mod = useModsStore(s => s.modToEdit);
  const { closeEditDialog } = useMods();

  useSlideInScreen({
    visible,
    title: mod ? `Edit Mod: ${mod.name}` : 'Edit Mod',
    content: <ModEditFormContent mod={mod} />,
    width: '55%',
    onClose: closeEditDialog,
  });

  return null;
};
