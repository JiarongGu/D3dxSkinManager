import React, { useState, useEffect, useMemo, useCallback } from "react";
import classNames from 'classnames';
import { Tag as AntTag, Empty, Space, ColorPicker } from "antd";
import {
  SearchOutlined,
  DeleteOutlined,
  PlusOutlined,
} from "@ant-design/icons";
import type { Color } from "antd/es/color-picker";
import { useTranslation } from "react-i18next";
import { debounce } from "lodash-es";
import { FormDialog } from "../../../../shared/components/dialogs/FormDialog";
import { ConfirmDialog } from "../../../../shared/components/dialogs/ConfirmDialog";
import { CompactButton, CompactInput } from "../../../../shared/components/compact";
import { Tag } from "../../../../shared/types/mod.types";
import { modService } from "../../../../shared/services/ipc";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { handleError } from "../../../../shared/utils/errorHandler";
import "./TagManagementDialog.css";

export interface TagManagementDialogProps {
  visible: boolean;
  selectedTags: string[];
  onConfirm: (tags: string[]) => void;
  onCancel: () => void;
  onTagDeleted?: () => void; // Callback when a tag is deleted
  title?: string;
  tagColorsMap?: Map<string, string>;
  setTagColorsMap?: (map: Map<string, string>) => void;
}

/**
 * Enhanced tag management dialog
 * Features:
 * - Select/deselect tags for mod
 * - View tags as colored chips
 * - Delete tags from master list (doesn't affect mod tags)
 * - Customize tag colors with real-time updates
 * - Show newly created tags separately
 * - Search/filter tags
 */
export const TagManagementDialog: React.FC<TagManagementDialogProps> = ({
  visible,
  selectedTags,
  onConfirm,
  onCancel,
  onTagDeleted,
  title = "Manage Tags",
  tagColorsMap,
  setTagColorsMap,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [searchTerm, setSearchTerm] = useState("");
  const [localSelectedTags, setLocalSelectedTags] =
    useState<string[]>(selectedTags);
  const [allTags, setAllTags] = useState<Tag[]>([]);
  const [existingTagNames, setExistingTagNames] = useState<Set<string>>(
    new Set(),
  );
  const [tagToDelete, setTagToDelete] = useState<string | null>(null);

  // Load all tags when dialog opens
  useEffect(() => {
    if (visible && selectedProfileId) {
      void loadTags();
    }
  }, [visible, selectedProfileId]);

  // Reset local state when dialog opens or selectedTags changes
  useEffect(() => {
    if (visible) {
      setLocalSelectedTags(selectedTags);
      setSearchTerm("");
    }
  }, [visible, selectedTags]);

  const loadTags = async () => {
    if (!selectedProfileId) return;

    try {
      // Load tags from Tags table
      const tagsFromTable = await modService.getAllTags(selectedProfileId);

      // Track existing tag names from Tags table
      const existingNames = new Set(tagsFromTable.map((t) => t.name));
      setExistingTagNames(existingNames);

      // Find tags that the CURRENT mod has but aren't in Tags table yet
      const missingTags = selectedTags.filter(
        (tag) => !existingNames.has(tag)
      );

      // Create Tag objects for missing tags with pre-generated color from tagColorsMap
      const missingTagObjects = missingTags.map((name) => ({
        name,
        color: tagColorsMap?.get(name) || "#1890ff", // Use pre-generated color or fallback
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      }));

      // Combine tags from Tags table with tags from current mod only
      setAllTags([...tagsFromTable, ...missingTagObjects]);
    } catch (error: unknown) {
      handleError(error);
    }
  };

  // Separate tags into existing and newly created
  const { existingTags, newlyCreatedTags } = useMemo(() => {
    const existing: Tag[] = [];
    const newlyCreated: Tag[] = [];

    allTags.forEach((tag) => {
      if (existingTagNames.has(tag.name)) {
        existing.push(tag);
      } else {
        newlyCreated.push(tag);
      }
    });

    return { existingTags: existing, newlyCreatedTags: newlyCreated };
  }, [allTags, existingTagNames]);

  // Filter tags based on search term
  const filteredExistingTags = useMemo(() => {
    if (!searchTerm) return existingTags;
    const lowerSearch = searchTerm.toLowerCase();
    return existingTags.filter((tag) =>
      tag.name.toLowerCase().includes(lowerSearch),
    );
  }, [existingTags, searchTerm]);

  const filteredNewTags = useMemo(() => {
    if (!searchTerm) return newlyCreatedTags;
    const lowerSearch = searchTerm.toLowerCase();
    return newlyCreatedTags.filter((tag) =>
      tag.name.toLowerCase().includes(lowerSearch),
    );
  }, [newlyCreatedTags, searchTerm]);

  const handleToggleTag = (tagName: string) => {
    setLocalSelectedTags((prev) =>
      prev.includes(tagName)
        ? prev.filter((t) => t !== tagName)
        : [...prev, tagName],
    );
  };

  const handleConfirm = () => {
    onConfirm(localSelectedTags);
  };

  const handleSelectAll = () => {
    const allVisibleTags = [...filteredExistingTags, ...filteredNewTags].map(
      (t) => t.name,
    );
    setLocalSelectedTags((prev) => {
      const combined = new Set([...prev, ...allVisibleTags]);
      return Array.from(combined);
    });
  };

  const handleDeselectAll = () => {
    // Symmetric with Select All: only deselect the currently VISIBLE (filtered) tags, keeping
    // selections that are filtered out of view.
    const visible = new Set(
      [...filteredExistingTags, ...filteredNewTags].map((t) => t.name),
    );
    setLocalSelectedTags((prev) => prev.filter((name) => !visible.has(name)));
  };

  const handleDeleteTag = async () => {
    if (!selectedProfileId || !tagToDelete) return;

    try {
      await modService.deleteTag(selectedProfileId, tagToDelete);

      // Reload tags to get updated list
      // This ensures deleted tags don't reappear as "new" tags
      await loadTags();

      // Also remove from selected tags if it was selected
      setLocalSelectedTags((prev) => prev.filter((t) => t !== tagToDelete));

      // Notify parent component to refresh available tags
      onTagDeleted?.();

      setTagToDelete(null);
    } catch (error: unknown) {
      handleError(error);
      setTagToDelete(null);
    }
  };

  // Debounced function to save tag color to backend
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const debouncedSaveTagColor = useCallback(
    debounce(async (profileId: string, tagName: string, hexColor: string) => {
      try {
        await modService.upsertTag(profileId, tagName, hexColor);

        // Move tag from "new" to "existing" by adding to existingTagNames
        setExistingTagNames((prev) => new Set([...prev, tagName]));
      } catch (error: unknown) {
        handleError(error);
      }
    }, 500),
    [],
  );

  const handleColorChange = (tagName: string, color: Color) => {
    if (!selectedProfileId) return;

    const hexColor = color.toHexString();

    // Update local state immediately for real-time feedback
    setAllTags((prev) =>
      prev.map((t) =>
        t.name === tagName
          ? { ...t, color: hexColor, updatedAt: new Date().toISOString() }
          : t,
      ),
    );

    // Update shared tagColorsMap so MultiTagInput sees the change
    if (setTagColorsMap && tagColorsMap) {
      const updatedMap = new Map(tagColorsMap);
      updatedMap.set(tagName, hexColor);
      setTagColorsMap(updatedMap);
    }

    // Only save to database if tag exists in Tags table (not new)
    if (existingTagNames.has(tagName)) {
      // Debounced save to backend (500ms delay)
      void debouncedSaveTagColor(selectedProfileId, tagName, hexColor);
    }
  };

  // Cleanup debounced function on unmount
  useEffect(() => {
    return () => {
      debouncedSaveTagColor.cancel();
    };
  }, [debouncedSaveTagColor]);

  const renderTagItem = (tag: Tag, isNew: boolean = false) => {
    const isSelected = localSelectedTags.includes(tag.name);

    return (
      <div
        key={tag.name}
        className={classNames('tag-management-item', { selected: isSelected })}
      >
        <div
          className="tag-management-item-content"
          onClick={() => handleToggleTag(tag.name)}
        >
          <AntTag color={tag.color} className="tag-chip">
            {tag.name}
            {isNew && <span className="tag-management-new-badge"> (new)</span>}
          </AntTag>
        </div>

        <div className="tag-management-item-actions">
          <ColorPicker
            value={tag.color}
            onChange={(color) => void handleColorChange(tag.name, color)}
            size="small"
            showText={() => null}
          />
          <CompactButton
            size="small"
            icon={<DeleteOutlined />}
            danger
            onClick={(e) => {
              e.stopPropagation();
              setTagToDelete(tag.name);
            }}
          />
        </div>
      </div>
    );
  };

  return (
    <FormDialog
      visible={visible}
      title={title}
      onOk={handleConfirm}
      onCancel={onCancel}
      okText={t('tags.confirmWithCount', { count: localSelectedTags.length })}
      cancelText={t('common.cancel')}
      width={700}
      destroyOnHidden
    >
      <div className="tag-management-dialog">
        {/* Search bar */}
        <div className="tag-management-search">
          <CompactInput
            placeholder={t('tags.searchPlaceholder')}
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            prefix={<SearchOutlined />}
            allowClear
          />
        </div>

        {/* Selection actions */}
        <div className="tag-management-actions">
          <Space size="small">
            <CompactButton
              size="small"
              type="primary"
              onClick={handleSelectAll}
            >
              {t("common.selectAll")} (
              {filteredExistingTags.length + filteredNewTags.length})
            </CompactButton>
            <CompactButton size="small" onClick={handleDeselectAll}>
              {t("common.deselectAll")}
            </CompactButton>
          </Space>
          <div className="tag-management-count">
            {localSelectedTags.length} {t("common.selected")}
          </div>
        </div>

        {/* Tag lists */}
        <div className="tag-management-list">
          {/* Newly created tags section */}
          {filteredNewTags.length > 0 && (
            <div className="tag-management-section">
              <div className="tag-management-section-header">
                <PlusOutlined /> {t('tags.newlyCreatedTags')}
              </div>
              <div className="tag-management-section-content">
                {filteredNewTags.map((tag) => renderTagItem(tag, true))}
              </div>
            </div>
          )}

          {/* Existing tags section */}
          {filteredExistingTags.length > 0 && (
            <div className="tag-management-section">
              {filteredNewTags.length > 0 && (
                <div className="tag-management-section-header">{t('tags.existingTags')}</div>
              )}
              <div className="tag-management-section-content">
                {filteredExistingTags.map((tag) => renderTagItem(tag))}
              </div>
            </div>
          )}

          {/* Empty state */}
          {filteredExistingTags.length === 0 &&
            filteredNewTags.length === 0 && (
              <Empty
                description={searchTerm ? t('tags.noTagsFound') : t('tags.noTagsAvailable')}
                image={Empty.PRESENTED_IMAGE_SIMPLE}
              />
            )}
        </div>
      </div>

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        visible={tagToDelete !== null}
        title={t("tags.deleteTag")}
        content={
          <>
            <p>{t("tags.deleteTagConfirm", { tagName: tagToDelete })}</p>
            <p
              style={{
                fontSize: "12px",
                color: "var(--color-text-secondary)",
                marginTop: "8px",
              }}
            >
              {t("tags.deleteTagNote")}
            </p>
          </>
        }
        okText={t("common.delete")}
        cancelText={t("common.cancel")}
        okType="danger"
        onOk={handleDeleteTag}
        onCancel={() => setTagToDelete(null)}
      />
    </FormDialog>
  );
};
