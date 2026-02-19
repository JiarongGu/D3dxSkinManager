import React from 'react';
import { Modal } from 'antd';

interface FullScreenPreviewProps {
  visible: boolean;
  imageSrc: string;
  imageAlt?: string;
  onClose: () => void;
}

export const FullScreenPreview: React.FC<FullScreenPreviewProps> = ({
  visible,
  imageSrc,
  imageAlt = 'Full screen preview',
  onClose,
}) => {
  return (
    <Modal
      open={visible}
      onCancel={onClose}
      footer={null}
      width="100vw"
      style={{
        top: 0,
        maxWidth: 'none',
        padding: 0,
        margin: 0,
      }}
      styles={{
        body: {
          height: '100vh',
          padding: 0,
          margin: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          background: 'rgba(0, 0, 0, 0.95)',
        }
      }}
      closeIcon={
        <span style={{ color: '#fff', fontSize: '24px' }}>×</span>
      }
      maskStyle={{ background: 'rgba(0, 0, 0, 0.95)' }}
      destroyOnHidden
    >
      <img
        src={imageSrc}
        alt={imageAlt}
        style={{
          maxWidth: '95vw',
          maxHeight: '95vh',
          width: 'auto',
          height: 'auto',
          objectFit: 'contain',
          cursor: 'pointer',
        }}
        onClick={onClose}
        title="Click to close"
      />
    </Modal>
  );
};
