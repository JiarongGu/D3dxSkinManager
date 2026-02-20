import { notification } from '../../../../shared/utils/notification';
import React, { useState } from "react";
import { Typography, Button, Empty,  Space, Tag } from "antd";
import {
  CopyOutlined,
  LeftOutlined,
  RightOutlined,
  UserOutlined,
  TagsOutlined,
  FileTextOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  PictureOutlined,
  FolderOpenOutlined,
  DeleteOutlined,
  ExclamationCircleOutlined,
  PlusOutlined,
  SnippetsOutlined,
} from "@ant-design/icons";
import { ContextMenu, ContextMenuItem } from "../../../../shared/components/menu/ContextMenu";
import { ConfirmDialog } from "../../../../shared/components/dialogs";
import { GradingTag } from "../../../../shared/components/common/GradingTag";
import { FullScreenPreview } from "./FullScreenPreview";
import { toAppUrl } from "../../../../shared/utils/imageUrlHelper";
import { ModPreviewProvider, useModView } from "./ModPreviewContext";
import { ModInfo } from "../../../../shared/types/mod.types";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { modService } from "../../services/modService";
import { fileDialogService } from "../../../../shared/services/systemService";
import "./ModPreviewPanel.css";

const { Text, Paragraph, Title } = Typography;

export const ModPreviewPanelContent: React.FC = () => {
  const { state, actions } = useModView();
  const { selectedProfileId } = useProfile();
  const [fullScreenVisible, setFullScreenVisible] = useState(false);
  const [fullScreenImageSrc, setFullScreenImageSrc] = useState<string>("");
  const [currentImageIndex, setCurrentImageIndex] = useState(0);
  const [showLeftButton, setShowLeftButton] = useState(false);
  const [showRightButton, setShowRightButton] = useState(false);
  const [contextMenuVisible, setContextMenuVisible] = useState(false);
  const [contextMenuPosition, setContextMenuPosition] = useState({ x: 0, y: 0 });
  const [deleteConfirmVisible, setDeleteConfirmVisible] = useState(false);

  const mod = state.currentMod;
  const cacheTimestamp = state.cacheTimestamp;

  // Reset image index when mod changes (must be before early return)
  React.useEffect(() => {
    setCurrentImageIndex(0);
  }, [mod?.sha]);

  const handleCopySHA = () => {
    if (!mod) return;
    navigator.clipboard.writeText(mod.sha);
    notification.success("SHA copied to clipboard");
  };

  const handleImageClick = (imageSrc: string) => {
    setFullScreenImageSrc(imageSrc);
    setFullScreenVisible(true);
  };

  // Determine which images to show (preview paths, with thumbnail first)
  const allImagePaths: string[] = [];

  if (state.previewPaths && state.previewPaths.length > 0) {
    // Preview paths are already sorted alphabetically by the backend
    // The first preview is automatically used as the thumbnail
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

  // Context menu handlers
  const handleImageContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    setContextMenuPosition({ x: e.clientX, y: e.clientY });
    setContextMenuVisible(true);
  };

  const handleSetAsThumbnail = async () => {
    if (!mod || !selectedProfileId) return;
    const currentImagePath = allImagePaths[currentImageIndex];

    try {
      await modService.setThumbnail(selectedProfileId, mod.sha, currentImagePath);
      notification.success("Thumbnail updated successfully");
      // Refresh preview to update UI
      await actions.loadPreviewPaths(mod.sha);
    } catch (error) {
      console.error("Error setting thumbnail:", error);
      notification.error("Failed to set thumbnail");
    }
    setContextMenuVisible(false);
  };

  const handleOpenInExplorer = async () => {
    if (!mod) return;
    const currentImagePath = allImagePaths[currentImageIndex];

    try {
      await fileDialogService.openFileInExplorer(currentImagePath);
    } catch (error) {
      console.error("Error opening in explorer:", error);
      notification.error("Failed to open in file explorer");
    }
    setContextMenuVisible(false);
  };

  const handleCopyImagePath = async () => {
    if (!mod) return;
    const currentImagePath = allImagePaths[currentImageIndex];

    try {
      // Convert relative path to absolute for clipboard
      const absolutePath = await fileDialogService.getAbsolutePath(currentImagePath);
      await navigator.clipboard.writeText(absolutePath);
      notification.success("Image path copied to clipboard");
    } catch (error) {
      console.error("Error copying image path:", error);
      notification.error("Failed to copy image path");
    }
    setContextMenuVisible(false);
  };

  const handleDeletePreview = () => {
    setContextMenuVisible(false);
    setDeleteConfirmVisible(true);
  };

  const handleDeleteConfirm = async () => {
    if (!mod || !selectedProfileId) return;
    const currentImagePath = allImagePaths[currentImageIndex];
    const totalImages = allImagePaths.length;

    try {
      await modService.deletePreview(selectedProfileId, mod.sha, currentImagePath);
      notification.success("Preview image deleted");

      // Determine the new index after deletion
      // If we're deleting the last image, move back one position
      // Otherwise, stay at the same index (which will show the next image after deletion)
      const newIndex = currentImageIndex >= totalImages - 1
        ? Math.max(0, currentImageIndex - 1)
        : currentImageIndex;

      // Refresh preview to update UI
      await actions.loadPreviewPaths(mod.sha);

      // Set the new index after refresh
      setCurrentImageIndex(newIndex);
    } catch (error) {
      console.error("Error deleting preview:", error);
      notification.error("Failed to delete preview image");
    }
    setDeleteConfirmVisible(false);
  };

  const handleAddFromFile = async () => {
    if (!mod || !selectedProfileId) return;

    try {
      const result = await fileDialogService.openFileDialog({
        title: "Select Preview Image",
        filters: [
          { name: "Image Files", extensions: ["png", "jpg", "jpeg", "gif", "bmp", "webp"] }
        ],
        rememberPathKey: 'mod-preview-import'
      });

      if (result.success && result.filePath) {
        await modService.importPreviewImage(selectedProfileId, mod.sha, result.filePath);
        notification.success("Preview image added successfully");
        // Refresh preview to update UI
        await actions.loadPreviewPaths(mod.sha);
      }
    } catch (error) {
      console.error("Error adding preview from file:", error);
      notification.error("Failed to add preview image");
    }
    setContextMenuVisible(false);
  };

  const handlePasteFromClipboard = async () => {
    if (!mod || !selectedProfileId) return;

    try {
      // Check if clipboard API is available
      if (!navigator.clipboard || !navigator.clipboard.read) {
        notification.warning("Clipboard API not supported in this browser");
        setContextMenuVisible(false);
        return;
      }

      const clipboardItems = await navigator.clipboard.read();
      let imageFound = false;

      for (const item of clipboardItems) {
        // Look for image types
        const imageType = item.types.find(type => type.startsWith('image/'));

        if (imageType) {
          const blob = await item.getType(imageType);

          // Convert blob to file-like object
          const extension = imageType.split('/')[1] || 'png';
          const fileName = `clipboard_${Date.now()}.${extension}`;
          const file = new File([blob], fileName, { type: imageType });

          // Create a temporary path (backend will handle the actual file creation)
          // For now, we'll need to upload the blob - this requires a backend endpoint
          notification.info("Clipboard image paste feature requires backend implementation");
          imageFound = true;
          break;
        }
      }

      if (!imageFound) {
        notification.warning("No image found in clipboard");
      }
    } catch (error) {
      console.error("Error pasting from clipboard:", error);
      notification.error("Failed to paste from clipboard. Make sure you have copied an image.");
    }
    setContextMenuVisible(false);
  };

  // Get current image info
  const currentImagePath = allImagePaths[currentImageIndex] || "";
  const isCurrentImageThumbnail = currentImageIndex === 0; // First image is always the thumbnail
  const hasImages = allImagePaths.length > 0;

  // Context menu items (show only relevant items based on state)
  const contextMenuItems: ContextMenuItem[] = hasImages
    ? [
        {
          key: "add-from-file",
          label: "Add from File...",
          icon: <PlusOutlined />,
          onClick: handleAddFromFile,
        },
        {
          key: "paste-clipboard",
          label: "Paste from Clipboard",
          icon: <SnippetsOutlined />,
          onClick: handlePasteFromClipboard,
        },
        {
          type: "divider",
        },
        {
          key: "set-thumbnail",
          label: "Set as Thumbnail",
          icon: <PictureOutlined />,
          onClick: handleSetAsThumbnail,
          disabled: isCurrentImageThumbnail,
        },
        {
          type: "divider",
        },
        {
          key: "open-explorer",
          label: "Open in File Explorer",
          icon: <FolderOpenOutlined />,
          onClick: handleOpenInExplorer,
        },
        {
          key: "copy-path",
          label: "Copy Image Path",
          icon: <CopyOutlined />,
          onClick: handleCopyImagePath,
        },
        {
          type: "divider",
        },
        {
          key: "delete",
          label: "Delete Preview",
          icon: <DeleteOutlined />,
          danger: true,
          onClick: handleDeletePreview,
        },
      ]
    : [
        {
          key: "add-from-file",
          label: "Add from File...",
          icon: <PlusOutlined />,
          onClick: handleAddFromFile,
        },
        {
          key: "paste-clipboard",
          label: "Paste from Clipboard",
          icon: <SnippetsOutlined />,
          onClick: handlePasteFromClipboard,
        },
        {
          type: "divider",
        },
        {
          key: "open-preview-folder",
          label: "Open Previews Folder",
          icon: <FolderOpenOutlined />,
          onClick: async () => {
            if (!mod) return;
            try {
              // Get preview paths to determine folder location
              const previewPaths = await modService.getPreviewPaths(selectedProfileId!, mod.sha);
              if (previewPaths.length > 0) {
                // Open the folder containing the first preview
                const folderPath = previewPaths[0].substring(0, previewPaths[0].lastIndexOf("\\"));
                await fileDialogService.openDirectory(folderPath);
              } else {
                notification.info("No preview folder exists yet for this mod");
              }
            } catch (error) {
              console.error("Error opening preview folder:", error);
              notification.error("Failed to open preview folder");
            }
            setContextMenuVisible(false);
          },
        },
      ];

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
              <Tag
                icon={<CheckCircleOutlined style={{ fontSize: '16px' }} />}
                color="success"
                style={{ fontSize: '14px', padding: '4px 12px', fontWeight: 500 }}
              >
                Loaded
              </Tag>
            ) : (
              <Tag
                icon={<CloseCircleOutlined style={{ fontSize: '16px' }} />}
                color="default"
                style={{ fontSize: '14px', padding: '4px 12px' }}
              >
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
              onContextMenu={handleImageContextMenu}
            >
              <img
                key={`${allImagePaths[currentImageIndex]}-${cacheTimestamp}`}
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
          <div
            className="mod-preview-no-image"
            onContextMenu={handleImageContextMenu}
          >
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

      {/* Image Context Menu */}
      <ContextMenu
        items={contextMenuItems}
        visible={contextMenuVisible}
        position={contextMenuPosition}
        onClose={() => setContextMenuVisible(false)}
      />

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        visible={deleteConfirmVisible}
        title="Delete Preview Image"
        content="Are you sure you want to delete this preview image? This action cannot be undone."
        okText="Delete"
        cancelText="Cancel"
        okType="danger"
        icon={<ExclamationCircleOutlined style={{ color: '#ff4d4f', fontSize: '22px' }} />}
        onOk={handleDeleteConfirm}
        onCancel={() => setDeleteConfirmVisible(false)}
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
