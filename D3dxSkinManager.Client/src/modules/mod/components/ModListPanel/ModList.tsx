import { notification } from "../../../../shared/utils/notification";
import React, { useState, useRef, useCallback } from "react";
import classNames from 'classnames';
import { Tag, Button, Space, Spin } from "antd";
import {
  PlayCircleOutlined,
  PauseCircleOutlined,
  EditOutlined,
  DeleteOutlined,
  ExportOutlined,
  FolderOpenOutlined,
  FileZipOutlined,
  ClearOutlined,
  CopyOutlined,
} from "@ant-design/icons";
import { ModInfo } from "../../../../shared/types/mod.types";
import { fileDialogService } from "../../../../shared/services/systemService";
import { modService } from "../../services/modService";
import { cacheService } from "../../../tool/services/cacheService";
import { GradingTag } from "../GradingTag";
import { TagChip } from "../../../../shared/components/TagChip";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { useMods } from "../../hooks/useMods";
import { useDelayedLoading } from "../../../../shared/hooks/useDelayedLoading";
import { ConfirmDialog } from "../../../../shared/components/dialogs";
import {
  ContextMenu,
  ContextMenuItem,
  useContextMenu,
} from "../../../../shared/components/menu";
import { refreshMods } from "../../operations/modOperations";
import { useTranslation } from "react-i18next";
import "./ModList.css";

interface ModListProps {
  mods: ModInfo[];
  loading: boolean;
  onLoad: (sha: string) => void;
  onUnload: (sha: string) => void;
  onDelete: (sha: string, name: string) => void;
  onEdit?: (mod: ModInfo) => void;
  onRowClick?: (mod: ModInfo) => void;
  selectedMod?: ModInfo;
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
}) => {
  const { t } = useTranslation();
  const [displayCount, setDisplayCount] = useState(50);
  const observerTarget = useRef<HTMLDivElement>(null);
  const { state: profileState } = useProfile();
  const { updateModLocal } = useMods();
  const { loading: deletingCache, execute: executeDeleteCache } = useDelayedLoading(200);
  const menuState = useContextMenu();
  const [contextMenuMod, setContextMenuMod] = useState<ModInfo>();
  const [checkedPaths, setCheckedPaths] = useState<{
    originalPath: string | undefined;
    cachePath: string | undefined;
    thumbnailPath: string | undefined;
  }>();
  const [deleteConfirm, setDeleteConfirm] = useState<{ visible: boolean; mod?: ModInfo }>({ visible: false });

  // Intersection observer for infinite scroll
  const handleObserver = useCallback(
    (entries: IntersectionObserverEntry[]) => {
      const target = entries[0];
      if (target.isIntersecting && displayCount < mods.length) {
        setDisplayCount((prev) => Math.min(prev + 50, mods.length));
      }
    },
    [displayCount, mods.length],
  );

  React.useEffect(() => {
    const observer = new IntersectionObserver(handleObserver, {
      root: null,
      rootMargin: "100px",
      threshold: 0.1,
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

  // Reset display count when mods change
  React.useEffect(() => {
    setDisplayCount(50);
  }, [mods]);

  const displayedMods = mods.slice(0, displayCount);

  /**
   * Show delete mod confirmation dialog
   */
  const handleShowDeleteConfirm = (mod: ModInfo) => {
    setDeleteConfirm({ visible: true, mod });
  };

  /**
   * Execute delete mod after confirmation
   */
  const handleConfirmDelete = async () => {
    const mod = deleteConfirm.mod;
    if (!mod) {
      setDeleteConfirm({ visible: false });
      return;
    }

    await onDelete(mod.sha, mod.name);
    setDeleteConfirm({ visible: false });
  };

  /**
   * Delete cached mod (no confirmation needed)
   * Deletes cache for both loaded and unloaded mods:
   * - Loaded: {SHA}/ directory
   * - Unloaded: DISABLED-{SHA}/ directory
   * Updates local mod state to set isLoaded=false without backend fetch.
   * Uses delayed loading to show spinner only if operation takes >100ms.
   */
  const handleDeleteCachedMod = async (mod: ModInfo) => {
    if (!profileState.selectedProfile?.id) {
      notification.error(t('mods.notifications.noProfileSelected'));
      return;
    }

    const profileId = profileState.selectedProfile.id;

    await executeDeleteCache(async () => {
      try {
        // Delete the cache
        const success = await cacheService.deleteCacheItem(profileId, mod.sha);
        if (success) {
          notification.success(t('mods.notifications.cacheDeleted', { name: mod.name }));

          // Refresh from backend to update hasCache and other properties
          await refreshMods(profileId);

          // Refresh checked paths after deletion
          if (contextMenuMod?.sha === mod.sha) {
            try {
              const paths = await modService.checkFilePaths(profileId, mod.sha);
              setCheckedPaths(paths);
            } catch (error: unknown) {
                          }
          }
        } else {
          notification.error(t('mods.notifications.deleteCacheFailed'));
                  }
      } catch (error: unknown) {
        const errorMessage = error instanceof Error ? error.message : 'Unknown error';
        notification.error(`${t('mods.notifications.deleteCacheFailed')}: ${errorMessage}`);
              }
    });
  };

  const getContextMenuItems = (mod: ModInfo): ContextMenuItem[] => [
    // Group 1: Load/Unload Operations
    !mod.isLoaded
      ? {
          key: "load",
          label: t('contextMenu.loadMod'),
          icon: <PlayCircleOutlined />,
          onClick: () => onLoad(mod.sha),
        }
      : {
          key: "unload",
          label: t('contextMenu.unloadMod'),
          icon: <PauseCircleOutlined />,
          onClick: () => onUnload(mod.sha),
        },
    { type: "divider" as const },

    // Group 2: Edit & Export Operations
    {
      key: "edit",
      label: t('contextMenu.editModInfo'),
      icon: <EditOutlined />,
      onClick: () => {
        if (onEdit) {
          onEdit(mod);
        } else {
          notification.info(t('mods.notifications.editMod', { name: mod.name }));
        }
      },
    },
    {
      key: "export",
      label: t('contextMenu.exportMod'),
      icon: <ExportOutlined />,
      onClick: async () => {
        const result = await fileDialogService.saveFileDialog({
          title: t('dialogs.exportMod.title'),
          defaultPath: `${mod.name}.zip`,
          filters: [
            { name: t('dialogs.exportMod.zipArchive'), extensions: ["zip"] },
            { name: t('dialogs.exportMod.allFiles'), extensions: ["*"] },
          ],
        });

        if (result.success && result.filePath && profileState.selectedProfile) {
          try {
            await modService.exportMod(
              profileState.selectedProfile.id,
              mod.sha,
              result.filePath,
            );
            notification.success(t('mods.notifications.exportSuccess', { name: mod.name }));
          } catch (error: unknown) {
            notification.error(t('mods.notifications.exportFailed'));
          }
        } else if (result.error) {
          notification.error(result.error);
        }
      },
    },
    { type: "divider" as const },

    // Group 3: Copy Operations
    {
      key: "copy-sha",
      label: t('contextMenu.copySHA'),
      icon: <CopyOutlined />,
      onClick: () => {
        navigator.clipboard.writeText(mod.sha);
        notification.success(t('mods.notifications.shaCopied'));
      },
    },
    {
      key: "copy-name",
      label: t('contextMenu.copyName'),
      icon: <CopyOutlined />,
      onClick: () => {
        navigator.clipboard.writeText(mod.name);
        notification.success(t('mods.notifications.nameCopied'));
      },
    },
    { type: "divider" as const },

    // Group 4: File Operations
    {
      key: "view-original",
      label: t('contextMenu.viewOriginalFile'),
      icon: <FileZipOutlined />,
      disabled: !checkedPaths?.originalPath,
      onClick: async () => {
        if (checkedPaths?.originalPath) {
          try {
            await fileDialogService.openFileInExplorer(
              checkedPaths.originalPath,
            );
            notification.success(t('mods.notifications.openedOriginal'));
          } catch (error: unknown) {
            notification.error(t('mods.notifications.openOriginalFailed'));
          }
        }
      },
    },
    {
      key: "view-cache",
      label: t('contextMenu.openCacheFolder'),
      icon: <FolderOpenOutlined />,
      disabled: !checkedPaths?.cachePath,
      onClick: async () => {
        if (checkedPaths?.cachePath) {
          try {
            await fileDialogService.openDirectory(checkedPaths.cachePath);
            notification.success(t('mods.notifications.openedCache'));
          } catch (error: unknown) {
            notification.error(t('mods.notifications.openCacheFailed'));
          }
        }
      },
    },
    {
      key: "view-preview",
      label: t('contextMenu.openPreviewFolder'),
      icon: <FolderOpenOutlined />,
      disabled: !checkedPaths?.thumbnailPath,
      onClick: async () => {
        if (checkedPaths?.thumbnailPath) {
          try {
            await fileDialogService.openDirectory(checkedPaths.thumbnailPath);
            notification.success(t('mods.notifications.openedPreview'));
          } catch (error: unknown) {
            notification.error(t('mods.notifications.openPreviewFailed'));
          }
        }
      },
    },
    { type: "divider" as const },

    // Group 5: Destructive Operations
    {
      key: "delete-cache",
      label: t('contextMenu.deleteCachedMod'),
      icon: <ClearOutlined />,
      disabled: !checkedPaths?.cachePath,
      onClick: () => handleDeleteCachedMod(mod),
    },
    {
      key: "delete",
      label: t('contextMenu.deleteMod'),
      icon: <DeleteOutlined />,
      danger: true,
      onClick: () => handleShowDeleteConfirm(mod),
    },
  ];

  return (
    <>
      {loading || deletingCache ? (
        <div className="mod-list-loading-container">
          <Spin size="large" />
        </div>
      ) : (
        <>
          {displayedMods.map((mod) => {
            const isSelected = selectedMod?.sha === mod.sha;

            return (
              <div
                key={mod.sha}
                data-mod-sha={mod.sha}
                draggable
                onDragStart={(e) => {
                  e.dataTransfer.setData("application/mod-sha", mod.sha);
                  e.dataTransfer.effectAllowed = "move";
                }}
                className={classNames('mod-list-item', { 'mod-list-item-selected': isSelected })}
                onClick={() => {
                  onRowClick?.(mod);
                }}
                onContextMenu={async (e) => {
                  e.preventDefault();
                  setContextMenuMod(mod);

                  // Check file paths on-demand when opening context menu
                  if (profileState.selectedProfile?.id) {
                    try {
                      const paths = await modService.checkFilePaths(
                        profileState.selectedProfile.id,
                        mod.sha,
                      );
                      setCheckedPaths(paths);
                    } catch (error: unknown) {
                                            setCheckedPaths({
                        originalPath: undefined,
                        cachePath: undefined,
                        thumbnailPath: undefined,
                      });
                    }
                  }

                  menuState.show(e);
                }}
                onDoubleClick={() => {
                  if (!mod.isLoaded) {
                    onLoad(mod.sha);
                  } else {
                    onUnload(mod.sha);
                  }
                }}
              >
                <div className="mod-list-item-content">
                  <div className="mod-list-item-header">
                    <span className="mod-list-item-name">{mod.name}</span>
                    {mod.isLoaded && (
                      <Tag color="success" className="mod-list-item-loaded-tag">
                        {t('mods.list.loaded')}
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
                      {mod.categoryName || t('category.unclassified')}
                    </Tag>
                    {mod.tags &&
                      mod.tags.slice(0, 3).map((tagName) => {
                        // Use pre-loaded tag data if available
                        const tagData = mod.tagsWithMetadata?.find(t => t.name === tagName);
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
                        +{mod.tags.length - 3} {t('mods.list.more')}
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
                          onUnload(mod.sha);
                        } else {
                          onLoad(mod.sha);
                        }
                      }}
                      title={mod.isLoaded ? t('mods.list.unloadMod') : t('mods.list.loadMod')}
                      className="mod-list-item-action-button"
                    />
                    <Button
                      type="text"
                      size="middle"
                      icon={<EditOutlined className="mod-list-item-action-icon" />}
                      onClick={(e) => {
                        e.stopPropagation();
                        onEdit?.(mod);
                      }}
                      title={t('mods.list.editMod')}
                      className="mod-list-item-action-button"
                    />
                  </div>
                }
              </div>
            );
          })}
        </>
      )}

      {/* Infinite scroll trigger */}
      {displayCount < mods.length && (
        <div ref={observerTarget} className="mod-list-scroll-trigger">
          {t('mods.list.loadingMore')}
        </div>
      )}

      {/* Show total count */}
      {displayCount >= mods.length && mods.length > 50 && (
        <div className="mod-list-total-count">
          {t('mods.list.showingAll', { count: mods.length })}
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
        title={t('contextMenu.deleteMod')}
        content={t('mods.notifications.confirmDelete', { name: deleteConfirm.mod?.name || '' })}
        okText={t('common.delete')}
        cancelText={t('common.cancel')}
        okType="danger"
        onOk={handleConfirmDelete}
        onCancel={() => setDeleteConfirm({ visible: false })}
      />
    </>
  );
};
