/**
 * InfoDialog Component
 * Reusable information display dialog with proper theming and centering
 * For modals that display read-only information (About, Help, etc.)
 */

import React from 'react';
import { Modal } from 'antd';
import { CloseOutlined } from '@ant-design/icons';
import './InfoDialog.css';

interface InfoDialogProps {
  visible: boolean;
  title: React.ReactNode;
  children: React.ReactNode;
  onClose: () => void;
  width?: number;
  footer?: React.ReactNode;
  bodyStyle?: React.CSSProperties;
}

export const InfoDialog: React.FC<InfoDialogProps> = ({
  visible,
  title,
  children,
  onClose,
  width = 600,
  footer = null,
  bodyStyle,
}) => {
  return (
    <Modal
      className="info-dialog"
      title={title}
      open={visible}
      onCancel={onClose}
      centered
      transitionName=""
      maskTransitionName=""
      closeIcon={
        <div className="info-dialog-close-button">
          <CloseOutlined />
        </div>
      }
      footer={footer}
      width={width}
      styles={bodyStyle ? { body: bodyStyle } : undefined}
    >
      <div className="info-dialog-content">
        {children}
      </div>
    </Modal>
  );
};
