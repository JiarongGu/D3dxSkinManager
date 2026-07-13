import React, { useCallback, useEffect, useState } from 'react';
import { ThunderboltOutlined, SettingOutlined } from '@ant-design/icons';
import type { MenuProps } from 'antd';
import { useTranslation } from 'react-i18next';
import { toolService } from '../../../shared/services/ipc';
import { notification } from '../../../shared/utils/notification';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useEventSubscription } from '../../../shared/hooks/useEventSubscription';
import { Module, ToolsEventType } from '../../../shared/services/eventBus';
import type { ContextMenuItem } from '../../../shared/components/menu';
import type { ModFixTool as FixToolEntry } from '../../../shared/types/modFix.types';

export interface ModFixTools {
  /** Reload the fix-tool library (also auto-reloaded on the fixtools/ disk-watch event). */
  loadFixTools: () => Promise<void>;
  /** The right-click "Fix" submenu for the given mods (each toolset's entries flattened + Manage). */
  buildFixSubmenu: (modIds: string[]) => ContextMenuItem;
  /** The bulk-action-bar "Fix" dropdown items for the given mods. */
  bulkFixMenuItems: (modIds: string[]) => MenuProps['items'];
}

/**
 * Fix-tool menus for the mod list. Loads the per-profile fix-tool library (live-refreshed when the
 * fixtools/ folder changes) and builds the right-click "Fix" submenu + the bulk-bar dropdown, whose items
 * run a tool entry against a set of mods. `onManage` opens the fix-tool manager (dialog state owned by the
 * caller). Extracted verbatim from ModList (behavior-preserving); the menu builders take the target
 * modIds so the hook stays decoupled from the current selection.
 */
export function useModFixTools(onManage: () => void): ModFixTools {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [fixTools, setFixTools] = useState<FixToolEntry[]>([]);

  // Load the per-profile fix-tool library so the "Fix" menus can list them.
  const loadFixTools = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      setFixTools(await toolService.getFixTools(selectedProfileId));
    } catch {
      setFixTools([]);
    }
  }, [selectedProfileId]);
  useEffect(() => { void loadFixTools(); }, [loadFixTools]);
  // Live refresh when the fixtools/ folder changes on disk (watcher).
  useEventSubscription(Module.TOOL, ToolsEventType.FIX_TOOLS_CHANGED, () => { void loadFixTools(); }, [loadFixTools]);

  // Run one fix-tool entry against the given mods (the menu items call this).
  const runFixEntry = async (toolName: string, entryPath: string, recompress: boolean, modIds: string[]) => {
    if (!selectedProfileId) return;
    try {
      await toolService.runModFix(selectedProfileId, { scriptPath: entryPath, modIds, recompress });
      notification.info(t('mods.notifications.fixStarted', { name: toolName }));
    } catch {
      notification.error(t('tools.modFix.fixPartialFail', { failed: modIds.length }));
    }
  };

  // A runnable entry's menu label: the user's friendly name (alias), else the filename WITHOUT its
  // extension. No "Toolset — " prefix (per user: friendly name only).
  const entryLabel = (e: { displayName?: string; name: string }) =>
    e.displayName?.trim() || e.name.replace(/\.[^.]+$/, '');

  const buildFixSubmenu = (modIds: string[]): ContextMenuItem => {
    const children: ContextMenuItem[] = [];
    // Disabled tools stay in the library but are hidden from the Fix menu.
    const activeTools = fixTools.filter((tf) => tf.enabled !== false);
    if (activeTools.length === 0) {
      children.push({ key: 'fix-none', label: t('contextMenu.noFixTools'), disabled: true });
    }
    for (const tf of activeTools) {
      if (tf.entries.length === 0) {
        children.push({ key: `fix-${tf.id}`, label: `${tf.name} — ${t('tools.modFix.setEntryFirst')}`, disabled: true });
      } else if (tf.entries.length === 1) {
        const e = tf.entries[0];
        children.push({ key: `fix-${tf.id}`, label: tf.name, icon: <ThunderboltOutlined />, onClick: () => void runFixEntry(tf.name, e.path, tf.recompressDefault, modIds) });
      } else {
        for (const e of tf.entries) {
          children.push({ key: `fix-${tf.id}-${e.name}`, label: entryLabel(e), icon: <ThunderboltOutlined />, onClick: () => void runFixEntry(tf.name, e.path, tf.recompressDefault, modIds) });
        }
      }
    }
    children.push({ type: 'divider' as const });
    children.push({ key: 'fix-manage', label: t('contextMenu.manageFixTools'), icon: <SettingOutlined />, onClick: onManage });
    return { key: 'run-fix', label: t('contextMenu.runFix'), icon: <ThunderboltOutlined />, children };
  };

  const bulkFixMenuItems = (modIds: string[]): MenuProps['items'] => {
    const items: NonNullable<MenuProps['items']> = [];
    for (const tf of fixTools.filter((f) => f.enabled !== false)) {
      if (tf.entries.length === 0) {
        items.push({ key: tf.id, label: `${tf.name} — ${t('tools.modFix.setEntryFirst')}`, disabled: true });
      } else if (tf.entries.length === 1) {
        const e = tf.entries[0];
        items.push({ key: tf.id, label: tf.name, onClick: () => void runFixEntry(tf.name, e.path, tf.recompressDefault, modIds) });
      } else {
        for (const e of tf.entries) {
          items.push({ key: `${tf.id}-${e.name}`, label: entryLabel(e), onClick: () => void runFixEntry(tf.name, e.path, tf.recompressDefault, modIds) });
        }
      }
    }
    if (items.length === 0) items.push({ key: 'none', label: t('contextMenu.noFixTools'), disabled: true });
    items.push({ type: 'divider' });
    items.push({ key: 'manage', label: t('contextMenu.manageFixTools'), onClick: onManage });
    return items;
  };

  return { loadFixTools, buildFixSubmenu, bulkFixMenuItems };
}
