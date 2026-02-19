import React, { useState, useRef, useCallback } from 'react';
import { List, Tag, Button, Dropdown, message, Space } from 'antd';
import {
  PlayCircleOutlined,
  PauseCircleOutlined,
  EditOutlined,
  DeleteOutlined,
  MoreOutlined,
  ExportOutlined,
  FolderOpenOutlined,
  FileImageOutlined,
  FolderOutlined,
  CheckCircleFilled,
} from '@ant-design/icons';
import { ModInfo } from '../../../shared/types/mod.types';
import { fileDialogService } from '../../../shared/services/fileDialogService';
import { modService } from '../services/modService';
import { GradingTag } from '../../../shared/components/common/GradingTag';
import { useProfile } from '../../../shared/context/ProfileContext';

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
  selectedMod
}) => {
  const [displayCount, setDisplayCount] = useState(50);
  const observerTarget = useRef<HTMLDivElement>(null);
  const {state: profileState} = useProfile();

  // Intersection observer for infinite scroll
  const handleObserver = useCallback((entries: IntersectionObserverEntry[]) => {
    const target = entries[0];
    if (target.isIntersecting && displayCount < mods.length) {
      setDisplayCount(prev => Math.min(prev + 50, mods.length));
    }
  }, [displayCount, mods.length]);

  React.useEffect(() => {
    const observer = new IntersectionObserver(handleObserver, {
      root: null,
      rootMargin: '100px',
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

  const getContextMenuItems = (mod: ModInfo) => [
    // Load/Unload
    !mod.isLoaded
      ? {
          key: 'load',
          label: 'Load Mod',
          icon: <PlayCircleOutlined />,
          onClick: () => onLoad(mod.sha),
        }
      : {
          key: 'unload',
          label: 'Unload Mod',
          icon: <PauseCircleOutlined />,
          onClick: () => onUnload(mod.sha),
        },
    { type: 'divider' as const },

    // Edit & Export
    {
      key: 'edit',
      label: 'Edit Mod Information',
      icon: <EditOutlined />,
      onClick: () => {
        if (onEdit) {
          onEdit(mod);
        } else {
          message.info(`Edit mod: ${mod.name}`);
        }
      },
    },
    {
      key: 'export',
      label: 'Export Mod File',
      icon: <ExportOutlined />,
      onClick: async () => {
        const result = await fileDialogService.saveFileDialog({
          title: 'Export Mod',
          defaultPath: `${mod.name}.zip`,
          filters: [
            { name: 'ZIP Archive', extensions: ['zip'] },
            { name: 'All Files', extensions: ['*'] }
          ]
        });

        if (result.success && result.filePath && profileState.selectedProfile) {
          try {
            await modService.exportMod(profileState.selectedProfile.id, mod.sha, result.filePath);
            message.success(`Exported mod: ${mod.name}`);
          } catch (error) {
            message.error('Failed to export mod');
          }
        } else if (result.error) {
          message.error(result.error);
        }
      },
    },
    { type: 'divider' as const },

    // Copy operations
    {
      key: 'copy-sha',
      label: 'Copy SHA',
      onClick: () => {
        navigator.clipboard.writeText(mod.sha);
        message.success('SHA copied to clipboard');
      },
    },
    {
      key: 'copy-name',
      label: 'Copy Name',
      onClick: () => {
        navigator.clipboard.writeText(mod.name);
        message.success('Name copied to clipboard');
      },
    },
    { type: 'divider' as const },

    // File viewing operations
    {
      key: 'view-original',
      label: 'View Original File',
      icon: <FolderOpenOutlined />,
      disabled: !mod.originalPath,
      onClick: async () => {
        if (mod.originalPath) {
          try {
            await fileDialogService.openFileInExplorer(mod.originalPath);
            message.success('Opened original file location');
          } catch (error) {
            message.error('Failed to open original file');
          }
        }
      },
    },
    {
      key: 'view-work',
      label: 'View Work Files',
      icon: <FolderOutlined />,
      disabled: !mod.workPath,
      onClick: async () => {
        if (mod.workPath) {
          try {
            await fileDialogService.openDirectory(mod.workPath);
            message.success('Opened work directory');
          } catch (error) {
            message.error('Failed to open work directory');
          }
        }
      },
    },
    {
      key: 'view-cache',
      label: 'View Cache Files',
      icon: <FolderOutlined />,
      disabled: !mod.cachePath,
      onClick: async () => {
        if (mod.cachePath) {
          try {
            await fileDialogService.openDirectory(mod.cachePath);
            message.success('Opened cache directory');
          } catch (error) {
            message.error('Failed to open cache directory');
          }
        }
      },
    },
    {
      key: 'view-preview',
      label: 'View Preview Image',
      icon: <FileImageOutlined />,
      onClick: async () => {
        if (mod.previewPath) {
          try {
            await fileDialogService.openFile(mod.previewPath);
            message.success('Opened preview image');
          } catch (error) {
            message.error('Failed to open preview image');
          }
        } else {
          message.warning('No preview image available for this mod');
        }
      },
    },
    { type: 'divider' as const },

    // Delete
    {
      key: 'delete',
      label: 'Delete Mod',
      icon: <DeleteOutlined />,
      danger: true,
      onClick: () => onDelete(mod.sha, mod.name),
    },
  ];

  const handleUnloadClick = (mod: ModInfo) => {
    // Find the loaded mod for this object and unload it
    const loadedMod = mods.find(m => m.category === mod.category && m.isLoaded);
    if (loadedMod) {
      onUnload(loadedMod.sha);
      message.success(`Unloading object: ${mod.category}`);
    }
  };

  return (
    <div style={{ height: '100%', overflow: 'auto' }}>
      <List
        loading={loading}
        dataSource={displayedMods}
        renderItem={(mod) => {
          const isUnloadOption = mod.sha === '__UNLOAD__';
          const isSelected = selectedMod?.sha === mod.sha;

          return (
            <List.Item
              key={mod.sha}
              style={{
                padding: '12px 16px',
                cursor: 'pointer',
                background: isUnloadOption
                  ? 'var(--color-warning-bg)'
                  : isSelected
                  ? 'var(--color-primary-bg)'
                  : undefined,
                borderLeft: isSelected ? '3px solid var(--color-primary)' : '3px solid transparent',
                transition: 'all 0.2s',
              }}
              onClick={() => {
                if (isUnloadOption) {
                  handleUnloadClick(mod);
                } else {
                  onRowClick?.(mod);
                }
              }}
              onDoubleClick={() => {
                if (!isUnloadOption && !mod.isLoaded) {
                  onLoad(mod.sha);
                  message.success(`Loading mod: ${mod.name}`);
                }
              }}
              actions={
                isUnloadOption
                  ? []
                  : [
                      <Button
                        key="load-unload"
                        type="text"
                        size="small"
                        icon={mod.isLoaded ? <PauseCircleOutlined /> : <PlayCircleOutlined />}
                        onClick={(e) => {
                          e.stopPropagation();
                          if (mod.isLoaded) {
                            onUnload(mod.sha);
                          } else {
                            onLoad(mod.sha);
                          }
                        }}
                      />,
                      <Button
                        key="edit"
                        type="text"
                        size="small"
                        icon={<EditOutlined />}
                        onClick={(e) => {
                          e.stopPropagation();
                          onEdit?.(mod);
                        }}
                      />,
                      <Dropdown
                        key="more"
                        menu={{ items: getContextMenuItems(mod) }}
                        trigger={['click']}
                      >
                        <Button
                          type="text"
                          size="small"
                          icon={<MoreOutlined />}
                          onClick={(e) => e.stopPropagation()}
                        />
                      </Dropdown>,
                    ]
              }
            >
              <List.Item.Meta
                title={
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    {mod.isLoaded && (
                      <CheckCircleFilled style={{ color: 'var(--color-success)', fontSize: '14px' }} />
                    )}
                    <span style={{ fontWeight: isUnloadOption ? 600 : 500 }}>
                      {mod.name}
                    </span>
                  </div>
                }
                description={
                  <Space size={[8, 4]} wrap>
                    {mod.grading && (
                      <GradingTag grading={mod.grading} />
                    )}
                    {mod.author && mod.author.trim() !== '' && (
                      <Tag color="blue" style={{ margin: 0 }}>
                        {mod.author}
                      </Tag>
                    )}
                    {mod.category && mod.category.trim() !== '' && (
                      <Tag color="geekblue" style={{ margin: 0 }}>
                        {mod.category}
                      </Tag>
                    )}
                    {mod.tags && mod.tags.slice(0, 3).map(tag => (
                      <Tag key={tag} style={{ margin: 0 }}>
                        {tag}
                      </Tag>
                    ))}
                    {mod.tags && mod.tags.length > 3 && (
                      <Tag style={{ margin: 0 }}>+{mod.tags.length - 3} more</Tag>
                    )}
                  </Space>
                }
              />
            </List.Item>
          );
        }}
      />

      {/* Infinite scroll trigger */}
      {displayCount < mods.length && (
        <div
          ref={observerTarget}
          style={{
            height: '20px',
            margin: '10px 0',
            textAlign: 'center',
            color: 'var(--color-text-secondary)',
          }}
        >
          Loading more...
        </div>
      )}

      {/* Show total count */}
      {displayCount >= mods.length && mods.length > 50 && (
        <div
          style={{
            padding: '10px',
            textAlign: 'center',
            color: 'var(--color-text-secondary)',
            fontSize: '12px',
          }}
        >
          Showing all {mods.length} mods
        </div>
      )}
    </div>
  );
};
