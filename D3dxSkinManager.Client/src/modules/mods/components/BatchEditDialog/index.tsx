import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Select, Space, Divider, Alert,  AutoComplete } from 'antd';
import { TagsOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../../shared/types/mod.types';
import { modService } from '../../services/modService';
import { MultiTagInput } from '../MultiTagInput';
import { TagManagementDialog } from '../TagManagementDialog';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';
import { FieldRow } from './FieldRow';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import { useTranslation } from 'react-i18next';
import './index.css';

const { TextArea } = Input;
const { Option } = Select;

/**
 * Dialog for batch editing multiple mods
 * Uses checkboxes to select which fields to update
 *
 * NEW ARCHITECTURE:
 * - Subscribes to its own state from useModsStore
 * - Calls batchUpdateMetadata operation directly
 * - No props needed!
 */
export const BatchEditDialog: React.FC = () => {
  // Subscribe to state this component needs
  const visible = useModsStore(s => s.batchEditDialogVisible);
  const selectedMods = useModsStore(s => s.selectedMods);

  // Get operations
  const { batchUpdateMetadata, closeBatchEditDialog } = useMods();
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [availableTags, setAvailableTags] = useState<string[]>([]);
  const [tagSelectorVisible, setTagSelectorVisible] = useState(false);
  const [authors, setAuthors] = useState<string[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [tagColorsMap, setTagColorsMap] = useState<Map<string, string>>(new Map());
  const { state: profileState } = useProfile();

  // Age rating options
  const ageRatingOptions = [
    { value: 'G', label: t('ageRating.general') },
    { value: 'P', label: t('ageRating.parentalGuidance') },
    { value: 'R', label: t('ageRating.restricted') },
    { value: 'X', label: t('ageRating.adultsOnly') },
  ];

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
        const [tagsFromTable, authorsData, categoriesData] = await Promise.all([
          modService.getAllTags(profileId), // Get tags from Tags table only
          modService.getAuthors(profileId),
          modService.getObjectNames(profileId),
        ]);
        setAvailableTags(tagsFromTable.map(t => t.name));
        setAuthors(authorsData);
        setCategories(categoriesData);

        // Initialize tag colors map with existing tags from database
        const colorsMap = new Map(tagsFromTable.map(t => [t.name, t.color]));
        setTagColorsMap(colorsMap);
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
        notification.warning(t('batchEdit.selectOneField'));
        return;
      }

      setSaving(true);

      // Get all existing tags from database
      const existingTags = await modService.getAllTags(profileState.selectedProfile!.id);
      const existingTagNames = new Set(existingTags.map(t => t.name));

      // Find new tags that need to be saved to Tags table
      const newTags = selectedTags.filter(tagName => !existingTagNames.has(tagName));

      // Save new tags with their pre-generated colors to Tags table
      if (newTags.length > 0 && profileState.selectedProfile?.id) {
        try {
          await Promise.all(
            newTags.map(tagName => {
              const color = tagColorsMap.get(tagName) || '#1890ff'; // Fallback color
              return modService.upsertTag(profileState.selectedProfile!.id, tagName, color);
            })
          );
        } catch (tagSaveError) {
          console.error('Failed to save new tags:', tagSaveError);
          // Continue with mod save even if tag save fails
        }
      }

      // Call operation directly - it handles everything
      await batchUpdateMetadata(
        selectedMods.map(m => m.sha),
        modData,
        fieldMask
      );

      handleReset();
      closeBatchEditDialog();
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
    setTagColorsMap(new Map());
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
    closeBatchEditDialog();
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
    <Modal
      title={t('batchEditMods.title', { count: selectedMods.length })}
      open={visible}
      onCancel={handleCancel}
      width={700}
      footer={[
        <CompactButton key="reset" onClick={handleReset}>
          {t('batchEdit.reset')}
        </CompactButton>,
        <CompactButton key="cancel" onClick={handleCancel}>
          {t('common.cancel')}
        </CompactButton>,
        <CompactButton key="save" type="primary" onClick={handleSave} loading={saving}>
          {t('batchEditMods.applyTo', { count: selectedMods.length })}
        </CompactButton>,
      ]}
    >
      <Space orientation="vertical" className="batch-edit-dialog-container" size="large">
        <Alert
          message={t('batchEditMods.alertTitle')}
          description={t('batchEditMods.alertDescription', { count: selectedMods.length })}
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
              label={t('addMod.description')}
              name="description"
              className="batch-edit-dialog-field"
            >
              <TextArea
                placeholder={t('batchEditMods.descriptionPlaceholder')}
                rows={3}
                disabled={!enabledFields.description}
                showCount
                maxLength={500}
              />
            </Form.Item>
          </FieldRow>

          <Divider className="batch-edit-dialog-divider" />

          {/* Author */}
          <FieldRow
            checked={enabledFields.author}
            onToggle={() => handleFieldToggle('author')}
          >
            <Form.Item
              label={t('addMod.author')}
              name="author"
              className="batch-edit-dialog-field"
            >
              <AutoComplete
                placeholder={t('batchEditMods.authorPlaceholder')}
                disabled={!enabledFields.author}
                options={authors.map(author => ({ value: author }))}
                filterOption={(inputValue, option) =>
                  option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
                }
              />
            </Form.Item>
          </FieldRow>

          <Divider className="batch-edit-dialog-divider" />

          {/* Category */}
          <FieldRow
            checked={enabledFields.category}
            onToggle={() => handleFieldToggle('category')}
          >
            <Form.Item
              label={t('addMod.category')}
              name="category"
              className="batch-edit-dialog-field"
            >
              <AutoComplete
                placeholder={t('batchEditMods.categoryPlaceholder')}
                disabled={!enabledFields.category}
                options={categories.map(cat => ({ value: cat }))}
                filterOption={(inputValue, option) =>
                  option!.value.toUpperCase().indexOf(inputValue.toUpperCase()) !== -1
                }
              />
            </Form.Item>
          </FieldRow>

          <Divider className="batch-edit-dialog-divider" />

          {/* Age Rating */}
          <FieldRow
            checked={enabledFields.grading}
            onToggle={() => handleFieldToggle('grading')}
          >
            <Form.Item
              label={t('addMod.grading')}
              name="grading"
              className="batch-edit-dialog-field"
            >
              <Select placeholder={t('batchEditMods.ageRatingPlaceholder')} disabled={!enabledFields.grading}>
                {ageRatingOptions.map(option => (
                  <Option key={option.value} value={option.value}>
                    {option.label}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </FieldRow>

          <Divider className="batch-edit-dialog-divider" />

          {/* Tags */}
          <FieldRow
            checked={enabledFields.tags}
            onToggle={() => handleFieldToggle('tags')}
          >
            <Form.Item
              label={t('addMod.tags')}
              className="batch-edit-dialog-field"
            >
              <Space.Compact style={{ width: '100%' }}>
                <MultiTagInput
                  value={selectedTags}
                  onChange={setSelectedTags}
                  availableTags={availableTags}
                  placeholder={t('batchEditMods.tagsPlaceholder')}
                  tagColorsMap={tagColorsMap}
                  setTagColorsMap={setTagColorsMap}
                />
                <CompactButton
                  icon={<TagsOutlined />}
                  onClick={handleOpenTagSelector}
                  title="Open tag selector"
                />
              </Space.Compact>
            </Form.Item>
          </FieldRow>

        </Form>

        {/* Summary */}
        <Alert
          message={
            Object.values(enabledFields).filter(Boolean).length > 0
              ? t('batchEditMods.summaryFields', { count: Object.values(enabledFields).filter(Boolean).length })
              : t('batchEditMods.summaryNoFields')
          }
          type={Object.values(enabledFields).filter(Boolean).length > 0 ? 'success' : 'warning'}
        />
      </Space>

      {/* Tag Management Dialog */}
      <TagManagementDialog
        visible={tagSelectorVisible}
        selectedTags={selectedTags}
        onConfirm={handleTagSelectorConfirm}
        onCancel={handleTagSelectorCancel}
        onTagDeleted={handleTagDeleted}
        title={t('batchEdit.manageTags')}
        tagColorsMap={tagColorsMap}
        setTagColorsMap={setTagColorsMap}
      />
    </Modal>
  );
};
