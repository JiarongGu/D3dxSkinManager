/**
 * FormDialog Component
 * Reusable form dialog with proper theming and centering
 * For modals that contain forms or complex interactions
 */

import React from 'react';
import { Modal } from 'antd';
import { CloseOutlined } from '@ant-design/icons';
import { CompactButton, CompactSpace } from '../compact';
import { useDelayedLoading } from '../../hooks/useDelayedLoading';
import './FormDialog.css';

interface FormDialogProps {
  visible: boolean;
  title: React.ReactNode;
  children: React.ReactNode;
  okText?: string;
  cancelText?: string;
  onOk?: () => void | Promise<void>;
  onCancel: () => void;
  width?: number;
  footer?: React.ReactNode;
  destroyOnClose?: boolean;
}

export const FormDialog: React.FC<FormDialogProps> = ({
  visible,
  title,
  children,
  okText = 'OK',
  cancelText = 'Cancel',
  onOk,
  onCancel,
  width = 520,
  footer,
  destroyOnClose = false,
}) => {
  const { loading, execute, reset } = useDelayedLoading(50);

  // Reset loading state when dialog visibility changes
  React.useEffect(() => {
    if (!visible) {
      reset();
    }
  }, [visible, reset]);

  const handleOk = async () => {
    if (!onOk) return;

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

  // Use custom footer if provided, otherwise use default
  const modalFooter = footer !== undefined ? footer : (
    <CompactSpace className="form-dialog-footer">
      <CompactButton onClick={onCancel}>
        {cancelText}
      </CompactButton>
      {onOk && (
        <CompactButton
          type="primary"
          loading={loading}
          onClick={handleOk}
        >
          {okText}
        </CompactButton>
      )}
    </CompactSpace>
  );

  return (
    <Modal
      className="form-dialog"
      title={title}
      open={visible}
      onCancel={onCancel}
      centered
      transitionName=""
      maskTransitionName=""
      closeIcon={
        <div className="form-dialog-close-button">
          <CloseOutlined />
        </div>
      }
      footer={modalFooter}
      width={width}
      destroyOnClose={destroyOnClose}
    >
      <div className="form-dialog-content">
        {children}
      </div>
    </Modal>
  );
};
