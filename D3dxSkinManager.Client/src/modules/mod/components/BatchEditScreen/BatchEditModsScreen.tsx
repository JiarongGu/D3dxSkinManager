import React, { useState, useEffect } from 'react';
import { Typography, Button } from 'antd';
import { SearchOutlined, UndoOutlined, SaveOutlined, CloseOutlined } from '@ant-design/icons';
import { BatchEditGrid } from './BatchEditGrid';
import { FindReplacePanel, ReplaceConfig } from './FindReplacePanel';
import { modService } from '../../../../shared/services/ipc';
import { ModInfo } from '../../../../shared/types/mod.types';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { CompactButton } from '../../../../shared/components/compact/CompactButton';
import { notification } from '../../../../shared/utils/notification';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { refreshMods } from '../../operations/modOperations';
import './BatchEditModsScreen.css';
import logger from '../../../../shared/utils/logger';

const { Text } = Typography;

/**
 * Form content component - contains all state and logic for batch editing mods
 */
const BatchEditFormContent: React.FC = () => {
  const { t } = useTranslation();

  // Subscribe to state from store
  const modsToEdit = useModsStore(s => s.modsToEdit);
  const { state: profileState } = useProfile();
  const { closeBatchEditScreen } = useMods();

  // Local state
  const [editedMods, setEditedMods] = useState<ModInfo[]>([]);
  const [originalMods, setOriginalMods] = useState<ModInfo[]>([]);
  const [showSearchReplace, setShowSearchReplace] = useState(false);
  const [hasChanges, setHasChanges] = useState(false);
  const [saving, setSaving] = useState(false);
  const [allTags, setAllTags] = useState<any[]>([]);
  const [searchHighlight, setSearchHighlight] = useState<{ find: string; caseSensitive: boolean; useRegex: boolean } | null>(null);

  // Load tags when screen opens
  useEffect(() => {
    const loadTags = async () => {
      if (profileState.selectedProfile?.id) {
        try {
          const tags = await modService.getAllTags(profileState.selectedProfile.id);
          setAllTags(tags);
        } catch (error) {
          logger.error('Failed to load tags:', error);
        }
      }
    };
    loadTags();
  }, [profileState.selectedProfile?.id]);

  // Initialize mods when modsToEdit changes
  useEffect(() => {
    logger.verbose('[BatchEdit] modsToEdit changed:', modsToEdit.length);
    if (modsToEdit.length > 0) {
      // Deep clone mods
      const clonedMods = modsToEdit.map(mod => ({ ...mod }));
      logger.verbose('[BatchEdit] Setting editedMods:', clonedMods.length);
      setEditedMods(clonedMods);
      setOriginalMods(clonedMods);
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
    setEditedMods([...originalMods]);
    setHasChanges(false);
    notification.info(t('mods.batchEdit.notifications.resetSuccess'));
  };

  const handleCancel = () => {
    closeBatchEditScreen();
  };

  const handleSave = async () => {
    if (!profileState.selectedProfile?.id) return;

    setSaving(true);
    let successCount = 0;
    let failCount = 0;

    try {
      for (const mod of editedMods) {
        try {
          await modService.updateMetadata(profileState.selectedProfile.id, mod.sha, {
            name: mod.name,
            author: mod.author,
            tags: mod.tags,
            grading: mod.grading,
            description: mod.description,
            disablePreview: mod.disablePreview,
          });
          successCount++;
        } catch (error) {
          logger.error(`Failed to update mod ${mod.sha}:`, error);
          failCount++;
        }
      }

      if (successCount > 0) {
        notification.success(
          t('mods.notifications.batchUpdateSuccess', { count: successCount })
        );
        closeBatchEditScreen();
        // Refresh mods list
        await refreshMods(profileState.selectedProfile.id);
      }

      if (failCount > 0) {
        notification.error(
          t('mods.notifications.batchUpdateFailed', { count: failCount })
        );
      }
    } finally {
      setSaving(false);
    }
  };

  const columns = [
    { label: t('mods.batchEdit.column.name'), value: 'name' },
    { label: t('mods.batchEdit.column.author'), value: 'author' },
    { label: t('mods.batchEdit.column.description'), value: 'description' },
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
                {hasChanges && ` • ${t('mods.batchEdit.toolbar.modified')}`}
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
          />
        </div>
      </div>

      {/* Footer with action buttons */}
      <div className="slide-in-screen-footer">
        <div style={{ display: 'flex', gap: '8px' }}>
          <CompactButton onClick={handleCancel} icon={<CloseOutlined />}>
            {t('mods.batchEdit.toolbar.cancel')}
          </CompactButton>
          <CompactButton
            type="primary"
            onClick={handleSave}
            loading={saving}
            disabled={!hasChanges}
            icon={<SaveOutlined />}
          >
            {t('mods.batchEdit.toolbar.saveChanges')}
          </CompactButton>
        </div>
      </div>
    </div>
  );
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

  useSlideInScreen({
    visible,
    title: `${t('mods.batchEdit.title')} (${modsToEdit.length} ${t('common.selected')})`,
    content: <BatchEditFormContent />,
    width: '80%',
    onClose: closeBatchEditScreen,
  });

  return null;
};
