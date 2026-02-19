import React from 'react';
import { Table, Dropdown, message } from 'antd';
import {
  EditOutlined,
  ExportOutlined,
  FolderOpenOutlined,
  FileImageOutlined,
  FolderOutlined,
  FileZipOutlined,
} from '@ant-design/icons';
import { ModInfo } from '../../../shared/types/mod.types';
import { createModTableColumns } from './ModTableColumns';
import { fileDialogService } from '../../../shared/services/fileDialogService';
import { modService } from '../services/modService';
import { useProfile } from '../../../shared/context/ProfileContext';

interface ModTableProps {
  mods: ModInfo[];
  loading: boolean;
  objects?: string[];
  authors?: string[];
  onLoad: (sha: string) => void;
  onUnload: (sha: string) => void;
  onDelete: (sha: string, name: string) => void;
  onEdit?: (mod: ModInfo) => void;
  onRowClick?: (mod: ModInfo) => void;
  selectedMod?: ModInfo | null;
}

export const ModTable: React.FC<ModTableProps> = ({
  mods,
  loading,
  objects = [],
  authors = [],
  onLoad,
  onUnload,
  onDelete,
  onEdit,
  onRowClick,
  selectedMod
}) => {
  const {state: profileState} = useProfile();
  const columns = createModTableColumns({
    objects,
    authors,
    onLoad,
    onUnload,
    onDelete
  });

  return (
    <Table
      columns={columns}
      dataSource={mods}
      rowKey="sha"
      loading={loading}
      pagination={{
        pageSize: 20,
        showSizeChanger: true,
        pageSizeOptions: ['10', '20', '50', '100'],
        showTotal: (total) => `Total ${total} mods`
      }}
      size="small"
      onRow={(record) => ({
        onClick: () => {
          // Special handling for unload option
          if (record.sha === '__UNLOAD__') {
            // Find the loaded mod for this object and unload it
            const loadedMod = mods.find(m => m.category === record.category && m.isLoaded);
            if (loadedMod) {
              onUnload(loadedMod.sha);
              message.success(`Unloading object: ${record.category}`);
            }
          } else {
            onRowClick?.(record);
          }
        },
        onDoubleClick: () => {
          // Skip double-click for unload option
          if (record.sha === '__UNLOAD__') {
            return;
          }
          // Double-click to load mod (if not already loaded)
          if (!record.isLoaded) {
            onLoad(record.sha);
            message.success(`Loading mod: ${record.name}`);
          }
        },
        style: {
          cursor: 'pointer',
          background: record.sha === '__UNLOAD__'
            ? '#fff7e6' // Light orange background for unload option
            : selectedMod?.sha === record.sha
            ? '#e6f7ff'
            : undefined,
          fontWeight: record.sha === '__UNLOAD__' ? 500 : undefined,
        },
      })}
      components={{
        body: {
          row: (props: any) => {
            const mod = props['data-row-key']
              ? mods.find(m => m.sha === props['data-row-key'])
              : null;

            if (!mod) return <tr {...props} />;

            // Build comprehensive context menu
            const contextMenuItems = [
              // Load/Unload
              !mod.isLoaded
                ? {
                    key: 'load',
                    label: 'Load Mod',
                    onClick: () => onLoad(mod.sha),
                  }
                : {
                    key: 'unload',
                    label: 'Unload Mod',
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

              // Add operations
              {
                key: 'add-folder',
                label: 'Add Folder as Mod',
                icon: <FolderOutlined />,
                onClick: () => {
                  // TODO: Open folder picker and add as mod
                  message.info('Add folder as mod');
                },
              },
              {
                key: 'add-archive',
                label: 'Add Archive as Mod',
                icon: <FileZipOutlined />,
                onClick: () => {
                  // TODO: Open file picker for archive and add as mod
                  message.info('Add archive as mod');
                },
              },
              { type: 'divider' as const },

              // Delete
              {
                key: 'delete',
                label: 'Delete Mod',
                danger: true,
                onClick: () => onDelete(mod.sha, mod.name),
              },
            ];

            return (
              <Dropdown
                menu={{ items: contextMenuItems }}
                trigger={['contextMenu']}
              >
                <tr {...props} />
              </Dropdown>
            );
          },
        },
      }}
    />
  );
};
