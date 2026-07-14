/**
 * ConfirmDialog Component
 * Reusable confirmation dialog with proper theming and centering
 */

import React from 'react';
import { Modal } from 'antd';
import { ExclamationCircleOutlined, CloseOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton, CompactSpace, CompactDangerButton } from '../compact';
import { useDelayedLoading } from '../../hooks/useDelayedLoading';
import { handleError } from '../../utils/errorHandler';
import './ConfirmDialog.css';

interface ConfirmDialogProps {
  visible: boolean;
  title: React.ReactNode;
  content: React.ReactNode;
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
  okText,
  cancelText,
  okType = 'primary',
  icon = <ExclamationCircleOutlined className="confirm-dialog-icon" />,
  onOk,
  onCancel,
}) => {
  const { t } = useTranslation();
  const resolvedOkText = okText ?? t('common.ok');
  const resolvedCancelText = cancelText ?? t('common.cancel');
  const { loading, execute, reset } = useDelayedLoading(200);

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
    } catch (error: unknown) {
      // Silently ignore if operation already in progress
      if (error instanceof Error && error.message === 'Operation already in progress') {
        return;
      }
      // The onOk callback threw and didn't handle it. Surface a translated notification instead of
      // rethrowing (this runs in an async onClick — a rethrow becomes an unhandled promise rejection
      // with no UI feedback).
      handleError(error);
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
            {resolvedCancelText}
          </CompactButton>
          {okType === 'danger' ? (
            <CompactDangerButton
              loading={loading}
              onClick={handleOk}
            >
              {resolvedOkText}
            </CompactDangerButton>
          ) : (
            <CompactButton
              type={okType === 'primary' ? 'primary' : 'default'}
              loading={loading}
              onClick={handleOk}
            >
              {resolvedOkText}
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
