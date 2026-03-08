import { notification } from '../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Card, Tag, Space, Button, Modal, Descriptions } from 'antd';
import {
  ApiOutlined,
  CheckCircleOutlined,
  InfoCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import './PluginsView.css';
import { bridgeService } from '../../../shared/services/bridgeService';
import { useProfile } from '../../../shared/context/ProfileContext';
import { DataTable, ColumnsType } from '../../../shared/components/common';

interface PluginInfo {
  id: string;
  name: string;
  version: string;
  description: string;
  author: string;
  isEnabled: boolean;
  capabilities: string[];
}

export const PluginsView: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [plugins, setPlugins] = useState<PluginInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedPlugin, setSelectedPlugin] = useState<PluginInfo>();
  const [showDetails, setShowDetails] = useState(false);

  useEffect(() => {
    if (selectedProfileId) {
      loadPlugins();
    }
  }, [selectedProfileId]);

  const loadPlugins = async () => {
    if (!selectedProfileId) {
      notification.error(t('plugins.noProfileSelected'));
      return;
    }

    setLoading(true);
    try {
      const response = await bridgeService.sendMessage<PluginInfo[]>({
        module: 'PLUGIN',
        type: 'GET_ALL',
        profileId: selectedProfileId
      });
      setPlugins(response);
    } catch (error: unknown) {
      notification.error(t('plugins.loadFailed'));
          } finally {
      setLoading(false);
    }
  };

  const handleShowDetails = (plugin: PluginInfo) => {
    setSelectedPlugin(plugin);
    setShowDetails(true);
  };

  const columns: ColumnsType<PluginInfo> = [
    {
      title: t("common.status"),
      dataIndex: 'isEnabled',
      key: 'status',
      width: 100,
      render: (isEnabled: boolean) => (
        <Tag icon={<CheckCircleOutlined />} color="success">
          {t('plugins.status.active')}
        </Tag>
      ),
    },
    {
      title: t('plugins.table.name'),
      dataIndex: 'name',
      key: 'name',
      render: (name: string, record: PluginInfo) => (
        <Space>
          <ApiOutlined />
          <span>{name}</span>
        </Space>
      ),
    },
    {
      title: t("common.version"),
      dataIndex: 'version',
      key: 'version',
      width: 100,
    },
    {
      title: t("common.capabilities"),
      dataIndex: 'capabilities',
      key: 'capabilities',
      render: (capabilities: string[]) => (
        <>
          {capabilities.length > 0 ? (
            capabilities.map((cap) => (
              <Tag key={cap} color="blue">
                {cap}
              </Tag>
            ))
          ) : (
            <Tag color="default">{t('plugins.capabilities.none')}</Tag>
          )}
        </>
      ),
    },
    {
      title: t("common.description"),
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
    },
    {
      title: t("common.actions"),
      key: 'actions',
      width: 150,
      render: (_: unknown, record: PluginInfo) => (
        <Space>
          <Button
            type="link"
            size="small"
            icon={<InfoCircleOutlined />}
            onClick={() => handleShowDetails(record)}
          >
            {t('plugins.actions.details')}
          </Button>
        </Space>
      ),
    },
  ];

  return (
    <div className="plugins-view-container">
      <Card
        title={
          <Space>
            <ApiOutlined />
            {t('plugins.title')}
          </Space>
        }
        extra={
          <Button
            type="primary"
            icon={<ReloadOutlined />}
            onClick={loadPlugins}
            loading={loading}
          >
            {t('plugins.actions.reload')}
          </Button>
        }
      >
        <DataTable
          columns={columns}
          dataSource={plugins}
          rowKey="id"
          loading={loading}
        />
      </Card>

      <Modal
        title={selectedPlugin?.name}
        open={showDetails}
        onCancel={() => setShowDetails(false)}
        footer={[
          <Button key="close" onClick={() => setShowDetails(false)}>
            {t('common.close')}
          </Button>,
        ]}
        width={600}
      >
        {selectedPlugin && (
          <Descriptions column={1} bordered>
            <Descriptions.Item label={t('plugins.details.id')}>
              {selectedPlugin.id}
            </Descriptions.Item>
            <Descriptions.Item label={t("common.version")}>
              {selectedPlugin.version}
            </Descriptions.Item>
            <Descriptions.Item label={t("common.author")}>
              {selectedPlugin.author}
            </Descriptions.Item>
            <Descriptions.Item label={t("common.description")}>
              {selectedPlugin.description}
            </Descriptions.Item>
            <Descriptions.Item label={t("common.capabilities")}>
              {selectedPlugin.capabilities.length > 0 ? (
                <Space>
                  {selectedPlugin.capabilities.map((cap) => (
                    <Tag key={cap} color="blue">
                      {cap}
                    </Tag>
                  ))}
                </Space>
              ) : (
                t('plugins.capabilities.none')
              )}
            </Descriptions.Item>
            <Descriptions.Item label={t("common.status")}>
              <Tag icon={<CheckCircleOutlined />} color="success">
                {t('plugins.status.active')}
              </Tag>
            </Descriptions.Item>
          </Descriptions>
        )}
      </Modal>
    </div>
  );
};
