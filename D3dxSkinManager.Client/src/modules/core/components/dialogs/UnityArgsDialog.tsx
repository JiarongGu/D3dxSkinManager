import React, { useState, useEffect } from 'react';
import { Modal, Form, Switch, Select, InputNumber, Space, Divider, message } from 'antd';
import { SettingOutlined } from '@ant-design/icons';

const { Option } = Select;

interface UnityArgsDialogProps {
  visible: boolean;
  currentArgs?: string;
  onSave: (args: string) => void;
  onCancel: () => void;
}

/**
 * Unity game launch arguments configuration dialog
 * Provides a user-friendly interface for configuring common Unity launch parameters
 */
export const UnityArgsDialog: React.FC<UnityArgsDialogProps> = ({
  visible,
  currentArgs = '',
  onSave,
  onCancel,
}) => {
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);

  // Parse existing args when dialog opens
  useEffect(() => {
    if (visible) {
      parseArgsToForm(currentArgs);
    }
  }, [visible, currentArgs]);

  // Parse launch arguments string into form values
  const parseArgsToForm = (args: string) => {
    const values: any = {
      borderless: false,
      popupWindow: 'not-set',
      fullscreen: 'not-set',
      screenWidth: 1920,
      screenHeight: 1080,
    };

    if (args.includes('-popupwindow')) {
      values.borderless = true;
    }

    if (args.includes('-popupwindow')) {
      const match = args.match(/-popupwindow/);
      if (match) {
        values.popupWindow = 'enabled';
      }
    }

    if (args.includes('-screen-fullscreen 0')) {
      values.fullscreen = '0';
    } else if (args.includes('-screen-fullscreen 1')) {
      values.fullscreen = '1';
    }

    const widthMatch = args.match(/-screen-width (\d+)/);
    if (widthMatch) {
      values.screenWidth = parseInt(widthMatch[1]);
    }

    const heightMatch = args.match(/-screen-height (\d+)/);
    if (heightMatch) {
      values.screenHeight = parseInt(heightMatch[1]);
    }

    form.setFieldsValue(values);
  };

  // Build launch arguments string from form values
  const buildArgsFromForm = (values: any): string => {
    const args: string[] = [];

    // Borderless window
    if (values.borderless) {
      args.push('-popupwindow');
    }

    // Popup window
    if (values.popupWindow === 'enabled') {
      args.push('-popupwindow');
    }

    // Fullscreen
    if (values.fullscreen === '0') {
      args.push('-screen-fullscreen 0');
    } else if (values.fullscreen === '1') {
      args.push('-screen-fullscreen 1');
    }

    // Screen dimensions
    if (values.screenWidth) {
      args.push(`-screen-width ${values.screenWidth}`);
    }
    if (values.screenHeight) {
      args.push(`-screen-height ${values.screenHeight}`);
    }

    return args.join(' ');
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);

      const argsString = buildArgsFromForm(values);
      onSave(argsString);

      message.success('Unity launch arguments updated');
      onCancel();
    } catch (error) {
      console.error('Validation failed:', error);
      message.error('Please check all fields');
    } finally {
      setSaving(false);
    }
  };

  const handleReset = () => {
    form.resetFields();
  };

  return (
    <Modal
      title={
        <Space size={8}>
          <SettingOutlined />
          <span>Unity Launch Arguments</span>
        </Space>
      }
      open={visible}
      onCancel={onCancel}
      width={600}
      footer={[
        <Space key="actions" style={{ width: '100%', justifyContent: 'space-between' }}>
          <Space>
            <button
              key="reset"
              onClick={handleReset}
              style={{
                padding: '4px 15px',
                border: '1px solid #d9d9d9',
                borderRadius: '2px',
                background: '#fff',
                cursor: 'pointer',
              }}
            >
              Reset
            </button>
          </Space>
          <Space>
            <button
              key="cancel"
              onClick={onCancel}
              style={{
                padding: '4px 15px',
                border: '1px solid #d9d9d9',
                borderRadius: '2px',
                background: '#fff',
                cursor: 'pointer',
              }}
            >
              Cancel
            </button>
            <button
              key="save"
              onClick={handleSave}
              disabled={saving}
              style={{
                padding: '4px 15px',
                border: 'none',
                borderRadius: '2px',
                background: '#1890ff',
                color: '#fff',
                cursor: saving ? 'not-allowed' : 'pointer',
                opacity: saving ? 0.6 : 1,
              }}
            >
              {saving ? 'Saving...' : 'OK'}
            </button>
          </Space>
        </Space>,
      ]}
    >
      <Form
        form={form}
        layout="vertical"
        autoComplete="off"
        initialValues={{
          borderless: false,
          popupWindow: 'not-set',
          fullscreen: 'not-set',
          screenWidth: 1920,
          screenHeight: 1080,
        }}
      >
        {/* Borderless Window */}
        <Form.Item
          label="Borderless Window"
          name="borderless"
          valuePropName="checked"
          tooltip="Launch game in borderless window mode (-popupwindow)"
        >
          <Switch checkedChildren="Enabled" unCheckedChildren="Disabled" />
        </Form.Item>

        <Divider style={{ margin: '16px 0' }} />

        {/* Popup Window */}
        <Form.Item
          label="Popup Window Mode"
          name="popupWindow"
          tooltip="Control popup window behavior"
        >
          <Select>
            <Option value="not-set">不设置 (Not Set)</Option>
            <Option value="enabled">Enabled (-popupwindow)</Option>
          </Select>
        </Form.Item>

        <Divider style={{ margin: '16px 0' }} />

        {/* Fullscreen */}
        <Form.Item
          label="Fullscreen Mode"
          name="fullscreen"
          tooltip="Control fullscreen mode (0=windowed, 1=fullscreen)"
        >
          <Select>
            <Option value="not-set">不设置 (Not Set)</Option>
            <Option value="0">0 - Windowed Mode</Option>
            <Option value="1">1 - Fullscreen Mode</Option>
          </Select>
        </Form.Item>

        <Divider style={{ margin: '16px 0' }} />

        {/* Screen Dimensions */}
        <Form.Item label="Screen Dimensions">
          <Space size="middle" style={{ width: '100%' }}>
            <Form.Item
              label="Width"
              name="screenWidth"
              style={{ marginBottom: 0, flex: 1 }}
              tooltip="Screen width in pixels (-screen-width)"
            >
              <InputNumber
                min={640}
                max={7680}
                step={1}
                style={{ width: '100%' }}
                placeholder="1920"
              />
            </Form.Item>

            <span style={{ paddingTop: '30px' }}>×</span>

            <Form.Item
              label="Height"
              name="screenHeight"
              style={{ marginBottom: 0, flex: 1 }}
              tooltip="Screen height in pixels (-screen-height)"
            >
              <InputNumber
                min={480}
                max={4320}
                step={1}
                style={{ width: '100%' }}
                placeholder="1080"
              />
            </Form.Item>
          </Space>
        </Form.Item>

        <div
          style={{
            marginTop: '24px',
            padding: '12px',
            background: '#f5f5f5',
            borderRadius: '4px',
            fontSize: '12px',
            color: '#595959',
          }}
        >
          <strong>Common Resolutions:</strong>
          <div style={{ marginTop: '8px' }}>
            • 1920×1080 (Full HD)<br />
            • 2560×1440 (2K)<br />
            • 3840×2160 (4K)<br />
            • 1280×720 (HD)<br />
          </div>
        </div>
      </Form>
    </Modal>
  );
};
