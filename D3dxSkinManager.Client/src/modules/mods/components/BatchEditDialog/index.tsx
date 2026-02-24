import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Select, Space, Divider, Alert,  AutoComplete } from 'antd';
import { ModInfo } from '../../../../shared/types/mod.types';
import { modService } from '../../services/modService';
import { MultiTagInput } from '../../../../shared/components/common/MultiTagInput';
import { ModTagSelectorDialog } from '../ModEditScreen/ModTagSelectorDialog';
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
        notification.warning(t('batchEdit.selectOneField'));
        return;
      }

      setSaving(true);

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
              <MultiTagInput
                value={selectedTags}
                onChange={setSelectedTags}
                availableTags={availableTags}
                onOpenTagSelector={handleOpenTagSelector}
                placeholder={t('batchEditMods.tagsPlaceholder')}
              />
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
