/**
 * Example frontend plugin
 *
 * Demonstrates:
 * - Custom tab with UI
 * - Event handling
 * - Backend communication via custom message types
 */

import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Card, Button, Typography, Space } from 'antd';
import { FileTextOutlined, ClearOutlined } from '@ant-design/icons';
import type { UIPlugin, PluginContext, PluginEventArgs } from '../PluginTypes';
import { photinoService } from '../../../../shared/services/photinoService';
import { useProfile } from '../../../../shared/context/ProfileContext';

const { Title, Paragraph, Text } = Typography;

export class ModLogViewerPlugin implements UIPlugin {
  id = 'com.d3dxskinmanager.modlogviewer';
  name = 'Mod Log Viewer';
  version = '1.0.0';
  description = 'View and manage mod operation logs from the ModLogger backend plugin';
  author = 'D3dxSkinManager Team';
  tabLabel = 'Mod Logs';
  tabIcon = 'FileTextOutlined';

  private context?: PluginContext;

  async initialize(context: PluginContext): Promise<void> {
    this.context = context;

    // Register event handlers
    context.registerEventHandler('MOD_LOADED' as any, this.onModLoaded);
    context.registerEventHandler('MOD_UNLOADED' as any, this.onModUnloaded);

    console.log(`[${this.name}] Initialized`);
  }

  async cleanup(): Promise<void> {
    console.log(`[${this.name}] Cleaned up`);
  }

  private onModLoaded = (args: PluginEventArgs) => {
    notification.success(`Mod loaded event received`);
  };

  private onModUnloaded = (args: PluginEventArgs) => {
    notification.info(`Mod unloaded event received`);
  };

  renderTab = () => {
    return <ModLogViewerTab />;
  };
}

/**
 * Tab component for viewing mod logs
 */
const ModLogViewerTab: React.FC = () => {
  const [log, setLog] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const { state: profileState } = useProfile();

  const loadLog = async () => {
    setLoading(true);
    try {
      // Send custom message to backend plugin
      const response = await photinoService.sendMessage<{ log: string }>({
        module: 'PLUGINS',
        type: 'GET_MOD_LOG',
        profileId: profileState.selectedProfile?.id
      });
      setLog(response.log || 'No logs available');
    } catch (err: any) {
      notification.error(`Failed to load log: ${err.message}`);
      console.error('Error loading log:', err);
    } finally {
      setLoading(false);
    }
  };

  const clearLog = async () => {
    try {
      await photinoService.sendMessage({
        module: 'PLUGINS',
        type: 'CLEAR_MOD_LOG',
        profileId: profileState.selectedProfile?.id
      });
      notification.success('Log cleared');
      setLog('');
    } catch (err: any) {
      notification.error(`Failed to clear log: ${err.message}`);
    }
  };

  useEffect(() => {
    loadLog();
  }, []);

  return (
    <div style={{ padding: '24px' }}>
      <Card>
        <Title level={3}>
          <FileTextOutlined /> Mod Operation Logs
        </Title>
        <Paragraph>
          This plugin displays logs from the ModLogger backend plugin. It demonstrates
          how frontend and backend plugins can work together.
        </Paragraph>

        <Space style={{ marginBottom: '16px' }}>
          <Button type="primary" onClick={loadLog} loading={loading}>
            Refresh Log
          </Button>
          <Button icon={<ClearOutlined />} onClick={clearLog}>
            Clear Log
          </Button>
        </Space>

        <Card
          style={{
            backgroundColor: '#1e1e1e',
            color: '#d4d4d4',
            maxHeight: '500px',
            overflow: 'auto'
          }}
        >
          <pre style={{ margin: 0, fontFamily: 'monospace', fontSize: '12px' }}>
            {log || 'Loading...'}
          </pre>
        </Card>

        <Paragraph style={{ marginTop: '16px', color: '#888' }}>
          <Text type="secondary">
            Backend plugin: ModLogger | Frontend plugin: ModLogViewer
          </Text>
        </Paragraph>
      </Card>
    </div>
  );
};
