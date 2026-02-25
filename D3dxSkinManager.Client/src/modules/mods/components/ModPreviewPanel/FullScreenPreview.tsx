import React, { useState } from 'react';
import classNames from 'classnames';
import { Modal, Spin, Typography } from 'antd';
import { CloseOutlined, LeftOutlined, RightOutlined } from '@ant-design/icons';
import './FullScreenPreview.css';

const { Text } = Typography;

interface FullScreenPreviewProps {
  visible: boolean;
  imageSrc: string;
  imageAlt?: string;
  onClose: () => void;
  allImages?: string[];
  initialIndex?: number;
}

export const FullScreenPreview: React.FC<FullScreenPreviewProps> = ({
  visible,
  imageSrc,
  imageAlt = 'Full screen preview',
  onClose,
  allImages = [],
  initialIndex = 0,
}) => {
  const [imageLoaded, setImageLoaded] = useState(false);
  const [imageError, setImageError] = useState(false);
  const [showLeftButton, setShowLeftButton] = useState(false);
  const [showRightButton, setShowRightButton] = useState(false);
  const [currentIndex, setCurrentIndex] = useState(initialIndex);
  const [showCounter, setShowCounter] = useState(true);

  const hasMultipleImages = allImages.length > 1;
  const currentImageSrc = hasMultipleImages ? allImages[currentIndex] : imageSrc;

  // Reset index when modal opens or initialIndex changes
  React.useEffect(() => {
    if (visible) {
      setCurrentIndex(initialIndex);
      setImageLoaded(false);
      setImageError(false);
    }
  }, [visible, initialIndex]);

  // Reset image loading states when image changes
  React.useEffect(() => {
    setImageLoaded(false);
    setImageError(false);
  }, [currentImageSrc]);

  // Show counter when currentIndex changes (navigation or initial load)
  React.useEffect(() => {
    if (!hasMultipleImages) return;

    setShowCounter(true);
    const timer = setTimeout(() => {
      setShowCounter(false);
    }, 200); // Start fading after 0.2s

    return () => clearTimeout(timer);
  }, [currentIndex, hasMultipleImages]);

  const handleImageLoad = () => {
    setImageLoaded(true);
  };

  const handleImageError = () => {
    setImageLoaded(true);
    setImageError(true);
  };

  // Handle mouse movement to show/hide navigation buttons
  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!hasMultipleImages) return;

    const container = e.currentTarget;
    const rect = container.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const containerWidth = rect.width;

    // Show left button when hovering on left 20% of the container
    const leftThreshold = containerWidth * 0.2;
    setShowLeftButton(x < leftThreshold);

    // Show right button when hovering on right 20% of the container
    const rightThreshold = containerWidth * 0.8;
    setShowRightButton(x > rightThreshold);
  };

  const handleMouseLeave = () => {
    setShowLeftButton(false);
    setShowRightButton(false);
  };

  // Navigation handlers for fullscreen
  const handlePreviousImage = () => {
    setCurrentIndex((prev) =>
      prev > 0 ? prev - 1 : allImages.length - 1
    );
  };

  const handleNextImage = () => {
    setCurrentIndex((prev) =>
      prev < allImages.length - 1 ? prev + 1 : 0
    );
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
      <div
        className="fullscreen-preview-content"
        onMouseMove={handleMouseMove}
        onMouseLeave={handleMouseLeave}
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
            key={`fullscreen-${currentIndex}-${currentImageSrc}`}
            src={currentImageSrc}
            alt={imageAlt}
            onLoad={handleImageLoad}
            onError={handleImageError}
            className={classNames('fullscreen-preview-image', { loaded: imageLoaded })}
          />
        )}

        {/* Left Navigation Button - Windows Gallery style */}
        {hasMultipleImages && showLeftButton && (
          <div
            className="fullscreen-nav-button fullscreen-nav-button-left"
            onClick={handlePreviousImage}
          >
            <div className="fullscreen-nav-icon">
              <LeftOutlined className="fullscreen-nav-icon-arrow" />
            </div>
          </div>
        )}

        {/* Right Navigation Button - Windows Gallery style */}
        {hasMultipleImages && showRightButton && (
          <div
            className="fullscreen-nav-button fullscreen-nav-button-right"
            onClick={handleNextImage}
          >
            <div className="fullscreen-nav-icon">
              <RightOutlined className="fullscreen-nav-icon-arrow" />
            </div>
          </div>
        )}

        {/* Image Counter - Only show if multiple images */}
        {hasMultipleImages && (
          <div className={classNames('fullscreen-image-counter', { visible: showCounter, hidden: !showCounter })}>
            <Text type="secondary" className="fullscreen-counter-text">
              {currentIndex + 1} / {allImages.length}
            </Text>
          </div>
        )}
      </div>
    </Modal>
  );
};
