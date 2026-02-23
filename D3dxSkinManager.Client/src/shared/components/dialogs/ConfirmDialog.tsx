/**
 * ConfirmDialog Component
 * Reusable confirmation dialog with proper theming and centering
 */

import React from 'react';
import { Modal } from 'antd';
import { ExclamationCircleOutlined, CloseOutlined } from '@ant-design/icons';
import { CompactButton, CompactSpace, CompactDangerButton } from '../compact';
import { useDelayedLoading } from '../../hooks/useDelayedLoading';
import './ConfirmDialog.css';

interface ConfirmDialogProps {
  visible: boolean;
  title: string;
  content: string;
  okText?: string;
  cancelText?: string;
  okType?: 'primary' | 'danger' | 'default';
  icon?: React.ReactNode;
  onOk: () => void | Promise<void>;
  onCancel: () => void;
}

export const ConfirmDialog: React.FC<ConfirmDialogProps> = ({
  visible,
  title,
  content,
  okText = 'OK',
  cancelText = 'Cancel',
  okType = 'primary',
  icon = <ExclamationCircleOutlined className="confirm-dialog-icon" />,
  onOk,
  onCancel,
}) => {
  const { loading, execute, reset } = useDelayedLoading(50);

  // Reset loading state when dialog visibility changes
  React.useEffect(() => {
    if (!visible) {
      reset();
    }
  }, [visible, reset]);

  const handleOk = async () => {
    try {
      await execute(async () => {
        await onOk();
      });
    } catch (error) {
      // Silently ignore if operation already in progress
      if (error instanceof Error && error.message === 'Operation already in progress') {
        return;
      }
      throw error;
    }
  };

  return (
    <Modal
      className="confirm-dialog"
      title={
        <div className="confirm-dialog-title">
          {icon}
          <span>{title}</span>
        </div>
      }
      open={visible}
      onCancel={onCancel}
      centered
      transitionName=""
      maskTransitionName=""
      closeIcon={
        <div className="confirm-dialog-close-button">
          <CloseOutlined />
        </div>
      }
      footer={
        <CompactSpace className="confirm-dialog-footer">
          <CompactButton onClick={onCancel}>
            {cancelText}
          </CompactButton>
          {okType === 'danger' ? (
            <CompactDangerButton
              loading={loading}
              onClick={handleOk}
            >
              {okText}
            </CompactDangerButton>
          ) : (
            <CompactButton
              type={okType === 'primary' ? 'primary' : 'default'}
              loading={loading}
              onClick={handleOk}
            >
              {okText}
            </CompactButton>
          )}
        </CompactSpace>
      }
      width={420}
    >
      <div className="confirm-dialog-content">
        {content}
      </div>
    </Modal>
  );
};
