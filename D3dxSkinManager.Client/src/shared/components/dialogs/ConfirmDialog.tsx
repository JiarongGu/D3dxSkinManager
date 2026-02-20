/**
 * ConfirmDialog Component
 * Reusable confirmation dialog with proper theming and centering
 */

import React from 'react';
import { Modal } from 'antd';
import { ExclamationCircleOutlined, CloseOutlined } from '@ant-design/icons';
import { CompactButton, CompactSpace, CompactDangerButton } from '../compact';
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
  icon = <ExclamationCircleOutlined style={{ color: '#faad14', fontSize: '22px' }} />,
  onOk,
  onCancel,
}) => {
  const [loading, setLoading] = React.useState(false);
  const loadingTimeoutRef = React.useRef<NodeJS.Timeout | null>(null);
  const isProcessingRef = React.useRef(false);

  // Reset loading state when dialog visibility changes
  React.useEffect(() => {
    if (!visible) {
      setLoading(false);
      isProcessingRef.current = false;
      if (loadingTimeoutRef.current) {
        clearTimeout(loadingTimeoutRef.current);
        loadingTimeoutRef.current = null;
      }
    }
  }, [visible]);

  const handleOk = async () => {
    // Prevent multiple clicks while processing (without disabling button)
    if (isProcessingRef.current) {
      return;
    }
    isProcessingRef.current = true;

    // Only show loading spinner if operation takes longer than 50ms
    loadingTimeoutRef.current = setTimeout(() => {
      setLoading(true);
    }, 50);

    try {
      await onOk();
    } finally {
      if (loadingTimeoutRef.current) {
        clearTimeout(loadingTimeoutRef.current);
        loadingTimeoutRef.current = null;
      }
      setLoading(false);
      isProcessingRef.current = false;
    }
  };

  return (
    <Modal
      className="confirm-dialog"
      title={
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
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
        <CompactSpace style={{ justifyContent: 'flex-end', width: '100%' }}>
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
      <div style={{ fontSize: '14px', lineHeight: '1.5' }}>
        {content}
      </div>
    </Modal>
  );
};
