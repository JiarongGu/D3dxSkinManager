import React, { useState } from "react";
import { Typography, Empty } from "antd";
import { useTranslation } from "react-i18next";
import { LeftOutlined, RightOutlined } from "@ant-design/icons";
import { toAppUrl } from "../../../../shared/utils/imageUrlHelper";
import { ModInfo } from "../../../../shared/types/mod.types";

const { Text } = Typography;

interface PreviewImageCarouselProps {
  mod: ModInfo;
  allImagePaths: string[];
  cacheTimestamp: number;
  currentImageIndex: number;
  onImageIndexChange: (index: number) => void;
  onImageClick: (imageSrc: string) => void;
  onContextMenu: (e: React.MouseEvent) => void;
}

export const PreviewImageCarousel: React.FC<PreviewImageCarouselProps> = ({
  mod,
  allImagePaths,
  cacheTimestamp,
  currentImageIndex,
  onImageIndexChange,
  onImageClick,
  onContextMenu,
}) => {
  const { t } = useTranslation();
  const [showLeftButton, setShowLeftButton] = useState(false);
  const [showRightButton, setShowRightButton] = useState(false);

  const hasMultipleImages = allImagePaths.length > 1;

  // No content veil here — the veil covers the REMOTE library grid only (local mods are the
  // user's own imports; user decision 2026-07-10).
  const currentImageUrl = toAppUrl(allImagePaths[currentImageIndex], cacheTimestamp);

  // Navigation handlers
  const handlePreviousImage = () => {
    const newIndex = currentImageIndex > 0 ? currentImageIndex - 1 : allImagePaths.length - 1;
    onImageIndexChange(newIndex);
  };

  const handleNextImage = () => {
    const newIndex = currentImageIndex < allImagePaths.length - 1 ? currentImageIndex + 1 : 0;
    onImageIndexChange(newIndex);
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

  if (allImagePaths.length === 0) {
    return (
      <div
        className="mod-preview-no-image"
        onContextMenu={onContextMenu}
      >
        <Empty
          description={
            mod?.disablePreview
              ? t("mods.preview.previewDisabledMessage")
              : t("mods.preview.noPreview")
          }
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </div>
    );
  }

  return (
    <>
      {/* Image Display */}
      <div
        className="mod-preview-image-container"
        onMouseMove={handleMouseMove}
        onMouseLeave={handleMouseLeave}
        onContextMenu={onContextMenu}
      >
        <img
          key={`${allImagePaths[currentImageIndex]}-${cacheTimestamp}`}
          className="mod-preview-image"
          alt={t("mods.preview.imageAlt", {
            name: mod.name,
            index: currentImageIndex + 1,
          })}
          src={currentImageUrl || undefined}
          onClick={() => onImageClick(currentImageUrl || "")}
          title={t("mods.preview.clickFullScreen")}
          onError={(e) => {
            (e.target as HTMLImageElement).src =
              "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
          }}
        />

        {/* Left Navigation Button - Windows Gallery style */}
        {showLeftButton && (
          <div
            className="mod-preview-nav-button mod-preview-nav-button-left"
            onClick={handlePreviousImage}
            title={t("mods.preview.previous")}
          >
            <div className="mod-preview-nav-icon">
              <LeftOutlined className="mod-preview-nav-icon-arrow" />
            </div>
          </div>
        )}

        {/* Right Navigation Button - Windows Gallery style */}
        {showRightButton && (
          <div
            className="mod-preview-nav-button mod-preview-nav-button-right"
            onClick={handleNextImage}
            title={t("mods.preview.next")}
          >
            <div className="mod-preview-nav-icon">
              <RightOutlined className="mod-preview-nav-icon-arrow" />
            </div>
          </div>
        )}
      </div>

      {/* Image Counter - Only show if multiple images */}
      {hasMultipleImages && (
        <div className="mod-preview-image-counter">
          <Text type="secondary" className="mod-preview-counter-text">
            {currentImageIndex + 1} / {allImagePaths.length}
          </Text>
        </div>
      )}
    </>
  );
};
