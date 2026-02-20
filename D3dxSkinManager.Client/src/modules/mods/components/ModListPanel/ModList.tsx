import { notification } from "../../../../shared/utils/notification";
import React, { useState, useRef, useCallback } from "react";
import { Tag, Button, Space, Spin } from "antd";
import {
  PlayCircleOutlined,
  PauseCircleOutlined,
  EditOutlined,
  DeleteOutlined,
  ExportOutlined,
  FolderOpenOutlined,
  FileZipOutlined,
} from "@ant-design/icons";
import { ModInfo } from "../../../../shared/types/mod.types";
import { fileDialogService } from "../../../../shared/services/systemService";
import { modService } from "../../services/modService";
import { GradingTag } from "../../../../shared/components/common/GradingTag";
import { useProfile } from "../../../../shared/context/ProfileContext";
import {
  ContextMenu,
  ContextMenuItem,
  useContextMenu,
} from "../../../../shared/components/menu";
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
  selectedMod?: ModInfo | null;
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
  const menuState = useContextMenu();
  const [contextMenuMod, setContextMenuMod] = useState<ModInfo | null>(null);
  const [checkedPaths, setCheckedPaths] = useState<{
    originalPath: string | null;
    workPath: string | null;
    thumbnailPath: string | null;
  } | null>(null);

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

  const getContextMenuItems = (mod: ModInfo): ContextMenuItem[] => [
    // Load/Unload
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

    // Edit & Export
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
          } catch (error) {
            notification.error(t('mods.notifications.exportFailed'));
          }
        } else if (result.error) {
          notification.error(result.error);
        }
      },
    },
    { type: "divider" as const },

    // Copy operations
    {
      key: "copy-sha",
      label: t('contextMenu.copySHA'),
      onClick: () => {
        navigator.clipboard.writeText(mod.sha);
        notification.success(t('mods.notifications.shaCopied'));
      },
    },
    {
      key: "copy-name",
      label: t('contextMenu.copyName'),
      onClick: () => {
        navigator.clipboard.writeText(mod.name);
        notification.success(t('mods.notifications.nameCopied'));
      },
    },
    { type: "divider" as const },

    // File viewing operations
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
          } catch (error) {
            notification.error(t('mods.notifications.openOriginalFailed'));
          }
        }
      },
    },
    {
      key: "view-work",
      label: t('contextMenu.openWorkFolder'),
      icon: <FolderOpenOutlined />,
      disabled: !checkedPaths?.workPath,
      onClick: async () => {
        if (checkedPaths?.workPath) {
          try {
            await fileDialogService.openDirectory(checkedPaths.workPath);
            notification.success(t('mods.notifications.openedWork'));
          } catch (error) {
            notification.error(t('mods.notifications.openWorkFailed'));
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
          } catch (error) {
            notification.error(t('mods.notifications.openPreviewFailed'));
          }
        }
      },
    },
    { type: "divider" as const },

    // Delete
    {
      key: "delete",
      label: t('contextMenu.deleteMod'),
      icon: <DeleteOutlined />,
      danger: true,
      onClick: () => onDelete(mod.sha, mod.name),
    },
  ];

  const handleUnloadClick = (mod: ModInfo) => {
    // Find the loaded mod for this object and unload it
    const loadedMod = mods.find(
      (m) => m.category === mod.category && m.isLoaded,
    );
    if (loadedMod) {
      onUnload(loadedMod.sha);
      notification.success(t('mods.notifications.unloadingObject', { category: mod.category }));
    }
  };

  return (
    <div className="mod-list-container">
      {loading ? (
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
                onDragStart={(e) => {
                  e.dataTransfer.setData("application/mod-sha", mod.sha);
                  e.dataTransfer.effectAllowed = "move";
                }}
                className={`mod-list-item ${isSelected ? 'mod-list-item-selected' : ''}`}
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
                    } catch (error) {
                      console.error("Failed to check file paths:", error);
                      setCheckedPaths({
                        originalPath: null,
                        workPath: null,
                        thumbnailPath: null,
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
                    {mod.category && mod.category.trim() !== "" && (
                      <Tag color="geekblue" className="mod-list-item-tag">
                        {mod.category}
                      </Tag>
                    )}
                    {mod.tags &&
                      mod.tags.slice(0, 3).map((tag) => (
                        <Tag key={tag} className="mod-list-item-tag">
                          {tag}
                        </Tag>
                      ))}
                    {mod.tags && mod.tags.length > 3 && (
                      <Tag className="mod-list-item-tag">
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
            setContextMenuMod(null);
          }}
        />
      )}
    </div>
  );
};
