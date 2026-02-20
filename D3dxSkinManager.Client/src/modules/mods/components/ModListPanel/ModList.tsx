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
          label: "Load Mod",
          icon: <PlayCircleOutlined />,
          onClick: () => onLoad(mod.sha),
        }
      : {
          key: "unload",
          label: "Unload Mod",
          icon: <PauseCircleOutlined />,
          onClick: () => onUnload(mod.sha),
        },
    { type: "divider" as const },

    // Edit & Export
    {
      key: "edit",
      label: "Edit Mod Information",
      icon: <EditOutlined />,
      onClick: () => {
        if (onEdit) {
          onEdit(mod);
        } else {
          notification.info(`Edit mod: ${mod.name}`);
        }
      },
    },
    {
      key: "export",
      label: "Export Mod File",
      icon: <ExportOutlined />,
      onClick: async () => {
        const result = await fileDialogService.saveFileDialog({
          title: "Export Mod",
          defaultPath: `${mod.name}.zip`,
          filters: [
            { name: "ZIP Archive", extensions: ["zip"] },
            { name: "All Files", extensions: ["*"] },
          ],
        });

        if (result.success && result.filePath && profileState.selectedProfile) {
          try {
            await modService.exportMod(
              profileState.selectedProfile.id,
              mod.sha,
              result.filePath,
            );
            notification.success(`Exported mod: ${mod.name}`);
          } catch (error) {
            notification.error("Failed to export mod");
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
      label: "Copy SHA",
      onClick: () => {
        navigator.clipboard.writeText(mod.sha);
        notification.success("SHA copied to clipboard");
      },
    },
    {
      key: "copy-name",
      label: "Copy Name",
      onClick: () => {
        navigator.clipboard.writeText(mod.name);
        notification.success("Name copied to clipboard");
      },
    },
    { type: "divider" as const },

    // File viewing operations
    {
      key: "view-original",
      label: "View Original File",
      icon: <FileZipOutlined />,
      disabled: !checkedPaths?.originalPath,
      onClick: async () => {
        if (checkedPaths?.originalPath) {
          try {
            await fileDialogService.openFileInExplorer(
              checkedPaths.originalPath,
            );
            notification.success("Opened original file location");
          } catch (error) {
            notification.error("Failed to open original file");
          }
        }
      },
    },
    {
      key: "view-work",
      label: "Open Work Folder",
      icon: <FolderOpenOutlined />,
      disabled: !checkedPaths?.workPath,
      onClick: async () => {
        if (checkedPaths?.workPath) {
          try {
            await fileDialogService.openDirectory(checkedPaths.workPath);
            notification.success("Opened work folder");
          } catch (error) {
            notification.error("Failed to open work folder");
          }
        }
      },
    },
    {
      key: "view-preview",
      label: "Open Preview Folder",
      icon: <FolderOpenOutlined />,
      disabled: !checkedPaths?.thumbnailPath,
      onClick: async () => {
        if (checkedPaths?.thumbnailPath) {
          try {
            await fileDialogService.openDirectory(checkedPaths.thumbnailPath);
            notification.success("Opened preview folder");
          } catch (error) {
            notification.error("Failed to open preview folder");
          }
        }
      },
    },
    { type: "divider" as const },

    // Delete
    {
      key: "delete",
      label: "Delete Mod",
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
      notification.success(`Unloading object: ${mod.category}`);
    }
  };

  return (
    <div style={{ height: "100%", overflow: "auto" }}>
      {loading ? (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            height: "100%",
            padding: "20px",
          }}
        >
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
                style={{
                  padding: "12px 16px",
                  cursor: "pointer",
                  background: isSelected
                    ? "var(--color-primary-bg)"
                    : undefined,
                  borderLeft: isSelected
                    ? "3px solid var(--color-primary)"
                    : "3px solid transparent",
                  borderBottom: "1px solid var(--color-border-secondary)",
                  transition: "all 0.2s",
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "flex-start",
                }}
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
                <div
                  style={{
                    flex: 1,
                    minWidth: 0,
                    display: "flex",
                    flexDirection: "column",
                    justifyContent: "center",
                  }}
                >
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "8px",
                      marginBottom: "8px",
                    }}
                  >
                    <span style={{ fontWeight: 500 }}>{mod.name}</span>
                    {mod.isLoaded && (
                      <Tag
                        color="success"
                        style={{
                          margin: 0,
                          fontSize: "11px",
                          padding: "0 6px",
                          lineHeight: "18px",
                          borderRadius: "4px",
                          userSelect: "none",
                        }}
                      >
                        LOADED
                      </Tag>
                    )}
                  </div>
                  <Space size={[8, 4]} wrap style={{ userSelect: "none" }}>
                    {mod.grading && <GradingTag grading={mod.grading} />}
                    {mod.author && mod.author.trim() !== "" && (
                      <Tag color="blue" style={{ margin: 0 }}>
                        {mod.author}
                      </Tag>
                    )}
                    {mod.category && mod.category.trim() !== "" && (
                      <Tag color="geekblue" style={{ margin: 0 }}>
                        {mod.category}
                      </Tag>
                    )}
                    {mod.tags &&
                      mod.tags.slice(0, 3).map((tag) => (
                        <Tag key={tag} style={{ margin: 0 }}>
                          {tag}
                        </Tag>
                      ))}
                    {mod.tags && mod.tags.length > 3 && (
                      <Tag style={{ margin: 0 }}>
                        +{mod.tags.length - 3} more
                      </Tag>
                    )}
                  </Space>
                </div>
                {
                  <Space
                    size="small"
                    style={{ display: "flex", alignItems: "center" }}
                  >
                    <Button
                      type="text"
                      size="middle"
                      icon={
                        mod.isLoaded ? (
                          <PauseCircleOutlined style={{ fontSize: "18px" }} />
                        ) : (
                          <PlayCircleOutlined style={{ fontSize: "18px" }} />
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
                      title={mod.isLoaded ? "Unload mod" : "Load mod"}
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                      }}
                    />
                    <Button
                      type="text"
                      size="middle"
                      icon={<EditOutlined style={{ fontSize: "18px" }} />}
                      onClick={(e) => {
                        e.stopPropagation();
                        onEdit?.(mod);
                      }}
                      title="Edit mod"
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                      }}
                    />
                  </Space>
                }
              </div>
            );
          })}
        </>
      )}

      {/* Infinite scroll trigger */}
      {displayCount < mods.length && (
        <div
          ref={observerTarget}
          style={{
            height: "20px",
            margin: "10px 0",
            textAlign: "center",
            color: "var(--color-text-secondary)",
          }}
        >
          Loading more...
        </div>
      )}

      {/* Show total count */}
      {displayCount >= mods.length && mods.length > 50 && (
        <div
          style={{
            padding: "10px",
            textAlign: "center",
            color: "var(--color-text-secondary)",
            fontSize: "12px",
          }}
        >
          Showing all {mods.length} mods
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
