import React, { useState, useEffect } from 'react';
import { Form, Input, Button, Card, Space, message, Divider, Tooltip, Alert, Spin } from 'antd';
import {
  FolderOpenOutlined,
  PlayCircleOutlined,
  PlusOutlined,
  SettingOutlined,
} from '@ant-design/icons';
import { UnityArgsDialog } from '../../core/components/dialogs/UnityArgsDialog';
import { fileDialogService } from '../../../shared/services/fileDialogService';
import { getActiveProfileConfig, updateActiveProfileConfigField } from '../../profiles/services/profileConfigService';
import { useProfile } from '../../../shared/context/ProfileContext';

export const GameLaunchTab: React.FC = () => {
  const [form] = Form.useForm();
  const [unityArgsDialogVisible, setUnityArgsDialogVisible] = useState(false);
  const [loading, setLoading] = useState(true);
  const { state: profileState } = useProfile();

  // Load configuration when profile changes
  useEffect(() => {
    const loadConfig = async () => {
      // Only load if we have a selected profile
      if (!profileState.selectedProfile) {
        console.log('[GameLaunchTab] No profile selected, skipping config load');
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        console.log('[GameLaunchTab] Loading config for profile:', profileState.selectedProfile.name);
        const config = await getActiveProfileConfig(profileState.selectedProfile.id);
        if (config) {
          form.setFieldsValue({
            gamePath: config.gamePath || '',
            launchArgs: config.gameLaunchArgs || '',
            customProgramPath: config.customProgramPath || '',
            customLaunchArgs: config.customProgramArgs || '',
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
  }, [form, profileState.selectedProfile?.id]); // React to profile ID changes

  const handleBrowseGamePath = async () => {
    const result = await fileDialogService.openFileDialog({
      title: 'Select Game Executable',
      filters: [
        { name: 'Executable Files', extensions: ['exe'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });

    if (result.success && result.filePath) {
      form.setFieldsValue({ gamePath: result.filePath });
      if (!profileState.selectedProfile) {
        message.error('No profile selected');
        return;
      }
      try {
        await updateActiveProfileConfigField(profileState.selectedProfile.id, 'gamePath', result.filePath);
        message.success('Game path updated');
      } catch (error) {
        message.error('Failed to save game path');
      }
    }
  };

  const handleBrowseCustomProgramPath = async () => {
    const result = await fileDialogService.openFileDialog({
      title: 'Select Custom Program Executable',
      filters: [
        { name: 'Executable Files', extensions: ['exe'] },
        { name: 'All Files', extensions: ['*'] }
      ]
    });

    if (result.success && result.filePath) {
      form.setFieldsValue({ customProgramPath: result.filePath });
      if (!profileState.selectedProfile) {
        message.error('No profile selected');
        return;
      }
      try {
        await updateActiveProfileConfigField(profileState.selectedProfile.id, 'customProgramPath', result.filePath);
        message.success('Custom program path updated');
      } catch (error) {
        message.error('Failed to save custom program path');
      }
    }
  };

  const handleLaunchGame = () => {
    const gamePath = form.getFieldValue('gamePath');
    if (!gamePath) {
      message.error('Please set game path first');
      return;
    }
    // TODO: Launch game with arguments
    message.info('Launching game...');
  };

  const handleOpenGameDirectory = async () => {
    const gamePath = form.getFieldValue('gamePath');
    if (!gamePath) {
      message.error('Please set game path first');
      return;
    }
    try {
      await fileDialogService.openFileInExplorer(gamePath);
      message.success('Opened game directory');
    } catch (error) {
      message.error('Failed to open game directory');
    }
  };

  const handleLaunchCustomProgram = () => {
    const programPath = form.getFieldValue('customProgramPath');
    if (!programPath) {
      message.error('Please set custom program path first');
      return;
    }
    // TODO: Launch custom program
    message.info('Launching custom program...');
  };

  const handleOpenCustomDirectory = async () => {
    const programPath = form.getFieldValue('customProgramPath');
    if (!programPath) {
      message.error('Please set custom program path first');
      return;
    }
    try {
      await fileDialogService.openFileInExplorer(programPath);
      message.success('Opened custom program directory');
    } catch (error) {
      message.error('Failed to open custom program directory');
    }
  };

  const handleOpenUnityArgsDialog = () => {
    setUnityArgsDialogVisible(true);
  };

  const handleSaveUnityArgs = async (args: string) => {
    form.setFieldsValue({ launchArgs: args });
    setUnityArgsDialogVisible(false);
    if (!profileState.selectedProfile) {
      message.error('No profile selected');
      return;
    }
    try {
      await updateActiveProfileConfigField(profileState.selectedProfile.id, 'gameLaunchArgs', args);
      message.success('Launch arguments updated');
    } catch (error) {
      message.error('Failed to save launch arguments');
    }
  };

  const handleLaunchArgsChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!profileState.selectedProfile) return;
    try {
      await updateActiveProfileConfigField(profileState.selectedProfile.id, 'gameLaunchArgs', e.target.value);
    } catch (error) {
      console.error('Failed to auto-save launch args:', error);
    }
  };

  const handleCustomLaunchArgsChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!profileState.selectedProfile) return;
    try {
      await updateActiveProfileConfigField(profileState.selectedProfile.id, 'customProgramArgs', e.target.value);
    } catch (error) {
      console.error('Failed to auto-save custom launch args:', error);
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
          {/* Game Configuration */}
          <Card
            title={<><PlayCircleOutlined /> Game Configuration</>}
            style={{ marginBottom: '24px' }}
          >
            <Form.Item
              label="Game Executable Path"
              name="gamePath"
              tooltip="Path to the game executable for this profile"
            >
              <Input.Group compact>
                <Input
                  style={{ width: 'calc(100% - 100px)' }}
                  placeholder="C:\Program Files\Game\game.exe"
                  readOnly
                />
                <Button
                  icon={<FolderOpenOutlined />}
                  onClick={handleBrowseGamePath}
                >
                  Browse
                </Button>
              </Input.Group>
            </Form.Item>

            <Form.Item
              label="Launch Arguments"
              name="launchArgs"
              tooltip="Command line arguments for launching the game"
            >
              <Input.Group compact>
                <Input
                  style={{ width: 'calc(100% - 80px)' }}
                  placeholder="-windowed -dx11"
                  onChange={handleLaunchArgsChange}
                />
                <Tooltip title="Unity Arguments Helper">
                  <Button
                    icon={<PlusOutlined />}
                    onClick={handleOpenUnityArgsDialog}
                  >
                    Unity
                  </Button>
                </Tooltip>
              </Input.Group>
            </Form.Item>

            <Divider />

            <Space size="middle" wrap>
              <Button
                type="primary"
                size="large"
                icon={<PlayCircleOutlined />}
                onClick={handleLaunchGame}
              >
                Launch Game
              </Button>
              <Button
                size="large"
                icon={<FolderOpenOutlined />}
                onClick={handleOpenGameDirectory}
              >
                Open Game Directory
              </Button>
            </Space>
          </Card>

          {/* Custom Launch Program */}
          <Card
            title={<><SettingOutlined /> Custom Launch Program</>}
            style={{ marginBottom: '24px' }}
          >
            <Form.Item
              label="Custom Program Path"
              name="customProgramPath"
              tooltip="Path to a custom program to launch (e.g., ReShade, overlay, etc.)"
            >
              <Input.Group compact>
                <Input
                  style={{ width: 'calc(100% - 100px)' }}
                  placeholder="C:\Programs\CustomTool\tool.exe"
                  readOnly
                />
                <Button
                  icon={<FolderOpenOutlined />}
                  onClick={handleBrowseCustomProgramPath}
                >
                  Browse
                </Button>
              </Input.Group>
            </Form.Item>

            <Form.Item
              label="Custom Launch Arguments"
              name="customLaunchArgs"
              tooltip="Arguments for the custom program"
            >
              <Input
                placeholder="Custom arguments"
                onChange={handleCustomLaunchArgsChange}
              />
            </Form.Item>

            <Divider />

            <Space size="middle" wrap>
              <Button
                type="primary"
                size="large"
                icon={<PlayCircleOutlined />}
                onClick={handleLaunchCustomProgram}
              >
                Launch Custom Program
              </Button>
              <Button
                size="large"
                icon={<FolderOpenOutlined />}
                onClick={handleOpenCustomDirectory}
              >
                Open Program Directory
              </Button>
            </Space>
          </Card>
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
