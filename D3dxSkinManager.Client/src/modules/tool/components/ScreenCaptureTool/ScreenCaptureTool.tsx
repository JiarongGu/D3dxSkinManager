import React, { useState, useEffect } from 'react';
import { Form, InputNumber, Button, Select, Space, message } from 'antd';
import { CameraOutlined, EyeOutlined, EyeInvisibleOutlined, PlusOutlined, SaveOutlined, DeleteOutlined } from '@ant-design/icons';
import { api } from '../../../../shared/services/ipc';
import { eventBus, Module, ToolsEventType } from '../../../../shared/services/eventBus';
import type { ScreenCaptureProfile } from '../../../../shared/types/capture.types';
import { handleError } from '../../../../shared/utils/errorHandler';
import './ScreenCaptureTool.css';

const newProfileId = '__new__';

/**
 * Screen Capture Control Panel
 * Standalone window for managing capture profiles and showing capture area overlay
 */
export const ScreenCaptureTool: React.FC = () => {
  // Read profileId from URL query parameter (passed by backend)
  const urlParams = new URLSearchParams(window.location.search);
  const currentProfileId = urlParams.get('profileId') || undefined;
  const [form] = Form.useForm();
  const [profiles, setProfiles] = useState<ScreenCaptureProfile[]>([]);
  const [selectedProfileId, setSelectedProfileId] = useState<string | undefined>();
  const [loading, setLoading] = useState(false);
  const [showingBorder, setShowingBorder] = useState(false);
  const [isNewProfile, setIsNewProfile] = useState(false);

  useEffect(() => {
    loadProfiles();

    // Listen to EventHub for bounds changes from overlay
    const unsubscribe = eventBus.subscribe(Module.TOOL, ToolsEventType.CAPTURE_BOUNDS_CHANGED, (event) => {
      console.log('[CapturePanel] Overlay bounds changed:', event.payload);
      if (event.payload) {
        form.setFieldsValue({
          x: event.payload.x,
          y: event.payload.y,
          width: event.payload.width,
          height: event.payload.height,
        });
      }
    });

    return () => {
      unsubscribe();
    };
  }, []);

  const loadProfiles = async () => {
    try {
      setLoading(true);
      const data = await api.tool.getProfiles();

      // If no profiles exist, create a default one
      if (data.length === 0) {
        await api.tool.saveProfile({
          name: 'Default',
          x: 0,
          y: 0,
          width: 1920,
          height: 1080
        });
        // Reload profiles after creating default
        const updatedData = await api.tool.getProfiles();
        setProfiles(updatedData);
        const defaultProfile = updatedData[0];
        if (defaultProfile) {
          setSelectedProfileId(defaultProfile.id);
          form.setFieldsValue({
            x: defaultProfile.x,
            y: defaultProfile.y,
            width: defaultProfile.width,
            height: defaultProfile.height,
          });
        }
      } else {
        setProfiles(data);
        // Select default profile or first profile
        const defaultProfile = data[0];
        if (defaultProfile) {
          setSelectedProfileId(defaultProfile.id);
          form.setFieldsValue({
            x: defaultProfile.x,
            y: defaultProfile.y,
            width: defaultProfile.width,
            height: defaultProfile.height,
          });
        }
      }
    } catch (error) {
      handleError(error);
    } finally {
      setLoading(false);
    }
  };

  const handleProfileChange = (profileId: string) => {
    // Handle <New Profile> selection
    if (profileId === '__new__') {
      handleNewProfile();
      return;
    }

    setIsNewProfile(false);
    setSelectedProfileId(profileId);
    const profile = profiles.find(p => p.id === profileId);
    if (profile) {
      form.setFieldsValue({
        x: profile.x,
        y: profile.y,
        width: profile.width,
        height: profile.height,
      });
    }
  };

  const handleNewProfile = () => {
    // Auto-create new unsaved profile without prompting
    setIsNewProfile(true);
    setSelectedProfileId(undefined);
    form.setFieldsValue({
      x: 0,
      y: 0,
      width: 1920,
      height: 1080,
    });
    message.info('New profile created. Click Save to keep it.');
  };

  const handleSaveProfile = async () => {
    const values = form.getFieldsValue();

    // If it's a new profile, prompt for name
    if (isNewProfile) {
      const name = prompt('Enter profile name:');
      if (!name) return;

      try {
        await api.tool.saveProfile({
          name,
          x: values.x || 0,
          y: values.y || 0,
          width: values.width || 1920,
          height: values.height || 1080,
        });
        message.success('Profile created');
        setIsNewProfile(false);
        await loadProfiles();
      } catch (error) {
        handleError(error);
      }
      return;
    }

    // Otherwise update existing profile
    if (!selectedProfileId) {
      message.warning('No profile selected');
      return;
    }

    const profile = profiles.find(p => p.id === selectedProfileId);
    if (!profile) return;

    try {
      await api.tool.saveProfile({
        ...profile,
        x: values.x || 0,
        y: values.y || 0,
        width: values.width || 1920,
        height: values.height || 1080,
      });
      message.success('Profile updated');
      await loadProfiles();
    } catch (error) {
      handleError(error);
    }
  };

  const handleDeleteProfile = async () => {
    if (!selectedProfileId) {
      message.warning('No profile selected');
      return;
    }

    if (!confirm('Delete this profile?')) return;

    try {
      await api.tool.deleteProfile(selectedProfileId);
      message.success('Profile deleted');
      setSelectedProfileId(undefined);
      await loadProfiles();
    } catch (error) {
      handleError(error);
    }
  };

  const handleToggleBorder = async () => {
    if (!currentProfileId) {
      message.error('No active profile');
      return;
    }

    try {
      if (showingBorder) {
        console.log('[CapturePanel] Hiding border overlay');
        await api.tool.hideBorder(currentProfileId);
        setShowingBorder(false);
      } else {
        const values = form.getFieldsValue();
        const x = values.x ?? 0;
        const y = values.y ?? 0;
        const width = values.width ?? 1920;
        const height = values.height ?? 1080;

        console.log('[CapturePanel] Showing border overlay:', { currentProfileId, x, y, width, height });
        await api.tool.showBorder(currentProfileId, x, y, width, height);
        setShowingBorder(true);
      }
    } catch (error) {
      console.error('[CapturePanel] Error toggling border:', error);
      handleError(error);
    }
  };

  const handleCapture = async () => {
    try {
      const values = form.getFieldsValue();
      const x = values.x ?? 0;
      const y = values.y ?? 0;
      const width = values.width ?? 1920;
      const height = values.height ?? 1080;

      const result = await api.tool.captureScreen({
        x,
        y,
        width,
        height,
        copyToClipboard: true,
        saveToFile: false,
      });

      if (result.success) {
        message.success('Screen captured and copied to clipboard');
      } else {
        message.error(result.errorMessage || 'Capture failed');
      }
    } catch (error) {
      handleError(error);
    }
  };

  return (
    <div className='screen-capture-tool'>
      {/* Row 1: Profile selection and management */}
      <Space style={{ width: '100%' }} size="small">
        <Select
          style={{ width: 256 }}
          placeholder="Select profile"
          value={isNewProfile ? newProfileId : selectedProfileId ?? newProfileId}
          onChange={handleProfileChange}
          loading={loading}
          size="small"
          options={[
            ...profiles.map(p => ({
              label: p.name,
              value: p.id,
            })),
            { label: '<New Profile>', value: newProfileId }
          ]}
        />
        <Button size="small" icon={<PlusOutlined />} onClick={handleNewProfile} title="New Profile" />
        <Button
          size="small"
          icon={<SaveOutlined />}
          onClick={handleSaveProfile}
          disabled={!selectedProfileId && !isNewProfile}
          title="Save Profile"
        />
        <Button
          size="small"
          icon={<DeleteOutlined />}
          onClick={handleDeleteProfile}
          disabled={!selectedProfileId || isNewProfile}
          danger
          title="Delete Profile"
        />
      </Space>

      {/* Row 2: Position and Size - labels inline with inputs */}
      <Form className='screen-capture-tool-form' form={form} layout="inline" size="small">
        <Form.Item label="X" name="x" style={{ marginBottom: 0 }}>
          <InputNumber style={{ width: 60 }} min={-10000} max={10000} />
        </Form.Item>
        <Form.Item label="Y" name="y" style={{ marginBottom: 0 }}>
          <InputNumber style={{ width: 60 }} min={-10000} max={10000} />
        </Form.Item>
        <Form.Item label="W" name="width" style={{ marginBottom: 0 }}>
          <InputNumber style={{ width: 60 }} min={1} max={10000} />
        </Form.Item>
        <Form.Item label="H" name="height" style={{ marginBottom: 0 }}>
          <InputNumber style={{ width: 60 }} min={1} max={10000} />
        </Form.Item>
      </Form>

      {/* Row 3: Actions */}
      <Space size="small">
        <Button
          size="small"
          type={showingBorder ? 'default' : 'primary'}
          icon={showingBorder ? <EyeInvisibleOutlined /> : <EyeOutlined />}
          onClick={handleToggleBorder}
        >
          {showingBorder ? 'Hide Area' : 'Show Area'}
        </Button>
        <Button
          size="small"
          type="primary"
          icon={<CameraOutlined />}
          onClick={handleCapture}
        >
          Capture
        </Button>
      </Space>
    </div>
  );
};
