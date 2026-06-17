import { copyToClipboard } from "../../../../shared/utils/clipboardHelper";
import { notification } from "../../../../shared/utils/notification";
import React, { useState, useRef, useCallback } from "react";
import classNames from "classnames";
import { Tag, Button, Space, Spin } from "antd";
import {
  PlayCircleOutlined,
  PauseCircleOutlined,
  EditOutlined,
  DeleteOutlined,
  FolderOpenOutlined,
  ClearOutlined,
  CopyOutlined,
  SyncOutlined,
} from "@ant-design/icons";
import { ModInfo } from "../../../../shared/types/mod.types";
import { systemService } from "../../../../shared/services/ipc";
import { modService } from "../../../../shared/services/ipc";
import { GradingTag } from "../GradingTag";
import { TagChip } from "../../../../shared/components/TagChip";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { ConfirmDialog } from "../../../../shared/components/dialogs";
import {
  ContextMenu,
  ContextMenuItem,
  useContextMenu,
} from "../../../../shared/components/menu";
import { refreshMods } from "../../operations/modOperations";
import { useModsStore } from "../../store/modsStore";
import { useTranslation } from "react-i18next";
import { BatchEditModsScreen } from "../BatchEditScreen";
import { useMods } from "../../hooks/useMods";
import "./ModList.css";

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
  onBeforeReload,
  onAfterReload,
  minDisplayCount = 0,
}) => {
  const { t } = useTranslation();
  const [displayCount, setDisplayCount] = useState(50);
  // effectiveDisplayCount is the actual number of items to render:
  // at least displayCount (from natural scroll), but bumped up when parent
  // forces a specific item into view (e.g., scrolling to a loaded mod)
  const effectiveDisplayCount = Math.max(displayCount, minDisplayCount);
  const observerTarget = useRef<HTMLDivElement>(null);
  const { selectedProfileId } = useProfile();
  const { openBatchEditScreen } = useMods();
  const menuState = useContextMenu();
  const [contextMenuMod, setContextMenuMod] = useState<ModInfo>();
  const [deleteConfirm, setDeleteConfirm] = useState<{
    visible: boolean;
    mod?: ModInfo;
  }>({ visible: false });
  const busyModIds = useModsStore((s) => s.busyModIds);

  // Intersection observer for infinite scroll
  const handleObserver = useCallback(
    (entries: IntersectionObserverEntry[]) => {
      const target = entries[0];
      if (target.isIntersecting && effectiveDisplayCount < mods.length) {
        // Load next batch starting from effectiveDisplayCount (not prev displayCount)
        // so forced renders via minDisplayCount are accounted for
        setDisplayCount(Math.min(effectiveDisplayCount + 50, mods.length));
      }
    },
    [effectiveDisplayCount, mods.length],
  );

  React.useEffect(() => {
    const observer = new IntersectionObserver(handleObserver, {
      root: null,
      rootMargin: "100px",
      // threshold: 0 fires as soon as any pixel of the spacer enters the viewport.
      // A non-zero threshold (e.g. 0.1) would require 10% of the spacer to be
      // visible — unusable for a tall placeholder that can be thousands of pixels.
      threshold: 0,
    });

    const currentTarget = observerTarget.current;
    if (currentTarget) {
      observer.observe(currentTarget);
    }

    return () => {
      if (currentTarget) {
        observer.unobserve(currentTarget);
      }
    };
  }, [handleObserver]);

  // Reset display count when mods array length changes (category change, search, etc.)
  // Don't reset on property updates (like isLoaded) to prevent flash during infinite scroll
  const modsLength = mods.length;
  React.useEffect(() => {
    setDisplayCount(50);
  }, [modsLength]);

  // Save scroll position before reload and restore after
  React.useEffect(() => {
    if (loading) {
      onBeforeReload?.();
    } else {
      onAfterReload?.();
    }
  }, [loading, onBeforeReload, onAfterReload]);

  const displayedMods = mods.slice(0, effectiveDisplayCount);

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
        const result = await modService.batchDeleteMods(profileId, selectedModIds);

        if (result.successCount > 0) {
          notification.success(
            t("mods.notifications.batchDeleteSuccess", { count: result.successCount })
          );
        }

        if (result.failedCount > 0) {
          notification.error(
            t("mods.notifications.batchDeleteFailed", { count: result.failedCount })
          );
        }
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

            return (
              <div
                key={mod.id}
                data-mod-id={mod.id}
                draggable
                onDragStart={(e) => {
                  // If this mod is part of multi-selection, drag all selected mods
                  if (isInMultiSelection && selectedModIds.length > 1) {
                    e.dataTransfer.setData(
                      "application/mod-ids",
                      JSON.stringify(selectedModIds),
                    );
                  } else {
                    // Single mod drag
                    e.dataTransfer.setData("application/mod-id", mod.id);
                  }
                  e.dataTransfer.effectAllowed = "move";
                }}
                className={classNames("mod-list-item", {
                  "mod-list-item-selected": isPrimarySelection,
                  "mod-list-item-multi-selected":
                    isInMultiSelection && !isPrimarySelection,
                })}
                onClick={(e) => {
                  onRowClick?.(mod, e);
                }}
                onContextMenu={(e) => {
                  e.preventDefault();
                  setContextMenuMod(mod);
                  menuState.show(e);
                }}
                onDoubleClick={() => {
                  if (!mod.isLoaded) {
                    onLoad(mod.id);
                  } else {
                    onUnload(mod.id);
                  }
                }}
              >
                <div className="mod-list-item-content">
                  <div className="mod-list-item-header">
                    <span className="mod-list-item-name">
                      {mod.isOrphaned ? t('mods.list.unmanaged', { id: mod.name }) : mod.name}
                    </span>
                    {isBusy && !mod.isLoading && (
                      <Tag color="processing" className="mod-list-item-loaded-tag">
                        {t("mods.list.busy")}
                      </Tag>
                    )}
                    {mod.isLoading && (
                      <Tag color="warning" className="mod-list-item-loaded-tag">
                        {t("mods.list.loading")}
                      </Tag>
                    )}
                    {mod.isLoaded && !mod.isLoading && !isBusy && (
                      <Tag color="success" className="mod-list-item-loaded-tag">
                        {t("mods.list.loaded")}
                      </Tag>
                    )}
                  </div>
                  <Space size={[8, 4]} wrap className="mod-list-item-tags">
                    {mod.grading && <GradingTag grading={mod.grading} />}
                    {mod.author && mod.author.trim() !== "" && (
                      <Tag color="blue" className="mod-list-item-tag">
                        {mod.author}
                      </Tag>
                    )}
                    <Tag color="geekblue" className="mod-list-item-tag">
                      {mod.categoryName || t("category.unclassified")}
                    </Tag>
                    {mod.tags &&
                      mod.tags.slice(0, 3).map((tagName) => {
                        // Use pre-loaded tag data if available
                        const tagData = mod.tagsWithMetadata?.find(
                          (t) => t.name === tagName,
                        );
                        return (
                          <TagChip
                            key={tagName}
                            tagName={tagName}
                            tag={tagData}
                            size="small"
                            className="mod-list-item-tag"
                          />
                        );
                      })}
                    {mod.tags && mod.tags.length > 3 && (
                      <Tag className="mod-list-item-tag" color="default">
                        +{mod.tags.length - 3} {t("mods.list.more")}
                      </Tag>
                    )}
                  </Space>
                </div>
                {
                  <div className="mod-list-item-actions">
                    <Button
                      type="text"
                      size="middle"
                      icon={
                        mod.isLoaded ? (
                          <PauseCircleOutlined className="mod-list-item-action-icon" />
                        ) : (
                          <PlayCircleOutlined className="mod-list-item-action-icon" />
                        )
                      }
                      onClick={(e) => {
                        e.stopPropagation();
                        if (mod.isLoaded) {
                          onUnload(mod.id);
                        } else {
                          onLoad(mod.id);
                        }
                      }}
                      title={
                        mod.isLoaded
                          ? t("mods.list.unloadMod")
                          : t("mods.list.loadMod")
                      }
                      className="mod-list-item-action-button"
                    />
                    <Button
                      type="text"
                      size="middle"
                      icon={
                        <EditOutlined className="mod-list-item-action-icon" />
                      }
                      onClick={(e) => {
                        e.stopPropagation();
                        onEdit?.(mod);
                      }}
                      title={t("mods.list.editMod")}
                      className="mod-list-item-action-button"
                    />
                  </div>
                }
              </div>
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
    </>
  );
};
