import { notification } from '../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Card, Table, Tag, Switch, Space,  Button, Modal, Descriptions } from 'antd';
import {
  ApiOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  InfoCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons';

interface PluginInfo {
  name: string;
  version: string;
  description: string;
  author: string;
  type: 'MessageHandler' | 'Service' | 'Both';
  enabled: boolean;
  status: 'Active' | 'Inactive' | 'Error';
}

export const PluginsView: React.FC = () => {
  const [plugins, setPlugins] = useState<PluginInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedPlugin, setSelectedPlugin] = useState<PluginInfo | null>(null);
  const [showDetails, setShowDetails] = useState(false);

  useEffect(() => {
    loadPlugins();
  }, []);

  const loadPlugins = async () => {
    setLoading(true);
    try {
      // TODO: Load plugin list from backend
      // For now, populate with the 26 converted plugins
      const mockPlugins: PluginInfo[] = [
        // Fully implemented plugins
        { name: 'HighlightLoadingMod', version: '1.0.0', description: 'Highlights currently loading mods in the UI', author: 'System', type: 'MessageHandler', enabled: true, status: 'Active' },
        { name: 'LogToFile', version: '1.0.0', description: 'Logs system events and mod operations to file', author: 'System', type: 'Service', enabled: true, status: 'Active' },
        { name: 'Example', version: '1.0.0', description: 'Example plugin demonstrating message handling capabilities', author: 'System', type: 'MessageHandler', enabled: true, status: 'Active' },
        { name: 'ScreenCapture', version: '1.0.0', description: 'Captures screenshots of game or application window', author: 'System', type: 'Service', enabled: true, status: 'Active' },

        // Functional stub plugins
        { name: 'AutoUpdate', version: '1.0.0', description: 'Automatically updates mods and application', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'BackupManager', version: '1.0.0', description: 'Manages mod backups and restore points', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'ConflictDetector', version: '1.0.0', description: 'Detects conflicts between loaded mods', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'DeleteIndexNoFile', version: '1.0.0', description: 'Removes database entries for missing mod files', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'DeleteModCache', version: '1.0.0', description: 'Deletes cached mod data and temporary files', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'DropfilesMultiple', version: '1.0.0', description: 'Handles drag-and-drop of multiple mod files', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'HandleUserEnv', version: '1.0.0', description: 'Manages user environment variables for mods', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'KeybindManager', version: '1.0.0', description: 'Manages custom keyboard shortcuts', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'LoadOrderSync', version: '1.0.0', description: 'Synchronizes mod load order across installations', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'Modify3dmKey', version: '1.0.0', description: 'Modifies 3DMigoto hotkey configurations', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'ModifyListOrder', version: '1.0.0', description: 'Allows reordering of mod display and load order', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'ModMetadataSync', version: '1.0.0', description: 'Synchronizes mod metadata with online databases', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'ModProfileManager', version: '1.0.0', description: 'Manages different mod profiles and presets', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'PreviewClearup', version: '1.0.0', description: 'Cleans up old preview images and cached previews', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'QuickLaunch', version: '1.0.0', description: 'Quick launch shortcuts for game and tools', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'SearchFilter', version: '1.0.0', description: 'Advanced search and filter capabilities for mods', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'SkinValidator', version: '1.0.0', description: 'Validates mod integrity and compatibility', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'TempCacheCleanup', version: '1.0.0', description: 'Automatically cleans temporary cache files', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'ThemeManager', version: '1.0.0', description: 'Manages application themes and UI customization', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'UnloadWithDeleteCache', version: '1.0.0', description: 'Deletes mod cache when unloading mods', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'UpdateNotifier', version: '1.0.0', description: 'Notifies about available updates for mods', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
        { name: 'UsageStatistics', version: '1.0.0', description: 'Tracks and displays mod usage statistics', author: 'System', type: 'Service', enabled: false, status: 'Inactive' },
      ];
      setPlugins(mockPlugins);
    } catch (error) {
      notification.error('Failed to load plugins');
    } finally {
      setLoading(false);
    }
  };

  const handleTogglePlugin = (name: string, enabled: boolean) => {
    // TODO: Send enable/disable message to backend
    setPlugins(plugins.map(p =>
      p.name === name
        ? { ...p, enabled, status: enabled ? 'Active' : 'Inactive' }
        : p
    ));
    notification.success(`Plugin ${enabled ? 'enabled' : 'disabled'}: ${name}`);
  };

  const handleShowDetails = (plugin: PluginInfo) => {
    setSelectedPlugin(plugin);
    setShowDetails(true);
  };

  const columns = [
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: string) => {
        const color = status === 'Active' ? 'green' : status === 'Error' ? 'red' : 'default';
        const icon = status === 'Active' ? <CheckCircleOutlined /> : <CloseCircleOutlined />;
        return <Tag color={color} icon={icon}>{status}</Tag>;
      },
    },
    {
      title: 'Plugin Name',
      dataIndex: 'name',
      key: 'name',
      render: (name: string) => <strong>{name}</strong>,
    },
    {
      title: 'Description',
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
    },
    {
      title: 'Type',
      dataIndex: 'type',
      key: 'type',
      width: 150,
      render: (type: string) => {
        const color = type === 'MessageHandler' ? 'blue' : type === 'Service' ? 'green' : 'purple';
        return <Tag color={color}>{type}</Tag>;
      },
    },
    {
      title: 'Version',
      dataIndex: 'version',
      key: 'version',
      width: 100,
    },
    {
      title: 'Enabled',
      dataIndex: 'enabled',
      key: 'enabled',
      width: 100,
      render: (enabled: boolean, record: PluginInfo) => (
        <Switch
          checked={enabled}
          onChange={(checked) => handleTogglePlugin(record.name, checked)}
        />
      ),
    },
    {
      title: 'Action',
      key: 'action',
      width: 100,
      render: (_: any, record: PluginInfo) => (
        <Button
          type="link"
          icon={<InfoCircleOutlined />}
          onClick={() => handleShowDetails(record)}
        >
          Details
        </Button>
      ),
    },
  ];

  const activeCount = plugins.filter(p => p.enabled).length;
  const totalCount = plugins.length;

  return (
    <div style={{ height: '100%', overflow: 'auto', padding: '24px' }}>
      <div style={{ maxWidth: '1400px', margin: '0 auto' }}>
      <Card
        title={
          <Space>
            <ApiOutlined />
            <span>Plugins</span>
            <Tag color="blue">{activeCount} Active</Tag>
            <Tag>{totalCount} Total</Tag>
          </Space>
        }
        extra={
          <Button
            icon={<ReloadOutlined />}
            onClick={loadPlugins}
            loading={loading}
          >
            Refresh
          </Button>
        }
      >
        <Table
          columns={columns}
          dataSource={plugins}
          rowKey="name"
          loading={loading}
          size="middle"
          pagination={{
            pageSize: 15,
            showSizeChanger: true,
            showTotal: (total) => `Total ${total} plugins`,
          }}
        />
      </Card>

      {/* Plugin Details Modal */}
      <Modal
        title={
          <Space>
            <ApiOutlined />
            {selectedPlugin?.name}
          </Space>
        }
        open={showDetails}
        onCancel={() => setShowDetails(false)}
        footer={[
          <Button key="close" onClick={() => setShowDetails(false)}>
            Close
          </Button>,
        ]}
        width={700}
      >
        {selectedPlugin && (
          <Descriptions bordered column={1}>
            <Descriptions.Item label="Plugin Name">{selectedPlugin.name}</Descriptions.Item>
            <Descriptions.Item label="Version">{selectedPlugin.version}</Descriptions.Item>
            <Descriptions.Item label="Author">{selectedPlugin.author}</Descriptions.Item>
            <Descriptions.Item label="Type">
              <Tag color={selectedPlugin.type === 'MessageHandler' ? 'blue' : 'green'}>
                {selectedPlugin.type}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Status">
              <Tag color={selectedPlugin.status === 'Active' ? 'green' : 'default'}>
                {selectedPlugin.status}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Enabled">
              <Switch
                checked={selectedPlugin.enabled}
                onChange={(checked) => {
                  handleTogglePlugin(selectedPlugin.name, checked);
                  setSelectedPlugin({ ...selectedPlugin, enabled: checked });
                }}
              />
            </Descriptions.Item>
            <Descriptions.Item label="Description">
              {selectedPlugin.description}
            </Descriptions.Item>
          </Descriptions>
        )}
      </Modal>
      </div>
    </div>
  );
};
