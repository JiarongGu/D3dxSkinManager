import { notification } from '../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Form, Select, Alert, Spin, Row, Col, Modal } from 'antd';
import {
  FolderOpenOutlined,
  RocketOutlined,
  PlayCircleOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { CompactButton, CompactCard, CompactSpace, CompactDivider } from '../../../shared/components/compact';
import { fileDialogService } from '../../../shared/services/systemService';
import { getActiveProfileConfig, updateActiveProfileConfigField } from '../../profiles/services/profileConfigService';
import { launchService, D3DMigotoVersion } from '../services/launchService';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useTranslation } from 'react-i18next';
import './D3DMigotoTab.css';
import logger from '../../../shared/utils/logger';

const { Option } = Select;

export const D3DMigotoTab: React.FC = () => {
  const { t } = useTranslation();
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
        logger.info('[D3DMigotoTab] No profile selected, skipping config load');
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        logger.info(`[D3DMigotoTab] Loading config for profile: ${profileState.selectedProfile.name}`);
        const config = await getActiveProfileConfig(profileState.selectedProfile.id);
        if (config) {
          form.setFieldsValue({
            migotoVersion: config.migotoVersion,
          });
        }
      } catch (error: unknown) {
        // Don't show error if it's just because no profile is selected
        const errorMessage = error instanceof Error ? error.message : '';
        if (!errorMessage.includes('Profile ID is required')) {
          notification.error(t('launch.d3dmigoto.loadConfigFailed'));
          logger.error('Failed to load profile config:', error);
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
      notification.error(t('launch.notifications.noProfileSelected'));
      return;
    }

    try {
      await updateActiveProfileConfigField(profileState.selectedProfile.id, 'migotoVersion', value);
      notification.success(t('launch.d3dmigoto.versionChangedTo', { version: value }));
    } catch (error) {
      notification.error(t('launch.d3dmigoto.updateVersionFailed'));
    }
  };

  const handleLaunch3DMigoto = () => {
    // TODO: Implement 3DMigoto launch
    notification.info(t('launch.d3dmigoto.launching'));
  };

  const handleOpenWorkDirectory = async () => {
    try {
      // TODO: Get work directory from current profile
      const workDir = 'C:\\Games\\YourGame'; // Placeholder
      await fileDialogService.openDirectory(workDir);
      notification.success(t('launch.d3dmigoto.openedWorkDir'));
    } catch (error) {
      notification.error(t('launch.d3dmigoto.openWorkDirFailed'));
    }
  };

  /**
   * Load 3DMigoto versions
   */
  const handleLoad3DMigotoVersions = async () => {
    if (!profileState.selectedProfile) {
      notification.error(t('launch.notifications.noProfileSelected'));
      return;
    }

    const profileId = profileState.selectedProfile.id;

    try {
      setD3dLoading(true);
      const versions = await launchService.getAvailableVersions(profileId);
      setD3dVersions(versions);

      if (versions.length === 0) {
        notification.info(t('launch.d3dmigoto.noVersionsFound'));
      } else {
        notification.success(t('launch.d3dmigoto.foundVersions', { count: versions.length }));
      }
    } catch (error) {
      notification.error(t('launch.d3dmigoto.loadVersionsFailed'));
      logger.error('Failed to load 3DMigoto versions:', error);
    } finally {
      setD3dLoading(false);
    }
  };

  /**
   * Deploy a 3DMigoto version
   */
  const handleDeploy3DMigoto = async (version: D3DMigotoVersion) => {
    Modal.confirm({
      title: t('launch.d3dmigoto.deployTitle', { version: version.name }),
      content: t('launch.d3dmigoto.deployContent', {
        version: version.name,
        size: version.sizeFormatted
      }),
      okText: t('launch.d3dmigoto.deploy'),
      okType: 'primary',
      cancelText: t('common.cancel'),
      onOk: async () => {
        if (!profileState.selectedProfile) {
          notification.error(t('launch.notifications.noProfileSelected'));
          return;
        }
        try {
          setD3dLoading(true);
          const result = await launchService.deployVersion(profileState.selectedProfile.id, version.name);

          if (result.success) {
            notification.success(result.message || t('launch.d3dmigoto.deploySuccess'));
            await handleLoad3DMigotoVersions(); // Refresh list
          } else {
            notification.error(result.error || t('launch.d3dmigoto.deployFailed'));
          }
        } catch (error) {
          notification.error(t('launch.d3dmigoto.deployVersionFailed'));
          logger.error('Failed to deploy 3DMigoto version:', error);
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
      notification.error(t('launch.notifications.noProfileSelected'));
      return;
    }

    try {
      const result = await launchService.launch3DMigoto(profileState.selectedProfile.id);
      if (result) {
        notification.success(t('launch.d3dmigoto.launchSuccess'));
      } else {
        notification.error(t('launch.d3dmigoto.launchFailedCheck'));
      }
    } catch (error) {
      notification.error(t('launch.d3dmigoto.launchFailed'));
      console.error(error);
    }
  };

  if (loading) {
    return (
      <div className="d3dmigoto-tab-loading">
        <Spin size="large" description={t('launch.d3dmigoto.loadingConfig')} />
      </div>
    );
  }

  return (
    <div className="d3dmigoto-tab-container">
      <Form
          form={form}
          layout="vertical"
        >
          {/* 3DMigoto Configuration */}
          <CompactCard
            title={<><RocketOutlined /> {t('launch.d3dmigoto.configuration')}</>}
            className="d3dmigoto-tab-card"
          >
            <Form.Item
              label={t('launch.d3dmigoto.versionLabel')}
              name="migotoVersion"
              tooltip={t('launch.d3dmigoto.versionTooltip')}
            >
              <Select onChange={handleMigotoVersionChange}>
                <Option value="3dmigoto">{t('launch.d3dmigoto.versionLatest')}</Option>
                <Option value="3dmigoto-dev">{t('launch.d3dmigoto.versionDev')}</Option>
                <Option value="custom">{t('launch.d3dmigoto.versionCustom')}</Option>
              </Select>
            </Form.Item>

            <CompactDivider />

            <CompactSpace size="middle" wrap>
              <CompactButton
                type="primary"
                size="large"
                icon={<PlayCircleOutlined />}
                onClick={handleLaunch3DMigotoLoader}
              >
                {t('launch.d3dmigoto.launchLoader')}
              </CompactButton>
              <CompactButton
                size="large"
                icon={<PlayCircleOutlined />}
                onClick={handleLaunch3DMigoto}
              >
                {t('launch.d3dmigoto.oneKeyLaunch')}
              </CompactButton>
              <CompactButton
                size="large"
                icon={<FolderOpenOutlined />}
                onClick={handleOpenWorkDirectory}
              >
                {t('launch.d3dmigoto.openWorkDir')}
              </CompactButton>
            </CompactSpace>
          </CompactCard>

          {/* 3DMigoto Version Management */}
          <CompactCard
            title={<><FolderOpenOutlined /> {t('launch.d3dmigoto.versionManagement')}</>}
            className="d3dmigoto-tab-card"
            extra={
              <CompactSpace>
                <CompactButton
                  icon={<ReloadOutlined />}
                  onClick={handleLoad3DMigotoVersions}
                  loading={d3dLoading}
                >
                  {t('common.refresh')}
                </CompactButton>
              </CompactSpace>
            }
          >
            <CompactSpace orientation="vertical" className="d3dmigoto-tab-version-list" size="middle">
              {d3dVersions.length === 0 && (
                <Alert
                  title={t('launch.d3dmigoto.versionManagement')}
                  description={t('launch.d3dmigoto.versionManagementDesc')}
                  type="info"
                  showIcon
                />
              )}

              {d3dVersions.map((version) => (
                <CompactCard key={version.name} size="small">
                  <Row gutter={16} align="middle">
                    <Col flex="auto">
                      <CompactSpace orientation="vertical" size="small">
                        <div>
                          <strong>{version.name}</strong>
                          {version.isDeployed && (
                            <span className="d3dmigoto-tab-version-deployed">
                              ● {t('launch.d3dmigoto.deployed')}
                            </span>
                          )}
                        </div>
                        <div className="d3dmigoto-tab-version-size">
                          {t('launch.d3dmigoto.size')}: {version.sizeFormatted}
                        </div>
                      </CompactSpace>
                    </Col>
                    <Col>
                      <CompactButton
                        type="primary"
                        size="small"
                        onClick={() => handleDeploy3DMigoto(version)}
                        loading={d3dLoading}
                        disabled={version.isDeployed}
                      >
                        {version.isDeployed ? t('launch.d3dmigoto.deployed') : t('launch.d3dmigoto.deploy')}
                      </CompactButton>
                    </Col>
                  </Row>
                </CompactCard>
              ))}
            </CompactSpace>
          </CompactCard>
        </Form>
    </div>
  );
};
