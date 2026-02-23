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
        <div className="fullscreen-preview-loading">
          <Spin size="large" />
        </div>
      )}

      {/* Error message */}
      {imageError && (
        <div className="fullscreen-preview-error">
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
        />
      )}
    </Modal>
  );
};
