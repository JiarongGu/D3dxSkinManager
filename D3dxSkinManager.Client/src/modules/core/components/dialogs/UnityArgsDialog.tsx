import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect } from 'react';
import { Modal, Form, Switch, Select, InputNumber, Space, Divider } from 'antd';
import { SettingOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { SCREEN_RESOLUTIONS, RESOLUTION_DEFAULTS } from '../../../../shared/constants/ui.constants';
import './UnityArgsDialog.css';

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
  const { t } = useTranslation();
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

      notification.success(t('unityArgs.updated'));
      onCancel();
    } catch (error: unknown) {
            notification.error(t('unityArgs.checkFields'));
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
          <span>{t('unityArgs.title')}</span>
        </Space>
      }
      open={visible}
      transitionName=""
      maskTransitionName=""
      onCancel={onCancel}
      width={600}
      footer={[
        <Space key="actions" className="unity-args-footer">
          <Space>
            <button
              key="reset"
              onClick={handleReset}
              className="unity-args-button"
            >
              {t('unityArgs.reset')}
            </button>
          </Space>
          <Space>
            <button
              key="cancel"
              onClick={onCancel}
              className="unity-args-button"
            >
              {t('common.cancel')}
            </button>
            <button
              key="save"
              onClick={handleSave}
              disabled={saving}
              className="unity-args-button-primary"
            >
              {saving ? t('unityArgs.saving') : t('unityArgs.ok')}
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
          label={t('unityArgs.borderless')}
          name="borderless"
          valuePropName="checked"
          tooltip={t('unityArgs.borderlessTooltip')}
        >
          <Switch checkedChildren={t('unityArgs.enabled')} unCheckedChildren={t('unityArgs.disabled')} />
        </Form.Item>

        <Divider className="unity-args-divider" />

        {/* Popup Window */}
        <Form.Item
          label={t('unityArgs.popupWindow')}
          name="popupWindow"
          tooltip={t('unityArgs.popupWindowTooltip')}
        >
          <Select>
            <Option value="not-set">{t('unityArgs.notSet')}</Option>
            <Option value="enabled">{t('unityArgs.popupWindowEnabled')}</Option>
          </Select>
        </Form.Item>

        <Divider className="unity-args-divider" />

        {/* Fullscreen */}
        <Form.Item
          label={t('unityArgs.fullscreen')}
          name="fullscreen"
          tooltip={t('unityArgs.fullscreenTooltip')}
        >
          <Select>
            <Option value="not-set">{t('unityArgs.notSet')}</Option>
            <Option value="0">{t('unityArgs.windowedMode')}</Option>
            <Option value="1">{t('unityArgs.fullscreenMode')}</Option>
          </Select>
        </Form.Item>

        <Divider className="unity-args-divider" />

        {/* Screen Dimensions */}
        <Form.Item label={t('unityArgs.screenDimensions')}>
          <Space size="middle" className="unity-args-dimensions-container">
            <Form.Item
              label={t('unityArgs.width')}
              name="screenWidth"
              className="unity-args-dimension-item"
              tooltip={t('unityArgs.widthTooltip')}
            >
              <InputNumber
                min={640}
                max={7680}
                step={1}
                className="unity-args-dimension-input"
                placeholder={RESOLUTION_DEFAULTS.WIDTH}
              />
            </Form.Item>

            <span className="unity-args-dimension-separator">×</span>

            <Form.Item
              label={t('unityArgs.height')}
              name="screenHeight"
              className="unity-args-dimension-item"
              tooltip={t('unityArgs.heightTooltip')}
            >
              <InputNumber
                min={480}
                max={4320}
                step={1}
                className="unity-args-dimension-input"
                placeholder={RESOLUTION_DEFAULTS.HEIGHT}
              />
            </Form.Item>
          </Space>
        </Form.Item>

        <div className="unity-args-info-box">
          <strong>{t('unityArgs.commonResolutions')}</strong>
          <div className="unity-args-info-resolutions">
            �?{SCREEN_RESOLUTIONS.FULL_HD.label}<br />
            �?{SCREEN_RESOLUTIONS['2K'].label}<br />
            �?{SCREEN_RESOLUTIONS['4K'].label}<br />
            �?{SCREEN_RESOLUTIONS.HD.label}<br />
          </div>
        </div>
      </Form>
    </Modal>
  );
};
