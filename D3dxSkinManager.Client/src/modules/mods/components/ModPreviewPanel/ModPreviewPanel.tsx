import { notification } from "../../../../shared/utils/notification";
import React, { useState } from "react";
import { Typography, Button, Empty, Space, Tag, Spin } from "antd";
import { useTranslation } from "react-i18next";
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
import {
  ContextMenu,
  ContextMenuItem,
} from "../../../../shared/components/menu/ContextMenu";
import { ConfirmDialog } from "../../../../shared/components/dialogs";
import { GradingTag } from "../GradingTag";
import { FullScreenPreview } from "./FullScreenPreview";
import { toAppUrl } from "../../../../shared/utils/imageUrlHelper";
import { ModPreviewProvider, useModView } from "./ModPreviewContext";
import { ModInfo } from "../../../../shared/types/mod.types";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { useModsStore } from "../../store/modsStore";
import { modService } from "../../services/modService";
import { fileDialogService } from "../../../../shared/services/systemService";
import "./ModPreviewPanel.css";

const { Text, Paragraph, Title } = Typography;

export const ModPreviewPanelContent: React.FC = () => {
  const { t } = useTranslation();
  const { state, actions } = useModView();
  const { state: profileState } = useProfile();
  const selectedProfileId = profileState.selectedProfile?.id;

  // Subscribe to preview loading state
  const previewLoading = useModsStore((s) => s.previewLoading);

  const [fullScreenVisible, setFullScreenVisible] = useState(false);
  const [fullScreenImageSrc, setFullScreenImageSrc] = useState<string>("");
  const [currentImageIndex, setCurrentImageIndex] = useState(0);
  const [showLeftButton, setShowLeftButton] = useState(false);
  const [showRightButton, setShowRightButton] = useState(false);
  const [contextMenuVisible, setContextMenuVisible] = useState(false);
  const [contextMenuPosition, setContextMenuPosition] = useState({
    x: 0,
    y: 0,
  });
  const [deleteConfirmVisible, setDeleteConfirmVisible] = useState(false);
  const [clipboardHasImage, setClipboardHasImage] = useState(false);

  const mod = state.currentMod;
  const cacheTimestamp = state.cacheTimestamp;

  // Reset image index when mod changes (must be before early return)
  React.useEffect(() => {
    setCurrentImageIndex(0);
  }, [mod?.sha]);

  const handleCopySHA = () => {
    if (!mod) return;
    navigator.clipboard.writeText(mod.sha);
    notification.success(t("mods.notifications.shaCopied"));
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

  // Convert all image paths to URLs for fullscreen preview
  const allImageUrls = allImagePaths.map((path) => toAppUrl(path) || "");

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
  const handleImageContextMenu = async (e: React.MouseEvent) => {
    e.preventDefault();
    setContextMenuPosition({ x: e.clientX, y: e.clientY });

    // Check if clipboard has an image before showing menu
    if (selectedProfileId) {
      try {
        const hasImage =
          await modService.checkClipboardHasImage(selectedProfileId);
        setClipboardHasImage(hasImage);
      } catch (error) {
        console.error("Error checking clipboard:", error);
        setClipboardHasImage(false);
      }
    }

    setContextMenuVisible(true);
  };

  const handleSetAsThumbnail = async () => {
    if (!mod || !selectedProfileId) return;
    const currentImagePath = allImagePaths[currentImageIndex];

    try {
      await modService.setThumbnail(
        selectedProfileId,
        mod.sha,
        currentImagePath,
      );
      notification.success(t("mods.preview.thumbnailUpdated"));
      // Refresh preview to update UI and navigate to first image (newly set thumbnail)
      await actions.loadPreviewPaths(mod.sha);
      setCurrentImageIndex(0);
    } catch (error) {
      console.error("Error setting thumbnail:", error);
      notification.error(t("mods.preview.thumbnailUpdateFailed"));
    }
    setContextMenuVisible(false);
  };

  const handleOpenCacheFolder = async () => {
    if (!mod || !selectedProfileId) return;
    try {
      const paths = await modService.checkFilePaths(selectedProfileId, mod.sha);
      if (paths.cachePath) {
        await fileDialogService.openDirectory(paths.cachePath);
      } else {
        notification.warning(t("mods.preview.cacheFolderNotFound"));
      }
    } catch (error) {
      console.error("Error opening cache folder:", error);
      notification.error(t("mods.preview.openExplorerFailed"));
    }
    setContextMenuVisible(false);
  };

  const handleOpenPreviewFolder = async () => {
    if (!mod || !selectedProfileId) return;
    try {
      const paths = await modService.checkFilePaths(selectedProfileId, mod.sha);
      if (paths.thumbnailPath) {
        await fileDialogService.openDirectory(paths.thumbnailPath);
      } else {
        notification.warning(t("mods.preview.previewFolderNotFound"));
      }
    } catch (error) {
      console.error("Error opening preview folder:", error);
      notification.error(t("mods.preview.openExplorerFailed"));
    }
    setContextMenuVisible(false);
  };

  const handleCopyImagePath = async () => {
    if (!mod) return;
    const currentImagePath = allImagePaths[currentImageIndex];

    try {
      // Convert relative path to absolute for clipboard
      const absolutePath =
        await fileDialogService.getAbsolutePath(currentImagePath);
      await navigator.clipboard.writeText(absolutePath);
      notification.success(t("mods.preview.pathCopied"));
    } catch (error) {
      console.error("Error copying image path:", error);
      notification.error(t("mods.preview.pathCopyFailed"));
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
      await modService.deletePreview(
        selectedProfileId,
        mod.sha,
        currentImagePath,
      );
      notification.success(t("mods.preview.imageDeleted"));

      // Determine the new index after deletion
      // If we're deleting the last image, move back one position
      // Otherwise, stay at the same index (which will show the next image after deletion)
      const newIndex =
        currentImageIndex >= totalImages - 1
          ? Math.max(0, currentImageIndex - 1)
          : currentImageIndex;

      // Refresh preview to update UI
      await actions.loadPreviewPaths(mod.sha);

      // Set the new index after refresh
      setCurrentImageIndex(newIndex);
    } catch (error) {
      console.error("Error deleting preview:", error);
      notification.error(t("mods.preview.imageDeleteFailed"));
    }
    setDeleteConfirmVisible(false);
  };

  const handleAddFromFile = async () => {
    if (!mod || !selectedProfileId) return;

    try {
      const result = await fileDialogService.openFileDialog({
        title: t("mods.preview.selectImage"),
        filters: [
          {
            name: t("mods.preview.imageFiles"),
            extensions: ["png", "jpg", "jpeg", "gif", "bmp", "webp"],
          },
        ],
        rememberPathKey: "mod-preview-import",
      });

      if (result.success && result.filePath) {
        await modService.importPreviewImage(
          selectedProfileId,
          mod.sha,
          result.filePath,
        );
        notification.success(t("mods.preview.imageAdded"));
        // Refresh preview to update UI
        await actions.loadPreviewPaths(mod.sha);
      }
    } catch (error) {
      console.error("Error adding preview from file:", error);
      notification.error(t("mods.preview.imageAddFailed"));
    }
    setContextMenuVisible(false);
  };

  const handlePasteFromClipboard = async () => {
    if (!mod || !selectedProfileId) return;

    try {
      // Let backend handle clipboard access (no browser permission issues)
      await modService.importPreviewFromClipboard(selectedProfileId, mod.sha);
      notification.success(t("mods.preview.imageAdded"));
      // Refresh preview to update UI
      await actions.loadPreviewPaths(mod.sha);
    } catch (error) {
      console.error("Error pasting from clipboard:", error);
      notification.error(t("mods.preview.clipboardPasteFailed"));
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
          label: t("mods.preview.addFromFile"),
          icon: <PlusOutlined />,
          onClick: handleAddFromFile,
        },
        {
          key: "paste-clipboard",
          label: t("mods.preview.pasteFromClipboard"),
          icon: <SnippetsOutlined />,
          onClick: handlePasteFromClipboard,
          disabled: !clipboardHasImage,
        },
        {
          type: "divider",
        },
        {
          key: "set-thumbnail",
          label: t("mods.preview.setAsThumbnail"),
          icon: <PictureOutlined />,
          onClick: handleSetAsThumbnail,
          disabled: isCurrentImageThumbnail,
        },
        {
          type: "divider",
        },
        {
          key: "open-cache-folder",
          label: t("mods.preview.openCacheFolder"),
          icon: <FolderOpenOutlined />,
          onClick: handleOpenCacheFolder,
        },
        {
          key: "open-preview-folder",
          label: t("mods.preview.openPreviewFolder"),
          icon: <FolderOpenOutlined />,
          onClick: handleOpenPreviewFolder,
        },
        {
          key: "copy-path",
          label: t("mods.preview.copyImagePath"),
          icon: <CopyOutlined />,
          onClick: handleCopyImagePath,
        },
        {
          type: "divider",
        },
        {
          key: "delete",
          label: t("mods.preview.deletePreview"),
          icon: <DeleteOutlined />,
          danger: true,
          onClick: handleDeletePreview,
        },
      ]
    : [
        {
          key: "add-from-file",
          label: t("mods.preview.addFromFile"),
          icon: <PlusOutlined />,
          onClick: handleAddFromFile,
        },
        {
          key: "paste-clipboard",
          label: t("mods.preview.pasteFromClipboard"),
          icon: <SnippetsOutlined />,
          onClick: handlePasteFromClipboard,
          disabled: !clipboardHasImage,
        },
        {
          type: "divider",
        },
        {
          key: "open-preview-folder",
          label: t("mods.preview.openPreviewsFolder"),
          icon: <FolderOpenOutlined />,
          onClick: async () => {
            if (!mod) return;
            try {
              // Get preview paths to determine folder location
              const previewPaths = await modService.getPreviewPaths(
                selectedProfileId!,
                mod.sha,
              );
              if (previewPaths.length > 0) {
                // Open the folder containing the first preview
                const folderPath = previewPaths[0].substring(
                  0,
                  previewPaths[0].lastIndexOf("\\"),
                );
                await fileDialogService.openDirectory(folderPath);
              } else {
                notification.info(t("mods.preview.noPreviewFolder"));
              }
            } catch (error) {
              console.error("Error opening preview folder:", error);
              notification.error(t("mods.preview.openFolderFailed"));
            }
            setContextMenuVisible(false);
          },
        },
      ];

  if (!mod) {
    return (
      <div className="mod-preview-empty">
        <Empty
          description={t("mods.preview.selectMod")}
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </div>
    );
  }

  const showAuthor = mod.author && mod.author.trim() !== "";
  const showTags = mod.tags && mod.tags.length > 0;

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
                {mod.categoryName || mod.category}
              </Text>
            )}
          </div>
          <Space size="small">
            {mod.isLoaded ? (
              <Tag
                icon={<CheckCircleOutlined className="mod-preview-tag-icon" />}
                color="success"
                className="mod-preview-tag"
              >
                {t("mods.preview.loaded")}
              </Tag>
            ) : (
              <Tag
                icon={<CloseCircleOutlined className="mod-preview-tag-icon" />}
                color="default"
                className="mod-preview-tag-default"
              >
                {t("mods.preview.notLoaded")}
              </Tag>
            )}
            <GradingTag grading={mod.grading} />
          </Space>
        </div>
      </div>

      {/* Image Preview Section */}
      <Spin
        spinning={previewLoading}
        description={t("mods.preview.loadingImages")}
        classNames={{ root: "mod-preview-spin-wrapper" }}
      >
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
                  alt={t("mods.preview.imageAlt", {
                    name: mod.name,
                    index: currentImageIndex + 1,
                  })}
                  src={toAppUrl(allImagePaths[currentImageIndex]) || undefined}
                  onClick={() =>
                    handleImageClick(
                      toAppUrl(allImagePaths[currentImageIndex]) || "",
                    )
                  }
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
          ) : (
            <div
              className="mod-preview-no-image"
              onContextMenu={handleImageContextMenu}
            >
              <Empty
                description={t("mods.preview.noPreview")}
                image={Empty.PRESENTED_IMAGE_SIMPLE}
              />
            </div>
          )}
        </div>
      </Spin>

      {/* Info Section */}
      <div className="mod-preview-info">
        {(showAuthor || showTags) && (
          <div className="mod-preview-info-item">
            {/* Author */}
            {showAuthor && (
              <>
                <Text type="secondary" className="mod-preview-info-label">
                  <UserOutlined className="mod-preview-info-icon" />
                  {t("mods.details.author")}
                </Text>
                <Text className="mod-preview-info-value">{mod.author}</Text>
              </>
            )}
            {/* Tags */}
            {showTags && (
              <>
                <Text type="secondary" className="mod-preview-info-label">
                  <TagsOutlined className="mod-preview-info-icon" />
                  {t("mods.details.tags")}
                </Text>
                <Space size={[4, 4]} wrap>
                  {mod.tags.map((tag) => (
                    <Tag key={tag} className="mod-preview-tag">
                      {tag}
                    </Tag>
                  ))}
                </Space>
              </>
            )}
          </div>
        )}

        {/* Description */}
        {mod.description && (
          <div className="mod-preview-info-item">
            <Text type="secondary" className="mod-preview-info-label">
              {t("mods.details.description")}
            </Text>
            <Paragraph
              className="mod-preview-description"
              ellipsis={{
                rows: 3,
                expandable: true,
                symbol: t("common.showMore"),
              }}
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
            title={t("mods.preview.clickCopySHA")}
          >
            {mod.sha}
          </Text>
          <Button
            type="text"
            size="small"
            icon={<CopyOutlined />}
            onClick={handleCopySHA}
            title={t("mods.preview.copySHATooltip")}
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
        allImages={allImageUrls}
        initialIndex={currentImageIndex}
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
        title={t("mods.preview.deleteImageTitle")}
        content={t("mods.preview.deleteImageConfirm")}
        okText={t("common.delete")}
        cancelText={t("common.cancel")}
        okType="danger"
        icon={
          <ExclamationCircleOutlined className="mod-preview-confirm-icon" />
        }
        onOk={handleDeleteConfirm}
        onCancel={() => setDeleteConfirmVisible(false)}
      />
    </div>
  );
};

/**
 * ModPreviewPanel wrapper
 *
 * NEW ARCHITECTURE:
 * - Subscribes to selectedMod from store
 * - No props needed!
 */
export const ModPreviewPanel: React.FC = () => {
  // Subscribe to selectedMod
  const mod = useModsStore((s) => s.selectedMod);

  return (
    <ModPreviewProvider mod={mod}>
      <ModPreviewPanelContent />
    </ModPreviewProvider>
  );
};
