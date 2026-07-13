import { copyToClipboard } from "../../../../shared/utils/clipboardHelper";
import { notification } from "../../../../shared/utils/notification";
import React, { useState, useCallback } from "react";
import classNames from "classnames";
import { Space, Spin, Dropdown } from 'antd';
import type { MenuProps } from "antd";
import {
  PlayCircleOutlined,
  PauseCircleOutlined,
  EditOutlined,
  DeleteOutlined,
  FolderOpenOutlined,
  FileTextOutlined,
  MergeCellsOutlined,
  CompressOutlined,
  ClearOutlined,
  CopyOutlined,
  SyncOutlined,
  ThunderboltOutlined,
  ImportOutlined,
  SettingOutlined,
  CloseOutlined,
} from "@ant-design/icons";
import { ModInfo } from "../../../../shared/types/mod.types";
import { systemService } from "../../../../shared/services/ipc";
import { modService } from "../../../../shared/services/ipc";
import { toolService } from "../../../../shared/services/ipc";
import type { ModFixTool as FixToolEntry } from "../../../../shared/types/modFix.types";
import { Module, ToolsEventType } from "../../../../shared/services/eventBus";
import { useEventSubscription } from "../../../../shared/hooks/useEventSubscription";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { ConfirmDialog } from "../../../../shared/components/dialogs";
import {
  ContextMenu,
  ContextMenuItem,
  useContextMenu,
} from "../../../../shared/components/menu";
import { refreshMods } from "../../operations/modOperations";
import { useModsStore } from "../../store/modsStore";
import { useModsState } from "../../hooks/useMods";
import { useSettingsStore } from "../../../setting/store/settingsStore";
import { useTranslation } from "react-i18next";
import { BatchEditModsScreen } from "../BatchEditScreen";
import { ModFixTool } from "../../../tool/components/ModFixTool/ModFixTool";
import { ModIniEditor } from "../ModIniEditor/ModIniEditor";
import { MergeModsDialog } from "../MergeModsDialog/MergeModsDialog";
import { ModOptimizeDialog } from "../ModOptimizeDialog/ModOptimizeDialog";
import { useMods } from "../../hooks/useMods";
import { useInfiniteScroll } from "../../hooks/useInfiniteScroll";
import "./ModList.css";
import { CompactButton } from '../../../../shared/components/compact';
import { ModListItem } from "./ModListItem";

// Estimated item height in pixels (used for bottom spacer to maintain correct scroll height)
const ITEM_HEIGHT = 64;

interface ModListProps {
  mods: ModInfo[];
  loading: boolean;
  onLoad: (id: string) => void;
  onUnload: (id: string) => void;
  onDelete: (id: string, name: string) => void;
  onEdit?: (mod: ModInfo) => void;
  onRowClick?: (mod: ModInfo, event?: React.MouseEvent) => void;
  selectedMod?: ModInfo;
  selectedModIds?: string[];
  onClearSelection?: () => void;
  onBeforeReload?: () => void;
  onAfterReload?: () => void;
  /** Force a minimum number of items to render (used to scroll to items not yet in the DOM) */
  minDisplayCount?: number;
}

export const ModList: React.FC<ModListProps> = ({
  mods,
  loading,
  onLoad,
  onUnload,
  onDelete,
  onEdit,
  onRowClick,
  selectedMod,
  selectedModIds = [],
  onClearSelection,
  onBeforeReload,
  onAfterReload,
  minDisplayCount = 0,
}) => {
  const { t } = useTranslation();
  // Windowed rendering + infinite-scroll "load more" (extracted to useInfiniteScroll). effectiveDisplayCount
  // is the count actually rendered: >= minDisplayCount, which the parent bumps to scroll to an off-DOM item.
  const { visibleItems: displayedMods, sentinelRef: observerTarget, visibleCount: effectiveDisplayCount } =
    useInfiniteScroll(mods, minDisplayCount);
  const { selectedProfileId } = useProfile();
  const { openBatchEditScreen } = useMods();
  const menuState = useContextMenu();
  const [contextMenuMod, setContextMenuMod] = useState<ModInfo>();
  const [deleteConfirm, setDeleteConfirm] = useState<{
    visible: boolean;
    mod?: ModInfo;
  }>({ visible: false });
  const busyModIds = useModsState((s) => s.busyModIds);
  // "Game updated" watermark → per-row "may need re-fix" flag (see modFixRef).
  const gameUpdatedUtc = useSettingsStore((s) => s.gameUpdatedUtc);
  const [showFixManager, setShowFixManager] = useState(false);
  const [iniEditorMod, setIniEditorMod] = useState<ModInfo>();
  const [mergeDialogMods, setMergeDialogMods] = useState<ModInfo[]>();
  const [optimizeMod, setOptimizeMod] = useState<ModInfo>();

  // DEV-only: open the config editor directly (bypasses the context menu) for fast UI iteration in
  // Chrome / CDP. Stripped from production builds.
  React.useEffect(() => {
    if (!import.meta.env.DEV) return;
    (window as unknown as { __openIniEditor?: (id: string, name?: string) => void }).__openIniEditor =
      (id, name = 'Config') => setIniEditorMod({ id, name, hasCache: true } as ModInfo);
    return () => { delete (window as unknown as { __openIniEditor?: unknown }).__openIniEditor; };
  }, []);
  const [fixTools, setFixTools] = useState<FixToolEntry[]>([]);

  // Load the per-profile fix-tool library so the right-click "Fix" submenu can list them.
  const loadFixTools = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      setFixTools(await toolService.getFixTools(selectedProfileId));
    } catch {
      setFixTools([]);
    }
  }, [selectedProfileId]);
  React.useEffect(() => { void loadFixTools(); }, [loadFixTools]);
  // Live refresh the submenu when the fixtools/ folder changes on disk (watcher).
  useEventSubscription(Module.TOOL, ToolsEventType.FIX_TOOLS_CHANGED, () => { void loadFixTools(); }, [loadFixTools]);

  // Save scroll position before reload and restore after
  React.useEffect(() => {
    if (loading) {
      onBeforeReload?.();
    } else {
      onAfterReload?.();
    }
  }, [loading, onBeforeReload, onAfterReload]);


  /**
   * Show delete mod confirmation dialog
   */
  const handleShowDeleteConfirm = (mod: ModInfo) => {
    setDeleteConfirm({ visible: true, mod });
  };

  /**
   * Execute delete mod after confirmation
   * Supports both single mod deletion and batch deletion
   */
  const handleConfirmDelete = async () => {
    const mod = deleteConfirm.mod;
    if (!mod) {
      setDeleteConfirm({ visible: false });
      return;
    }

    // Check if this is a batch deletion (multiple mods selected)
    const isMultiSelect = selectedModIds.length > 1;

    if (isMultiSelect) {
      // Batch delete all selected mods using backend batch API
      if (!selectedProfileId) {
        notification.error(t("errors.noProfileSelected"));
        setDeleteConfirm({ visible: false });
        return;
      }

      const profileId = selectedProfileId;

      try {
        // Fire-and-forget: acks immediately, one cancellable process tracks the batch in the
        // Activity panel and rows disappear as each DELETED event lands.
        await modService.batchDeleteMods(profileId, selectedModIds);
        notification.info(
          t("mods.notifications.batchDeleteStarted", { count: selectedModIds.length })
        );
      } catch (error: unknown) {
        notification.error(
          t("mods.notifications.batchDeleteFailed", { count: selectedModIds.length })
        );
      }
    } else {
      // Single mod deletion
      await onDelete(mod.id, mod.name);
    }

    setDeleteConfirm({ visible: false });
  };

  /**
   * Delete cached mod (no confirmation needed)
   * Deletes cache for both loaded and unloaded mods:
   * - Loaded: {Id}/ directory
   * - Unloaded: DISABLED-{Id}/ directory
   * Updates local mod state to set isLoaded=false without backend fetch.
   * Uses delayed loading to show spinner only if operation takes >100ms.
   */
  const handleDeleteCachedMod = async (mod: ModInfo) => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }

    const profileId = selectedProfileId;

    try {
      // Delete the cache
      const success = await modService.deleteCache(profileId, mod.id);
      if (success) {
        notification.success(
          t("mods.notifications.cacheDeleted", { name: mod.name }),
        );

        // Refresh from backend to update hasCache and other properties
        await refreshMods(profileId);
      } else {
        notification.error(t("mods.notifications.deleteCacheFailed"));
      }
    } catch (error: unknown) {
      const errorMessage =
        error instanceof Error ? error.message : "Unknown error";
      notification.error(
        `${t("mods.notifications.deleteCacheFailed")}: ${errorMessage}`,
      );
    }
  };

  /**
   * Update archive from cache folder (re-compress cache back to archive)
   * Shows per-mod loading indicator during compression
   */
  const handleUpdateArchive = async (mod: ModInfo) => {
    if (!selectedProfileId || busyModIds.has(mod.id)) return;

    const { addBusyMod, removeBusyMod } = useModsStore.getState();
    addBusyMod(mod.id);
    try {
      await modService.updateArchiveFromCache(selectedProfileId, mod.id);
      notification.success(
        t("mods.notifications.archiveUpdated", { name: mod.name }),
      );
    } catch (error: unknown) {
      notification.error(t("mods.notifications.archiveUpdateFailed"));
    } finally {
      removeBusyMod(mod.id);
    }
  };

  // #14: replace an existing mod's content with a new archive/file (same id, metadata kept).
  const handleUpdateMod = async (mod: ModInfo) => {
    if (!selectedProfileId || busyModIds.has(mod.id)) return;
    const res = await systemService.openFileDialog({
      title: t("contextMenu.updateMod"),
      filters: [{ name: t("mods.modArchiveFilter"), extensions: ["7z", "zip", "rar"] }],
      rememberPathKey: "modUpdateSource",
    });
    if (!res.success || !res.filePath) return;

    const { addBusyMod, removeBusyMod } = useModsStore.getState();
    addBusyMod(mod.id);
    try {
      await modService.updateMod(selectedProfileId, mod.id, res.filePath);
      notification.success(t("mods.notifications.modUpdated", { name: mod.name }));
      await refreshMods(selectedProfileId);
    } catch (error: unknown) {
      notification.error(t("mods.notifications.modUpdateFailed"));
    } finally {
      removeBusyMod(mod.id);
    }
  };

  // Run one fix-tool entry against the given mods (the right-click submenu items call this).
  const runFixEntry = async (toolName: string, entryPath: string, recompress: boolean, modIds: string[]) => {
    if (!selectedProfileId) return;
    try {
      await toolService.runModFix(selectedProfileId, { scriptPath: entryPath, modIds, recompress });
      notification.info(t("mods.notifications.fixStarted", { name: toolName }));
    } catch (error: unknown) {
      notification.error(t("tools.modFix.fixPartialFail", { failed: modIds.length }));
    }
  };

  // A runnable entry's menu label: the user's friendly name (alias), else the filename WITHOUT its
  // extension. No "Toolset — " prefix (per user: friendly name only).
  const entryLabel = (e: { displayName?: string; name: string }) =>
    e.displayName?.trim() || e.name.replace(/\.[^.]+$/, '');

  // "Fix" submenu: each toolset's entries flattened (friendly name per entry) + Manage.
  const buildFixSubmenu = (modIds: string[]): ContextMenuItem => {
    const children: ContextMenuItem[] = [];
    // Disabled tools stay in the library but are hidden from the Fix menu.
    const activeTools = fixTools.filter((t) => t.enabled !== false);
    if (activeTools.length === 0) {
      children.push({ key: "fix-none", label: t("contextMenu.noFixTools"), disabled: true });
    }
    for (const tf of activeTools) {
      if (tf.entries.length === 0) {
        children.push({ key: `fix-${tf.id}`, label: `${tf.name} — ${t("tools.modFix.setEntryFirst")}`, disabled: true });
      } else if (tf.entries.length === 1) {
        const e = tf.entries[0];
        children.push({ key: `fix-${tf.id}`, label: tf.name, icon: <ThunderboltOutlined />, onClick: () => void runFixEntry(tf.name, e.path, tf.recompressDefault, modIds) });
      } else {
        for (const e of tf.entries) {
          children.push({ key: `fix-${tf.id}-${e.name}`, label: entryLabel(e), icon: <ThunderboltOutlined />, onClick: () => void runFixEntry(tf.name, e.path, tf.recompressDefault, modIds) });
        }
      }
    }
    children.push({ type: "divider" as const });
    children.push({
      key: "fix-manage",
      label: t("contextMenu.manageFixTools"),
      icon: <SettingOutlined />,
      onClick: () => setShowFixManager(true),
    });
    return { key: "run-fix", label: t("contextMenu.runFix"), icon: <ThunderboltOutlined />, children };
  };

  // ===== Bulk-action bar (shown when 2+ mods selected) — reuses the per-mod batch handlers =====
  const selectedMods = () => mods.filter((m) => selectedModIds.includes(m.id));

  const bulkFixMenuItems = (): MenuProps["items"] => {
    const items: NonNullable<MenuProps["items"]> = [];
    for (const tf of fixTools.filter((t) => t.enabled !== false)) {
      if (tf.entries.length === 0) {
        items.push({ key: tf.id, label: `${tf.name} — ${t("tools.modFix.setEntryFirst")}`, disabled: true });
      } else if (tf.entries.length === 1) {
        const e = tf.entries[0];
        items.push({ key: tf.id, label: tf.name, onClick: () => void runFixEntry(tf.name, e.path, tf.recompressDefault, selectedModIds) });
      } else {
        for (const e of tf.entries) {
          items.push({ key: `${tf.id}-${e.name}`, label: entryLabel(e), onClick: () => void runFixEntry(tf.name, e.path, tf.recompressDefault, selectedModIds) });
        }
      }
    }
    if (items.length === 0) items.push({ key: "none", label: t("contextMenu.noFixTools"), disabled: true });
    items.push({ type: "divider" });
    items.push({ key: "manage", label: t("contextMenu.manageFixTools"), onClick: () => setShowFixManager(true) });
    return items;
  };

  const openBulkDelete = () => {
    const base = selectedMods()[0];
    if (!base) return;
    setDeleteConfirm({ visible: true, mod: { ...base, name: t("mods.notifications.selectedMods", { count: selectedModIds.length }) } });
  };

  const bulkBar = selectedModIds.length >= 2 && (
    <div className="mod-bulk-bar">
      <span className="mod-bulk-bar__count">{t("mods.bulkBar.selected", { count: selectedModIds.length })}</span>
      <Space size={6}>
        <CompactButton size="small" icon={<EditOutlined />} onClick={() => openBatchEditScreen(selectedMods())}>
          {t("mods.bulkBar.edit")}
        </CompactButton>
        <Dropdown trigger={["click"]} menu={{ items: bulkFixMenuItems() }}>
          <CompactButton size="small" icon={<ThunderboltOutlined />}>{t("contextMenu.runFix")}</CompactButton>
        </Dropdown>
        <CompactButton size="small" icon={<DeleteOutlined style={{ color: "var(--color-error)" }} />} onClick={openBulkDelete}>
          {t("mods.bulkBar.delete")}
        </CompactButton>
      </Space>
      <CompactButton size="small" type="text" className="mod-bulk-bar__clear" icon={<CloseOutlined />} onClick={() => onClearSelection?.()}>
        {t("mods.bulkBar.clear")}
      </CompactButton>
    </div>
  );

  const getContextMenuItems = (mod: ModInfo): ContextMenuItem[] => {
    const isMultiSelect = selectedModIds.length > 1 && selectedModIds.includes(mod.id);

    // For orphaned mods, only show Open Cache Folder and Delete Cache
    if (mod.isOrphaned) {
      return [
        {
          key: "view-cache",
          label: t("contextMenu.openCacheFolder"),
          icon: <FolderOpenOutlined />,
          disabled: !mod?.hasCache,
          onClick: async () => {
            if (mod?.cachePath) {
              try {
                await systemService.openDirectory(mod.cachePath);
                notification.success(t("mods.notifications.openedCache"));
              } catch (error: unknown) {
                notification.error(t("mods.notifications.openCacheFailed"));
              }
            }
          },
        },
        {
          key: "delete-cache",
          label: t("contextMenu.deleteCache"),
          icon: <ClearOutlined />,
          danger: true,
          disabled: !mod?.hasCache,
          onClick: () => handleDeleteCachedMod(mod),
        },
      ];
    }

    // Batch operations menu for multi-select
    if (isMultiSelect) {
      // Filter selected mods to only those with cache
      const selectedModsWithCache = mods.filter(m =>
        selectedModIds.includes(m.id) && m.hasCache
      );
      const cacheCount = selectedModsWithCache.length;

      return [
        {
          key: "batch-edit",
          label: t("contextMenu.batchEditMods", { count: selectedModIds.length }),
          icon: <EditOutlined />,
          onClick: () => {
            const selectedMods = mods.filter(m => selectedModIds.includes(m.id));
            openBatchEditScreen(selectedMods);
          },
        },
        { ...buildFixSubmenu([...selectedModIds]), key: "batch-run-fix", label: t("contextMenu.runFixSelected", { count: selectedModIds.length }) },
        {
          key: "batch-merge",
          label: t("contextMenu.mergeSelected", { count: selectedModIds.length }),
          icon: <MergeCellsOutlined />,
          onClick: () => setMergeDialogMods(mods.filter((m) => selectedModIds.includes(m.id))),
        },
        { type: "divider" as const },
        {
          key: "batch-delete-caches",
          label: cacheCount > 0
            ? t("contextMenu.deleteAllCaches", { count: cacheCount })
            : t("contextMenu.deleteAllCachesDisabled"),
          icon: <ClearOutlined />,
          disabled: cacheCount === 0,
          onClick: async () => {
            if (!selectedProfileId) {
              notification.error(t("errors.noProfileSelected"));
              return;
            }

            // Filter to only mods with cache (button is disabled when none have cache)
            const shasWithCache = selectedModsWithCache.map(m => m.id);
            const profileId = selectedProfileId;

            try {
              const result = await modService.batchDeleteCaches(profileId, shasWithCache);

              if (result.successCount > 0) {
                notification.success(
                  t("mods.notifications.batchCacheDeleted", { count: result.successCount })
                );
                await refreshMods(profileId);
              }

              if (result.failedCount > 0) {
                notification.error(
                  t("mods.notifications.batchCacheDeleteFailed", { count: result.failedCount })
                );
              }
            } catch (error: unknown) {
              notification.error(t("mods.notifications.batchCacheDeleteFailed", { count: shasWithCache.length }));
            }
          },
        },
        {
          key: "batch-delete-mods",
          label: t("contextMenu.deleteAllMods", { count: selectedModIds.length }),
          icon: <DeleteOutlined />,
          danger: true,
          onClick: () => {
            // Show confirmation dialog with selected mod count
            setDeleteConfirm({
              visible: true,
              mod: {
                ...mod,
                name: t("mods.notifications.selectedMods", { count: selectedModIds.length }),
              },
            });
          },
        },
      ];
    }

    // Regular single-mod context menu
    return [
    // Group 1: Load/Unload and Edit Operations
    !mod.isLoaded
      ? {
          key: "load",
          label: t("contextMenu.loadMod"),
          icon: <PlayCircleOutlined />,
          onClick: () => onLoad(mod.id),
        }
      : {
          key: "unload",
          label: t("contextMenu.unloadMod"),
          icon: <PauseCircleOutlined />,
          onClick: () => onUnload(mod.id),
        },
    {
      key: "edit",
      label: t("contextMenu.editMod"),
      icon: <EditOutlined />,
      onClick: () => {
        if (onEdit) {
          onEdit(mod);
        } else {
          notification.info(
            t("mods.notifications.editMod", { name: mod.name }),
          );
        }
      },
    },
    {
      key: "edit-ini",
      label: t("contextMenu.editIni"),
      icon: <FileTextOutlined />,
      disabled: !mod?.hasCache,
      onClick: () => setIniEditorMod(mod),
    },
    {
      key: "optimize",
      label: t("contextMenu.optimize"),
      icon: <CompressOutlined />,
      disabled: !mod?.hasCache,
      onClick: () => setOptimizeMod(mod),
    },
    buildFixSubmenu([mod.id]),
    { type: "divider" as const },

    // Group 2: Copy Operations
    {
      key: "copy-id",
      label: t("contextMenu.copyId"),
      icon: <CopyOutlined />,
      onClick: () => { void copyToClipboard(mod.id, t("mods.notifications.idCopied")); },
    },
    {
      key: "copy-name",
      label: t("contextMenu.copyName"),
      icon: <CopyOutlined />,
      onClick: () => { void copyToClipboard(mod.name, t("mods.notifications.nameCopied")); },
    },
    { type: "divider" as const },

    // Group 3: File Operations
    {
      key: "view-archive",
      label: t("contextMenu.openModFolder"),
      icon: <FolderOpenOutlined />,
      disabled: !mod?.archiveFolderPath || !mod?.isAvailable,
      onClick: async () => {
        if (mod?.archiveFolderPath && mod?.id) {
          try {
            // Construct the archive file path (archives are stored without extension)
            const archiveFilePath = `${mod.archiveFolderPath}\\${mod.id}`;
            await systemService.openFileInExplorer(archiveFilePath);
            notification.success(t("mods.notifications.openedModFolder"));
          } catch (error: unknown) {
            notification.error(t("mods.notifications.openModFolderFailed"));
          }
        }
      },
    },
    {
      key: "view-cache",
      label: t("contextMenu.openCacheFolder"),
      icon: <FolderOpenOutlined />,
      disabled: !mod?.hasCache,
      onClick: async () => {
        if (mod?.cachePath) {
          try {
            await systemService.openDirectory(mod.cachePath);
            notification.success(t("mods.notifications.openedCache"));
          } catch (error: unknown) {
            notification.error(t("mods.notifications.openCacheFailed"));
          }
        }
      },
    },
    {
      key: "view-preview",
      label: t("contextMenu.openPreviewFolder"),
      icon: <FolderOpenOutlined />,
      disabled: !mod?.hasPreviewFolder,
      onClick: async () => {
        if (mod?.previewFolderPath) {
          try {
            await systemService.openDirectory(mod.previewFolderPath);
            notification.success(t("mods.notifications.openedPreview"));
          } catch (error: unknown) {
            notification.error(t("mods.notifications.openPreviewFailed"));
          }
        }
      },
    },
    {
      key: "update-archive",
      label: t("contextMenu.updateArchive"),
      icon: busyModIds.has(mod.id) ? <SyncOutlined spin /> : <SyncOutlined />,
      disabled: !mod?.hasCache || busyModIds.has(mod.id),
      onClick: () => handleUpdateArchive(mod),
    },
    {
      key: "update-mod",
      label: t("contextMenu.updateMod"),
      icon: <ImportOutlined />,
      onClick: () => handleUpdateMod(mod),
    },
    { type: "divider" as const },

    // Group 4: Destructive Operations
    {
      key: "delete-cache",
      label: t("contextMenu.deleteCache"),
      icon: <ClearOutlined />,
      disabled: !mod?.hasCache,
      onClick: () => handleDeleteCachedMod(mod),
    },
    {
      key: "delete",
      label: t("contextMenu.deleteMod"),
      icon: <DeleteOutlined />,
      danger: true,
      onClick: () => handleShowDeleteConfirm(mod),
    },
  ];
  };

  return (
    <>
      {/* Bulk-action bar — appears when multiple mods are selected */}
      {bulkBar}
      {/* Overlay spinner that doesn't replace content */}
      {loading && (
        <div className="mod-list-loading-overlay">
          <Spin size="large" />
        </div>
      )}
      <div className="mod-list-container">
        {/* Mod list content */}
        <div
          className={classNames("mod-list-content", {
            "mod-list-content-loading": loading,
          })}
        >
          {displayedMods.map((mod) => {
            const isPrimarySelection = selectedMod?.id === mod.id;
            const isInMultiSelection = selectedModIds.includes(mod.id);
            const isBusy = busyModIds.has(mod.id);
            // Broken state: the source archive is gone, so the mod can't be loaded (orphaned cache-only
            // mods get their own treatment below).
            const isUnavailable = !mod.isAvailable && !mod.isOrphaned && !mod.isLoaded;

            return (
              <ModListItem
                key={mod.id}
                mod={mod}
                isPrimarySelection={isPrimarySelection}
                isInMultiSelection={isInMultiSelection}
                isBusy={isBusy}
                isUnavailable={isUnavailable}
                selectedModIds={selectedModIds}
                gameUpdatedUtc={gameUpdatedUtc}
                onRowClick={onRowClick}
                onLoad={onLoad}
                onUnload={onUnload}
                onEdit={onEdit}
                onContextMenu={(m, e) => {
                  setContextMenuMod(m);
                  menuState.show(e);
                }}
              />
            );
          })}
        </div>
      </div>
      {/* Bottom spacer: represents unrendered items so the scroll container has
          the correct total height. The intersection observer fires when the user
          scrolls near it, triggering the next batch. Also allows scrollTo() from
          the parent to land at the right position before items are rendered. */}
      {effectiveDisplayCount < mods.length && (
        <div
          ref={observerTarget}
          className="mod-list-bottom-spacer"
          style={{ height: (mods.length - effectiveDisplayCount) * ITEM_HEIGHT }}
          aria-hidden="true"
        />
      )}
      {/* Show total count */}
      {effectiveDisplayCount >= mods.length && mods.length > 50 && (
        <div className="mod-list-total-count">
          {t("mods.list.showingAll", { count: mods.length })}
        </div>
      )}
      {/* Context menu */}
      {contextMenuMod && (
        <ContextMenu
          items={getContextMenuItems(contextMenuMod)}
          visible={menuState.visible}
          position={menuState.position}
          onClose={() => {
            menuState.hide();
            setContextMenuMod(undefined);
          }}
        />
      )}
      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        visible={deleteConfirm.visible}
        title={t("contextMenu.deleteMod")}
        content={t("mods.notifications.confirmDelete", {
          name: deleteConfirm.mod?.name || "",
        })}
        okText={t("common.delete")}
        cancelText={t("common.cancel")}
        okType="danger"
        onOk={handleConfirmDelete}
        onCancel={() => setDeleteConfirm({ visible: false })}
      />
      {/* Batch Edit Screen */}
      <BatchEditModsScreen />
      {/* Fix Tools manager — add/remove fix tools; opened from the right-click "Manage" entry.
          compact = the narrow, list-only context view (settings live in the full Tools-grid view). */}
      <ModFixTool
        compact
        visible={showFixManager}
        onClose={() => { setShowFixManager(false); void loadFixTools(); }}
      />
      {/* General .ini editor — opened from the right-click "Edit .ini files" entry (extracted mods only) */}
      <ModIniEditor
        visible={!!iniEditorMod}
        mod={iniEditorMod}
        onClose={() => setIniEditorMod(undefined)}
      />
      {/* Duplicate-asset optimizer — right-click "Optimize" (extracted mods only) */}
      <ModOptimizeDialog
        visible={!!optimizeMod}
        modId={optimizeMod?.id}
        modName={optimizeMod?.name}
        onClose={() => setOptimizeMod(undefined)}
      />
      {/* Mod-merge — combine the selected mods into a new cycle-merged mod */}
      <MergeModsDialog
        visible={!!mergeDialogMods}
        mods={mergeDialogMods ?? []}
        onClose={() => setMergeDialogMods(undefined)}
      />
    </>
  );
};
