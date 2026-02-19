import React, { useState, useEffect } from 'react';
import { Form, Select, Button, Card, Space, message, Divider, Alert, Spin, Row, Col, Modal } from 'antd';
import {
  FolderOpenOutlined,
  RocketOutlined,
  PlayCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { fileDialogService } from '../../../shared/services/fileDialogService';
import { getActiveProfileConfig, updateActiveProfileConfigField } from '../../profiles/services/profileConfigService';
import { launchService, D3DMigotoVersion } from '../services/launchService';
import { useProfile } from '../../../shared/context/ProfileContext';

const { Option } = Select;

export const D3DMigotoTab: React.FC = () => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(true);
  const [d3dVersions, setD3dVersions] = useState<D3DMigotoVersion[]>([]);
  const [d3dLoading, setD3dLoading] = useState(false);
  const { state: profileState } = useProfile();

  // Load configuration when profile changes
  useEffect(() => {
    const loadConfig = async () => {
      // Only load if we have a selected profile
      if (!profileState.selectedProfile) {
        console.log('[D3DMigotoTab] No profile selected, skipping config load');
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        console.log('[D3DMigotoTab] Loading config for profile:', profileState.selectedProfile.name);
        const config = await getActiveProfileConfig(profileState.selectedProfile.id);
        if (config) {
          form.setFieldsValue({
            migotoVersion: config.migotoVersion,
          });
        }
      } catch (error: any) {
        // Don't show error if it's just because no profile is selected
        if (!error?.message?.includes('Profile ID is required')) {
          message.error('Failed to load profile configuration');
          console.error('Failed to load profile config:', error);
        }
      } finally {
        setLoading(false);
      }
    };

    loadConfig();
    if (profileState.selectedProfile) {
      handleLoad3DMigotoVersions();
    }
  }, [form, profileState.selectedProfile?.id]); // React to profile ID changes

  const handleMigotoVersionChange = async (value: string) => {
    if (!profileState.selectedProfile) {
      message.error('No profile selected');
      return;
    }

    try {
      await updateActiveProfileConfigField(profileState.selectedProfile.id, 'migotoVersion', value);
      message.success(`3DMigoto version changed to: ${value}`);
    } catch (error) {
      message.error('Failed to update 3DMigoto version');
    }
  };

  const handleLaunch3DMigoto = () => {
    // TODO: Implement 3DMigoto launch
    message.info('Launching 3DMigoto...');
  };

  const handleOpenWorkDirectory = async () => {
    try {
      // TODO: Get work directory from current profile
      const workDir = 'C:\\Games\\YourGame'; // Placeholder
      await fileDialogService.openDirectory(workDir);
      message.success('Opened work directory');
    } catch (error) {
      message.error('Failed to open work directory');
    }
  };

  /**
   * Load 3DMigoto versions
   */
  const handleLoad3DMigotoVersions = async () => {
    if (!profileState.selectedProfile) {
      message.error('No profile selected');
      return;
    }

    const profileId = profileState.selectedProfile.id;

    try {
      setD3dLoading(true);
      const versions = await launchService.getAvailableVersions(profileId);
      setD3dVersions(versions);

      if (versions.length === 0) {
        message.info('No 3DMigoto versions found. Place version archives in the 3dmigoto directory.');
      } else {
        message.success(`Found ${versions.length} 3DMigoto version(s)`);
      }
    } catch (error) {
      message.error('Failed to load 3DMigoto versions');
      console.error(error);
    } finally {
      setD3dLoading(false);
    }
  };

  /**
   * Deploy a 3DMigoto version
   */
  const handleDeploy3DMigoto = async (version: D3DMigotoVersion) => {
    Modal.confirm({
      title: `Deploy 3DMigoto ${version.name}?`,
      content: `This will extract ${version.name} (${version.sizeFormatted}) to your work directory. Existing 3DMigoto files will be replaced, but .ini configuration files will be preserved.`,
      okText: 'Deploy',
      okType: 'primary',
      cancelText: 'Cancel',
      onOk: async () => {       
        if (!profileState.selectedProfile) {
          message.error('No profile selected');
          return;
        }
        try {
          setD3dLoading(true);
          const result = await launchService.deployVersion(profileState.selectedProfile.id, version.name);

          if (result.success) {
            message.success(result.message || '3DMigoto deployed successfully');
            await handleLoad3DMigotoVersions(); // Refresh list
          } else {
            message.error(result.error || 'Deployment failed');
          }
        } catch (error) {
          message.error('Failed to deploy 3DMigoto version');
          console.error(error);
        } finally {
          setD3dLoading(false);
        }
      }
    });
  };

  /**
   * Launch 3DMigoto
   */
  const handleLaunch3DMigotoLoader = async () => {
    if (!profileState.selectedProfile) {
      message.error('No profile selected');
      return;
    }

    try {
      const result = await launchService.launch3DMigoto(profileState.selectedProfile.id);
      if (result) {
        message.success('3DMigoto launched successfully');
      } else {
        message.error('Failed to launch 3DMigoto. Check that the work directory is configured and contains a loader executable.');
      }
    } catch (error) {
      message.error('Failed to launch 3DMigoto');
      console.error(error);
    }
  };

  if (loading) {
    return (
      <div style={{ height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <Spin size="large" description="Loading profile configuration..." />
      </div>
    );
  }

  return (
    <div style={{ paddingTop: '16px' }}>
      <Form
          form={form}
          layout="vertical"
        >
          {/* 3DMigoto Configuration */}
          <Card
            title={<><RocketOutlined /> 3DMigoto Configuration</>}
            style={{ marginBottom: '24px' }}
          >
            <Form.Item
              label="3DMigoto Version"
              name="migotoVersion"
              tooltip="Select the 3DMigoto version to use for this profile"
            >
              <Select onChange={handleMigotoVersionChange}>
                <Option value="3dmigoto">3DMigoto (Latest)</Option>
                <Option value="3dmigoto-dev">3DMigoto Dev Build</Option>
                <Option value="custom">Custom Build</Option>
              </Select>
            </Form.Item>

            <Divider />

            <Space size="middle" wrap>
              <Button
                type="primary"
                size="large"
                icon={<PlayCircleOutlined />}
                onClick={handleLaunch3DMigotoLoader}
              >
                Launch 3DMigoto Loader
              </Button>
              <Button
                size="large"
                icon={<PlayCircleOutlined />}
                onClick={handleLaunch3DMigoto}
              >
                One-Key Launch
              </Button>
              <Button
                size="large"
                icon={<FolderOpenOutlined />}
                onClick={handleOpenWorkDirectory}
              >
                Open Work Directory
              </Button>
            </Space>
          </Card>

          {/* 3DMigoto Version Management */}
          <Card
            title={<><FolderOpenOutlined /> 3DMigoto Version Management</>}
            style={{ marginBottom: '24px' }}
            extra={
              <Space>
                <Button
                  icon={<ReloadOutlined />}
                  onClick={handleLoad3DMigotoVersions}
                  loading={d3dLoading}
                >
                  Refresh
                </Button>
              </Space>
            }
          >
            <Space orientation="vertical" style={{ width: '100%' }} size="middle">
              {d3dVersions.length === 0 && (
                <Alert
                  title="3DMigoto Version Management"
                  description="Place 3DMigoto version archives (.zip, .7z) in the '3dmigoto' directory, then click Refresh to see them here. Deploy a version to extract it to your work directory."
                  type="info"
                  showIcon
                />
              )}

              {d3dVersions.map((version) => (
                <Card key={version.name} size="small">
                  <Row gutter={16} align="middle">
                    <Col flex="auto">
                      <Space orientation="vertical" size="small">
                        <div>
                          <strong>{version.name}</strong>
                          {version.isDeployed && (
                            <span style={{ marginLeft: '8px', color: '#52c41a' }}>
                              ● Deployed
                            </span>
                          )}
                        </div>
                        <div style={{ fontSize: '12px', color: '#8c8c8c' }}>
                          Size: {version.sizeFormatted}
                        </div>
                      </Space>
                    </Col>
                    <Col>
                      <Button
                        type="primary"
                        size="small"
                        onClick={() => handleDeploy3DMigoto(version)}
                        loading={d3dLoading}
                        disabled={version.isDeployed}
                      >
                        {version.isDeployed ? 'Deployed' : 'Deploy'}
                      </Button>
                    </Col>
                  </Row>
                </Card>
              ))}
            </Space>
          </Card>
        </Form>
    </div>
  );
};
