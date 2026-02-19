import React, { useState } from 'react';
import { Modal, Spin } from 'antd';
import { CloseOutlined } from '@ant-design/icons';
import './FullScreenPreview.css';

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
  const [imageLoaded, setImageLoaded] = useState(false);
  const [imageError, setImageError] = useState(false);

  // Reset states when modal opens/closes
  React.useEffect(() => {
    if (visible) {
      setImageLoaded(false);
      setImageError(false);
    }
  }, [visible, imageSrc]);

  const handleImageLoad = () => {
    setImageLoaded(true);
  };

  const handleImageError = () => {
    setImageLoaded(true);
    setImageError(true);
  };

  return (
    <Modal
      open={visible}
      onCancel={onClose}
      footer={null}
      width="100vw"
      destroyOnHidden
      keyboard
      mask={{ closable: true }}
      transitionName=""
      maskTransitionName=""
      wrapClassName="fullscreen-preview-modal"
      closeIcon={
        <div className="fullscreen-close-button">
          <CloseOutlined />
        </div>
      }
    >
      {/* Loading spinner */}
      {!imageLoaded && !imageError && (
        <div
          style={{
            position: 'absolute',
            top: '50%',
            left: '50%',
            transform: 'translate(-50%, -50%)',
            zIndex: 2,
          }}
        >
          <Spin size="large" />
        </div>
      )}

      {/* Error message */}
      {imageError && (
        <div
          style={{
            color: '#fff',
            fontSize: '16px',
            textAlign: 'center',
          }}
        >
          Failed to load image
        </div>
      )}

      {/* Image */}
      {!imageError && (
        <img
          src={imageSrc}
          alt={imageAlt}
          onLoad={handleImageLoad}
          onError={handleImageError}
          className={imageLoaded ? 'fullscreen-preview-image loaded' : 'fullscreen-preview-image'}
          style={{
            maxWidth: 'calc(100vw - 100px)',
            maxHeight: 'calc(100vh - 120px)',
            width: 'auto',
            height: 'auto',
            objectFit: 'contain',
          }}
        />
      )}
    </Modal>
  );
};
