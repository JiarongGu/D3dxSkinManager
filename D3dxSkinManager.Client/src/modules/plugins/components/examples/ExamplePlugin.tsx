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
import { bridgeService } from '../../../../shared/services/bridgeService';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useTranslation } from 'react-i18next';
import './ExamplePlugin.css';

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
  const { t } = useTranslation();
  const [log, setLog] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const { state: profileState } = useProfile();

  const loadLog = async () => {
    setLoading(true);
    try {
      // Send custom message to backend plugin
      const response = await bridgeService.sendMessage<{ log: string }>({
        module: 'PLUGINS',
        type: 'GET_MOD_LOG',
        profileId: profileState.selectedProfile?.id
      });
      setLog(response.log || t('plugins.modLog.noLogs'));
    } catch (err: any) {
      notification.error(t('plugins.modLog.loadFailed', { error: err.message }));
      console.error('Error loading log:', err);
    } finally {
      setLoading(false);
    }
  };

  const clearLog = async () => {
    try {
      await bridgeService.sendMessage({
        module: 'PLUGINS',
        type: 'CLEAR_MOD_LOG',
        profileId: profileState.selectedProfile?.id
      });
      notification.success(t('plugins.modLog.logCleared'));
      setLog('');
    } catch (err: any) {
      notification.error(t('plugins.modLog.clearFailed', { error: err.message }));
    }
  };

  useEffect(() => {
    loadLog();
  }, []);

  return (
    <div className="mod-log-viewer-container">
      <Card>
        <Title level={3}>
          <FileTextOutlined /> {t('plugins.modLog.title')}
        </Title>
        <Paragraph>
          {t('plugins.modLog.description')}
        </Paragraph>

        <Space className="mod-log-viewer-actions">
          <Button type="primary" onClick={loadLog} loading={loading}>
            {t('plugins.modLog.refreshLog')}
          </Button>
          <Button icon={<ClearOutlined />} onClick={clearLog}>
            {t('plugins.modLog.clearLog')}
          </Button>
        </Space>

        <Card className="mod-log-viewer-log-card">
          <pre className="mod-log-viewer-log-pre">
            {log || t('plugins.modLog.loading')}
          </pre>
        </Card>

        <Paragraph className="mod-log-viewer-footer">
          <Text type="secondary">
            {t('plugins.modLog.pluginInfo')}
          </Text>
        </Paragraph>
      </Card>
    </div>
  );
};
