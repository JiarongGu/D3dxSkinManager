import React, { useState, useEffect, useMemo, useRef } from 'react';
import { Typography, Button } from 'antd';
import { SearchOutlined, UndoOutlined, SaveOutlined, CloseOutlined } from '@ant-design/icons';
import { cloneDeep } from 'lodash-es';
import { AgGridReact } from 'ag-grid-react';
import { BatchEditGrid } from './BatchEditGrid';
import { FindReplacePanel, ReplaceConfig } from './FindReplacePanel';
import { modService } from '../../../../shared/services/ipc';
import { ModInfo } from '../../../../shared/types/mod.types';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { CompactButton } from '../../../../shared/components/compact';
import { notification } from '../../../../shared/utils/notification';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { refreshMods } from '../../operations/modOperations';
import './BatchEditModsScreen.css';
import logger from '../../../../shared/utils/logger';

const { Text } = Typography;

interface BatchEditFormContentProps {
  setLoading: (loading: boolean, loadingText?: string) => void;
}

/**
 * Form content component - contains all state and logic for batch editing mods
 */
const BatchEditFormContent: React.FC<BatchEditFormContentProps> = ({ setLoading }) => {
  const { t } = useTranslation();

  // Subscribe to state from store
  const modsToEdit = useModsStore(s => s.modsToEdit);
  const { selectedProfileId } = useProfile();
  const { closeBatchEditScreen } = useMods();

  // Grid ref to access AG Grid API
  const gridRef = useRef<AgGridReact>(null);

  // Local state
  const [editedMods, setEditedMods] = useState<ModInfo[]>([]);
  const [showSearchReplace, setShowSearchReplace] = useState(false);
  const [hasChanges, setHasChanges] = useState(false);
  const [saving, setSaving] = useState(false);
  const [allTags, setAllTags] = useState<any[]>([]);
  const [searchHighlight, setSearchHighlight] = useState<{ find: string; caseSensitive: boolean; useRegex: boolean } | null>(null);

  // Load tags when screen opens
  useEffect(() => {
    const loadTags = async () => {
      if (selectedProfileId) {
        try {
          const tags = await modService.getAllTags(selectedProfileId);
          setAllTags(tags);
        } catch (error) {
          logger.error('Failed to load tags:', error);
        }
      }
    };
    loadTags();
  }, [selectedProfileId]);

  // Initialize mods when modsToEdit changes
  useEffect(() => {
    logger.verbose('[BatchEdit] modsToEdit changed:', modsToEdit.length);
    if (modsToEdit.length > 0) {
      // Deep clone mods to avoid reference issues with nested objects/arrays
      const clonedMods = cloneDeep(modsToEdit);
      logger.verbose('[BatchEdit] Setting editedMods:', clonedMods.length);
      setEditedMods(clonedMods);
      setHasChanges(false);
    }
  }, [modsToEdit]);

  // Keyboard shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const ctrlPressed = e.ctrlKey || e.metaKey;

      // Ctrl+F or Ctrl+H to toggle find/replace panel
      if (ctrlPressed && (e.key === 'f' || e.key === 'h')) {
        e.preventDefault();
        setShowSearchReplace(prev => !prev);
      }

      // Escape to close find/replace panel
      if (e.key === 'Escape' && showSearchReplace) {
        setShowSearchReplace(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [showSearchReplace]);

  const handleModsChange = (mods: ModInfo[]) => {
    setEditedMods(mods);
    setHasChanges(true);
  };

  const handleSearchReplace = (config: ReplaceConfig) => {
    // Search and replace across all searchable columns
    const searchableColumns: Array<'name' | 'author' | 'description'> = ['name', 'author', 'description'];

    const updated = editedMods.map(mod => {
      const updatedMod = { ...mod };

      searchableColumns.forEach(column => {
        const value = mod[column];
        if (typeof value !== 'string') return;

        let newValue = value;
        if (config.useRegex) {
          try {
            const flags = config.caseSensitive ? 'g' : 'gi';
            const regex = new RegExp(config.find, flags);
            newValue = value.replace(regex, config.replace);
          } catch (error) {
            logger.error('Regex error:', error);
            return;
          }
        } else {
          const flags = config.caseSensitive ? 'g' : 'gi';
          const regex = new RegExp(config.find.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), flags);
          newValue = value.replace(regex, config.replace);
        }

        if (newValue !== value) {
          updatedMod[column] = newValue;
        }
      });

      return updatedMod;
    });

    setEditedMods(updated);
    setHasChanges(true);
    notification.success(t('mods.batchEdit.notifications.replaceSuccess'));
  };

  const handleReset = () => {
    // Reset to original data using AG Grid API
    const clonedMods = cloneDeep(modsToEdit);
    setEditedMods(clonedMods);
    setHasChanges(false);
    notification.info(t('mods.batchEdit.notifications.resetSuccess'));
  };

  const handleCancel = () => {
    closeBatchEditScreen();
  };

  const handleSave = async () => {
    if (!selectedProfileId || !gridRef.current) return;

    // Get dirty (modified) rows from AG Grid
    const dirtyNodes: any[] = [];
    gridRef.current.api.forEachNode((node) => {
      if (node.data && gridRef.current!.api.getCellEditorInstances({ rowNodes: [node] }).length > 0) {
        // Cell is currently being edited
        dirtyNodes.push(node);
      }
    });

    // Get all current row data and compare with original to find changes
    const changedMods: ModInfo[] = [];
    const originalModsMap = new Map(modsToEdit.map(m => [m.id, m]));

    gridRef.current.api.forEachNode((node) => {
      if (node.data) {
        const originalMod = originalModsMap.get(node.data.id);
        if (originalMod) {
          // Simple shallow comparison for the editable fields
          const hasChanged =
            node.data.name !== originalMod.name ||
            node.data.author !== originalMod.author ||
            JSON.stringify(node.data.tags) !== JSON.stringify(originalMod.tags) ||
            node.data.grading !== originalMod.grading ||
            node.data.description !== originalMod.description ||
            node.data.disablePreview !== originalMod.disablePreview;

          if (hasChanged) {
            changedMods.push(node.data);
          }
        }
      }
    });

    // If no changes, just close
    if (changedMods.length === 0) {
      notification.info(t('mods.batchEdit.notifications.noChanges'));
      closeBatchEditScreen();
      return;
    }

    setSaving(true);
    setLoading(true, t('mods.batchEdit.notifications.saving'));

    try {
      // Build updates object with Mod ID as key and metadata as value
      const updates: Record<string, any> = {};
      changedMods.forEach((mod) => {
        updates[mod.id] = {
          name: mod.name,
          author: mod.author,
          tags: mod.tags,
          grading: mod.grading,
          description: mod.description,
          disablePreview: mod.disablePreview,
        };
      });

      // Call batch update API
      const result = await modService.batchUpdateMetadata(selectedProfileId, updates);

      if (result.updatedCount > 0) {
        notification.success(
          t('mods.notifications.batchUpdateSuccess', { count: result.updatedCount })
        );
        closeBatchEditScreen();
        // Refresh mods list
        await refreshMods(selectedProfileId);
      }

      const failCount = result.totalRequested - result.updatedCount;
      if (failCount > 0) {
        notification.error(
          t('mods.notifications.batchUpdateFailed', { count: failCount })
        );
      }
    } catch (error) {
      logger.error('Failed to batch update mods:', error);
      notification.error(t('mods.notifications.batchUpdateFailed', { count: changedMods.length }));
    } finally {
      setSaving(false);
      setLoading(false);
    }
  };

  const columns = [
    { label: t("common.name"), value: 'name' },
    { label: t("common.author"), value: 'author' },
    { label: t("common.description"), value: 'description' },
  ];

  return (
    <div className="batch-edit-screen-wrapper">
      <div className="batch-edit-screen-content">
        {/* Editor-style Toolbar */}
        <div className="batch-edit-toolbar">
          <div className="batch-edit-toolbar-left">
            <Button
              icon={<UndoOutlined />}
              onClick={handleReset}
              disabled={!hasChanges}
              size="small"
            >
              {t('mods.batchEdit.toolbar.reset')}
              <span className="batch-edit-shortcut">Ctrl+Z</span>
            </Button>
            <span className="batch-edit-divider" />
            <div className="batch-edit-toolbar-info">
              <Text type="secondary" style={{ fontSize: '12px' }}>
                {t('mods.batchEdit.toolbar.modsCount', { count: editedMods.length })}
                {hasChanges && ` �?${t('mods.batchEdit.toolbar.modified')}`}
              </Text>
            </div>
          </div>

          <div className="batch-edit-toolbar-right">
            <Button
              icon={<SearchOutlined />}
              onClick={() => setShowSearchReplace(prev => !prev)}
              size="small"
            >
              {t('mods.batchEdit.toolbar.findReplace')}
              <span className="batch-edit-shortcut">Ctrl+F</span>
            </Button>
          </div>
        </div>

        {/* Grid Container with Find/Replace Panel */}
        <div className="batch-edit-grid-wrapper" style={{ position: 'relative' }}>
          <FindReplacePanel
            visible={showSearchReplace}
            onClose={() => {
              setShowSearchReplace(false);
              setSearchHighlight(null);
            }}
            onReplace={handleSearchReplace}
            onSearchChange={setSearchHighlight}
            columns={columns}
          />
          <BatchEditGrid
            mods={editedMods}
            tags={allTags}
            onModsChange={handleModsChange}
            searchHighlight={searchHighlight}
            gridRef={gridRef}
          />
        </div>
      </div>

      {/* Footer with action buttons */}
      <div className="slide-in-screen-footer">
        <div style={{ display: 'flex', gap: '8px' }}>
          <CompactButton.Danger onClick={handleCancel} icon={<CloseOutlined />} disabled={saving}>
            {t("common.cancel")}
          </CompactButton.Danger>
          <CompactButton.Primary
            onClick={handleSave}
            loading={saving}
            disabled={!hasChanges}
            icon={<SaveOutlined />}
          >
            {t("common.saveChanges")}
          </CompactButton.Primary>
        </div>
      </div>
    </div>
  );
};

/**
 * Wrapper component that provides setLoading to the form content
 */
const BatchEditScreenContent: React.FC<{ setLoadingFn: (loading: boolean, text?: string) => void }> = ({ setLoadingFn }) => {
  return <BatchEditFormContent setLoading={setLoadingFn} />;
};

/**
 * Slide-in screen for batch editing mod metadata
 * Lightweight wrapper that manages the slide-in dialog
 */
export const BatchEditModsScreen: React.FC = () => {
  const { t } = useTranslation();
  const visible = useModsStore(s => s.batchEditScreenVisible);
  const modsToEdit = useModsStore(s => s.modsToEdit);
  const { closeBatchEditScreen } = useMods();

  // Create a ref to store setLoading function
  const setLoadingRef = useRef<(loading: boolean, text?: string) => void>(() => {});

  // Create content with the ref
  const content = useMemo(
    () => <BatchEditScreenContent setLoadingFn={(loading, text) => setLoadingRef.current(loading, text)} />,
    []
  );

  const { setLoading } = useSlideInScreen({
    visible,
    title: `${t('mods.batchEdit.title')} (${modsToEdit.length} ${t('common.selected')})`,
    content,
    width: '80%',
    onClose: closeBatchEditScreen,
  });

  // Update the ref when setLoading changes
  setLoadingRef.current = setLoading;

  return null;
};
