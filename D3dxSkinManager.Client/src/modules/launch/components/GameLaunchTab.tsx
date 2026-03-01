import { notification } from '../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Form, Input, Tooltip, Alert, Spin } from 'antd';
import { useTranslation } from 'react-i18next';
import {
  FolderOpenOutlined,
  PlayCircleOutlined,
  PlusOutlined,
  SettingOutlined,
} from '@ant-design/icons';
import { CompactButton, CompactCard, CompactSpace, CompactDivider } from '../../../shared/components/compact';
import { UnityArgsDialog } from '../../core/components/dialogs/UnityArgsDialog';
import { fileDialogService } from '../../../shared/services/systemService';
import { profileService } from '../../profile/services/profileService';
import { useProfile } from '../../../shared/context/ProfileContext';
import { PATH_PLACEHOLDERS, LAUNCH_ARG_EXAMPLES, MODULE_NAMES } from '../../../shared/constants/ui.constants';
import './GameLaunchTab.css';

export const GameLaunchTab: React.FC = () => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [unityArgsDialogVisible, setUnityArgsDialogVisible] = useState(false);
  const [loading, setLoading] = useState(true);
  const { state: profileState } = useProfile();

  // Load configuration when profile changes
  useEffect(() => {
    const loadConfig = async () => {
      // Only load if we have a selected profile
      if (!profileState.selectedProfile) {
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        const config = await profileService.getProfileConfig(profileState.selectedProfile.id);
        if (config) {
          form.setFieldsValue({
            gamePath: config.gamePath || '',
            launchArgs: config.gameLaunchArgs || '',
            customProgramPath: config.customProgramPath || '',
            customLaunchArgs: config.customProgramArgs || '',
          });
        }
      } catch (error: unknown) {
        // Don't show error if it's just because no profile is selected
        const errorMessage = error instanceof Error ? error.message : '';
        if (!errorMessage.includes('Profile ID is required')) {
          notification.error(t('launch.game.loadConfigFailed'));
          console.error('Failed to load profile config:', error);
        }
      } finally {
        setLoading(false);
      }
    };

    loadConfig();
  }, [form, profileState.selectedProfile?.id]); // React to profile ID changes

  const handleBrowseGamePath = async () => {
    const result = await fileDialogService.openFileDialog({
      title: t('launch.game.selectGameExe'),
      filters: [
        { name: t('launch.game.executableFiles'), extensions: ['exe'] },
        { name: t('launch.game.allFiles'), extensions: ['*'] }
      ]
    });

    if (result.success && result.filePath) {
      form.setFieldsValue({ gamePath: result.filePath });
      if (!profileState.selectedProfile) {
        notification.error(t('errors.noProfileSelected'));
        return;
      }
      try {
        await profileService.updateProfileConfig({
          profileId: profileState.selectedProfile.id,
          gamePath: result.filePath
        });
        notification.success(t('launch.game.gamePathUpdated'));
      } catch (error) {
        notification.error(t('launch.game.saveGamePathFailed'));
      }
    }
  };

  const handleBrowseCustomProgramPath = async () => {
    const result = await fileDialogService.openFileDialog({
      title: t('launch.game.selectCustomProgram'),
      filters: [
        { name: t('launch.game.executableFiles'), extensions: ['exe'] },
        { name: t('launch.game.allFiles'), extensions: ['*'] }
      ]
    });

    if (result.success && result.filePath) {
      form.setFieldsValue({ customProgramPath: result.filePath });
      if (!profileState.selectedProfile) {
        notification.error(t('errors.noProfileSelected'));
        return;
      }
      try {
        await profileService.updateProfileConfig({
          profileId: profileState.selectedProfile.id,
          customProgramPath: result.filePath
        });
        notification.success(t('launch.game.customPathUpdated'));
      } catch (error) {
        notification.error(t('launch.game.saveCustomPathFailed'));
      }
    }
  };

  const handleLaunchGame = () => {
    const gamePath = form.getFieldValue('gamePath');
    if (!gamePath) {
      notification.error(t('launch.game.setGamePathFirst'));
      return;
    }
    // TODO: Launch game with arguments
    notification.info(t('launch.game.launchingGame'));
  };

  const handleOpenGameDirectory = async () => {
    const gamePath = form.getFieldValue('gamePath');
    if (!gamePath) {
      notification.error(t('launch.game.setGamePathFirst'));
      return;
    }
    try {
      await fileDialogService.openFileInExplorer(gamePath);
      notification.success(t('launch.game.openedGameDir'));
    } catch (error) {
      notification.error(t('launch.game.openGameDirFailed'));
    }
  };

  const handleLaunchCustomProgram = () => {
    const programPath = form.getFieldValue('customProgramPath');
    if (!programPath) {
      notification.error(t('launch.game.setCustomPathFirst'));
      return;
    }
    // TODO: Launch custom program
    notification.info(t('launch.game.launchingCustom'));
  };

  const handleOpenCustomDirectory = async () => {
    const programPath = form.getFieldValue('customProgramPath');
    if (!programPath) {
      notification.error(t('launch.game.setCustomPathFirst'));
      return;
    }
    try {
      await fileDialogService.openFileInExplorer(programPath);
      notification.success(t('launch.game.openedCustomDir'));
    } catch (error) {
      notification.error(t('launch.game.openCustomDirFailed'));
    }
  };

  const handleOpenUnityArgsDialog = () => {
    setUnityArgsDialogVisible(true);
  };

  const handleSaveUnityArgs = async (args: string) => {
    form.setFieldsValue({ launchArgs: args });
    setUnityArgsDialogVisible(false);
    if (!profileState.selectedProfile) {
      notification.error(t('errors.noProfileSelected'));
      return;
    }
    try {
      await profileService.updateProfileConfig({
        profileId: profileState.selectedProfile.id,
        gameLaunchArgs: args
      });
      notification.success(t('launch.game.launchArgsUpdated'));
    } catch (error) {
      notification.error(t('launch.game.saveLaunchArgsFailed'));
    }
  };

  const handleLaunchArgsChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!profileState.selectedProfile) return;
    try {
      await profileService.updateProfileConfig({
        profileId: profileState.selectedProfile.id,
        gameLaunchArgs: e.target.value
      });
    } catch (error) {
      console.error('Failed to auto-save launch args:', error);
    }
  };

  const handleCustomLaunchArgsChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!profileState.selectedProfile) return;
    try {
      await profileService.updateProfileConfig({
        profileId: profileState.selectedProfile.id,
        customProgramArgs: e.target.value
      });
    } catch (error) {
      console.error('Failed to auto-save custom launch args:', error);
    }
  };

  if (loading) {
    return (
      <div className="game-launch-loading">
        <Spin size="large" description={t('launch.game.loadingConfig')} />
      </div>
    );
  }

  return (
    <div className="game-launch-tab">
      <Form
          form={form}
          layout="vertical"
        >
          {/* Game Configuration */}
          <CompactCard
            title={<><PlayCircleOutlined /> {t('launch.game.gameConfig')}</>}
            className="game-launch-card"
          >
            <Form.Item
              label={t('launch.game.gameExePath')}
              name="gamePath"
              tooltip={t('launch.game.gameExeTooltip')}
            >
              <Input.Group compact>
                <Input
                  className="game-launch-input-with-button"
                  placeholder={PATH_PLACEHOLDERS.GAME_EXE}
                  readOnly
                />
                <CompactButton
                  icon={<FolderOpenOutlined />}
                  onClick={handleBrowseGamePath}
                >
                  {t('common.browse')}
                </CompactButton>
              </Input.Group>
            </Form.Item>

            <Form.Item
              label={t('launch.game.launchArgs')}
              name="launchArgs"
              tooltip={t('launch.game.launchArgsTooltip')}
            >
              <Input.Group compact>
                <Input
                  className="game-launch-input-with-small-button"
                  placeholder={LAUNCH_ARG_EXAMPLES.COMMON_ARGS}
                  onChange={handleLaunchArgsChange}
                />
                <Tooltip title={t('launch.game.unityArgsHelper')}>
                  <CompactButton
                    icon={<PlusOutlined />}
                    onClick={handleOpenUnityArgsDialog}
                  >
                    {MODULE_NAMES.UNITY}
                  </CompactButton>
                </Tooltip>
              </Input.Group>
            </Form.Item>

            <CompactDivider />

            <CompactSpace size="middle" wrap>
              <CompactButton
                type="primary"
                size="large"
                icon={<PlayCircleOutlined />}
                onClick={handleLaunchGame}
              >
                {t('launch.game.launchGame')}
              </CompactButton>
              <CompactButton
                size="large"
                icon={<FolderOpenOutlined />}
                onClick={handleOpenGameDirectory}
              >
                {t('launch.game.openGameDir')}
              </CompactButton>
            </CompactSpace>
          </CompactCard>

          {/* Custom Launch Program */}
          <CompactCard
            title={<><SettingOutlined /> {t('launch.game.customProgram')}</>}
            className="game-launch-card"
          >
            <Form.Item
              label={t('launch.game.customProgramPath')}
              name="customProgramPath"
              tooltip={t('launch.game.customProgramTooltip')}
            >
              <Input.Group compact>
                <Input
                  className="game-launch-input-with-button"
                  placeholder={PATH_PLACEHOLDERS.CUSTOM_PROGRAM}
                  readOnly
                />
                <CompactButton
                  icon={<FolderOpenOutlined />}
                  onClick={handleBrowseCustomProgramPath}
                >
                  {t('common.browse')}
                </CompactButton>
              </Input.Group>
            </Form.Item>

            <Form.Item
              label={t('launch.game.customLaunchArgs')}
              name="customLaunchArgs"
              tooltip={t('launch.game.customArgsTooltip')}
            >
              <Input
                placeholder={t('launch.game.customArgsPlaceholder')}
                onChange={handleCustomLaunchArgsChange}
              />
            </Form.Item>

            <CompactDivider />

            <CompactSpace size="middle" wrap>
              <CompactButton
                type="primary"
                size="large"
                icon={<PlayCircleOutlined />}
                onClick={handleLaunchCustomProgram}
              >
                {t('launch.game.launchCustom')}
              </CompactButton>
              <CompactButton
                size="large"
                icon={<FolderOpenOutlined />}
                onClick={handleOpenCustomDirectory}
              >
                {t('launch.game.openProgramDir')}
              </CompactButton>
            </CompactSpace>
          </CompactCard>
        </Form>

        {/* Unity Args Dialog */}
        <UnityArgsDialog
          visible={unityArgsDialogVisible}
          currentArgs={form.getFieldValue('launchArgs') || ''}
          onSave={handleSaveUnityArgs}
          onCancel={() => setUnityArgsDialogVisible(false)}
        />
    </div>
  );
};
