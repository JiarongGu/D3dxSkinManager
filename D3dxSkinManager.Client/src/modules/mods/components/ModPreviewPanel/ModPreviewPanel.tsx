import React, { useState } from "react";
import { Typography, Button, Empty, message, Space, Tag } from "antd";
import {
  CopyOutlined,
  LeftOutlined,
  RightOutlined,
  UserOutlined,
  TagsOutlined,
  FileTextOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from "@ant-design/icons";
import { GradingTag } from "../../../../shared/components/common/GradingTag";
import { FullScreenPreview } from "./FullScreenPreview";
import { toAppUrl } from "../../../../shared/utils/imageUrlHelper";
import { ModPreviewProvider, useModView } from "./ModPreviewContext";
import { ModInfo } from "../../../../shared/types/mod.types";
import "./ModPreviewPanel.css";

const { Text, Paragraph, Title } = Typography;

export const ModPreviewPanelContent: React.FC = () => {
  const { state } = useModView();
  const [fullScreenVisible, setFullScreenVisible] = useState(false);
  const [fullScreenImageSrc, setFullScreenImageSrc] = useState<string>("");
  const [currentImageIndex, setCurrentImageIndex] = useState(0);
  const [showLeftButton, setShowLeftButton] = useState(false);
  const [showRightButton, setShowRightButton] = useState(false);

  const mod = state.currentMod;

  // Reset image index when mod changes (must be before early return)
  React.useEffect(() => {
    setCurrentImageIndex(0);
  }, [mod?.sha]);

  const handleCopySHA = () => {
    if (!mod) return;
    navigator.clipboard.writeText(mod.sha);
    message.success("SHA copied to clipboard");
  };

  const handleImageClick = (imageSrc: string) => {
    setFullScreenImageSrc(imageSrc);
    setFullScreenVisible(true);
  };

  // Determine which images to show (thumbnail + preview paths)
  const allImagePaths: string[] = [];

  // Add thumbnail first if available
  if (mod?.thumbnailPath) {
    allImagePaths.push(mod.thumbnailPath);
  }

  // Add preview paths
  if (state.previewPaths && state.previewPaths.length > 0) {
    allImagePaths.push(...state.previewPaths);
  }

  const hasMultipleImages = allImagePaths.length > 1;

  // Navigation handlers
  const handlePreviousImage = () => {
    setCurrentImageIndex((prev) =>
      prev > 0 ? prev - 1 : allImagePaths.length - 1,
    );
  };

  const handleNextImage = () => {
    setCurrentImageIndex((prev) =>
      prev < allImagePaths.length - 1 ? prev + 1 : 0,
    );
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

  if (!mod) {
    return (
      <div className="mod-preview-empty">
        <Empty
          description="Select a mod to view details"
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </div>
    );
  }

  return (
    <div className="mod-preview-panel">
      {/* Header Section */}
      <div className="mod-preview-header">
        <div className="mod-preview-header-content">
          <div className="mod-preview-header-title">
            <Title level={4} className="mod-preview-title">
              {mod.name}
            </Title>
            {mod.category && (
              <Text type="secondary" className="mod-preview-category">
                <FileTextOutlined className="mod-preview-category-icon" />
                {mod.category}
              </Text>
            )}
          </div>
          <Space size="small">
            {mod.isLoaded ? (
              <Tag icon={<CheckCircleOutlined />} color="success">
                Loaded
              </Tag>
            ) : (
              <Tag icon={<CloseCircleOutlined />} color="default">
                Not Loaded
              </Tag>
            )}
            <GradingTag grading={mod.grading} />
          </Space>
        </div>
      </div>

      {/* Image Preview Section */}
      <div className="mod-preview-image-section">
        {allImagePaths.length > 0 ? (
          <>
            {/* Image Display */}
            <div
              className="mod-preview-image-container"
              onMouseMove={handleMouseMove}
              onMouseLeave={handleMouseLeave}
            >
              <img
                className="mod-preview-image"
                alt={`${mod.name} - Preview ${currentImageIndex + 1}`}
                src={toAppUrl(allImagePaths[currentImageIndex]) || undefined}
                onClick={() =>
                  handleImageClick(
                    toAppUrl(allImagePaths[currentImageIndex]) || "",
                  )
                }
                title="Click to view full screen"
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
                  title="Previous"
                >
                  <div className="mod-preview-nav-icon">
                    <LeftOutlined style={{ fontSize: "12px" }} />
                  </div>
                </div>
              )}

              {/* Right Navigation Button - Windows Gallery style */}
              {showRightButton && (
                <div
                  className="mod-preview-nav-button mod-preview-nav-button-right"
                  onClick={handleNextImage}
                  title="Next"
                >
                  <div className="mod-preview-nav-icon">
                    <RightOutlined style={{ fontSize: "12px" }} />
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
        ) : (
          <div className="mod-preview-no-image">
            <Empty
              description="No Preview Available"
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          </div>
        )}
      </div>

      {/* Info Section */}
      <div className="mod-preview-info">
        {/* Author */}
        {mod.author && (
          <div className="mod-preview-info-item">
            <Text type="secondary" className="mod-preview-info-label">
              <UserOutlined style={{ marginRight: "4px" }} />
              Author
            </Text>
            <Text className="mod-preview-info-value">{mod.author}</Text>
          </div>
        )}

        {/* Tags */}
        {mod.tags && mod.tags.length > 0 && (
          <div className="mod-preview-info-item">
            <Text
              type="secondary"
              className="mod-preview-info-label mod-preview-info-label-with-margin"
            >
              <TagsOutlined style={{ marginRight: "4px" }} />
              Tags
            </Text>
            <Space size={[4, 4]} wrap>
              {mod.tags.map((tag, index) => (
                <Tag key={index} style={{ fontSize: "11px", margin: 0 }}>
                  {tag}
                </Tag>
              ))}
            </Space>
          </div>
        )}

        {/* Description */}
        {mod.description && (
          <div className="mod-preview-info-item">
            <Text
              type="secondary"
              className="mod-preview-info-label mod-preview-info-label-with-margin"
            >
              Description
            </Text>
            <Paragraph
              className="mod-preview-description"
              ellipsis={{ rows: 3, expandable: true, symbol: "more" }}
            >
              {mod.description}
            </Paragraph>
          </div>
        )}
      </div>

      {/* SHA Section - Fixed at Bottom */}
      <div className="mod-preview-sha">
        <div className="mod-preview-sha-content">
          <Text type="secondary" className="mod-preview-sha-label">
            SHA256:
          </Text>
          <Text
            className="mod-preview-sha-value"
            onClick={handleCopySHA}
            title="Click to copy full SHA"
          >
            {mod.sha}
          </Text>
          <Button
            type="text"
            size="small"
            icon={<CopyOutlined />}
            onClick={handleCopySHA}
            title="Copy SHA to clipboard"
            className="mod-preview-sha-button"
          />
        </div>
      </div>

      {/* Full Screen Preview Dialog */}
      <FullScreenPreview
        visible={fullScreenVisible}
        imageSrc={fullScreenImageSrc}
        imageAlt={mod.name}
        onClose={() => setFullScreenVisible(false)}
      />
    </div>
  );
};

export const ModPreviewPanel: React.FC<{ mod: ModInfo | null }> = ({ mod }) => {
  return (
    <ModPreviewProvider mod={mod}>
      <ModPreviewPanelContent />
    </ModPreviewProvider>
  );
};
