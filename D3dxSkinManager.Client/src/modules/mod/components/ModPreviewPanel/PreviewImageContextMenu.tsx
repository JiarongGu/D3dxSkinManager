import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import {
  CopyOutlined,
  FolderOutlined,
  PictureOutlined,
  FolderOpenOutlined,
  DeleteOutlined,
  PlusOutlined,
  SnippetsOutlined,
  EyeOutlined,
  EyeInvisibleOutlined,
} from "@ant-design/icons";
import {
  ContextMenu,
  ContextMenuItem,
} from "../../../../shared/components/menu/ContextMenu";
import { ConfirmDialog } from "../../../../shared/components/dialogs";
import { notification } from "../../../../shared/utils/notification";
import { modService, systemService } from "../../../../shared/services/ipc";
import { ModInfo } from "../../../../shared/types/mod.types";

interface PreviewImageContextMenuProps {
  mod: ModInfo;
  profileId: string;
  allImagePaths: string[];
  currentImageIndex: number;
  visible: boolean;
  position: { x: number; y: number };
  onClose: () => void;
  onImageIndexChange: (index: number) => void;
  onUpdateMod: (id: string, data: Partial<ModInfo>) => Promise<void>;
}

export const PreviewImageContextMenu: React.FC<PreviewImageContextMenuProps> = ({
  mod,
  profileId,
  allImagePaths,
  currentImageIndex,
  visible,
  position,
  onClose,
  onImageIndexChange,
  onUpdateMod,
}) => {
  const { t } = useTranslation();
  const [deleteConfirmVisible, setDeleteConfirmVisible] = useState(false);
  const [clipboardHasImage, setClipboardHasImage] = useState(false);

  // Check clipboard when menu becomes visible
  useEffect(() => {
    if (visible && profileId) {
      void (async () => {
        try {
          const hasImage = await modService.checkClipboardHasImage(profileId);
          setClipboardHasImage(hasImage);
        } catch (error: unknown) {
          setClipboardHasImage(false);
        }
      })();
    }
  }, [visible, profileId]);

  const handleSetAsThumbnail = async () => {
    if (!mod || !profileId) return;
    const currentImagePath = allImagePaths[currentImageIndex];

    try {
      await modService.setThumbnail(
        profileId,
        mod.id,
        currentImagePath,
      );
      notification.success(t("mods.preview.thumbnailUpdated"));
      // Navigate to first image (newly set thumbnail)
      // Preview paths will be refreshed automatically by THUMBNAIL_UPDATED event
      onImageIndexChange(0);
    } catch (error: unknown) {
      notification.error(t("mods.preview.thumbnailUpdateFailed"));
    }
    onClose();
  };

  const handleOpenCacheFolder = async () => {
    if (!mod || !profileId) return;
    try {
      if (mod.cachePath) {
        await systemService.openDirectory(mod.cachePath);
      } else {
        notification.warning(t("mods.preview.cacheFolderNotFound"));
      }
    } catch (error: unknown) {
      notification.error(t("mods.preview.openExplorerFailed"));
    }
    onClose();
  };

  const handleOpenPreviewFolder = async () => {
    if (!mod || !profileId) return;
    try {
      if (mod.previewFolderPath) {
        await systemService.openDirectory(mod.previewFolderPath);
      } else {
        notification.warning(t("mods.preview.previewFolderNotFound"));
      }
    } catch (error: unknown) {
      notification.error(t("mods.preview.openExplorerFailed"));
    }
    onClose();
  };

  const handleCopyImagePath = async () => {
    if (!mod) return;
    const currentImagePath = allImagePaths[currentImageIndex];

    try {
      // Convert relative path to absolute for clipboard
      const absolutePath =
        await systemService.getAbsolutePath(currentImagePath);
      await navigator.clipboard.writeText(absolutePath);
      notification.success(t("mods.preview.pathCopied"));
    } catch (error: unknown) {
      notification.error(t("mods.preview.pathCopyFailed"));
    }
    onClose();
  };

  const handleCopyImageToClipboard = async () => {
    if (!mod || !profileId) return;
    const currentImagePath = allImagePaths[currentImageIndex];

    try {
      await modService.copyPreviewToClipboard(profileId, currentImagePath);
      notification.success(t("mods.preview.imageCopied"));
    } catch (error: unknown) {
      notification.error(t("mods.preview.imageCopyFailed"));
    }
    onClose();
  };

  const handleDeletePreview = () => {
    onClose();
    setDeleteConfirmVisible(true);
  };

  const handleDeleteConfirm = async () => {
    if (!mod || !profileId) return;
    const currentImagePath = allImagePaths[currentImageIndex];
    const totalImages = allImagePaths.length;

    try {
      await modService.deletePreview(
        profileId,
        mod.id,
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

      // Set the new index
      // Preview paths will be refreshed automatically by PREVIEW_DELETED event
      onImageIndexChange(newIndex);
    } catch (error: unknown) {
      notification.error(t("mods.preview.imageDeleteFailed"));
    }
    setDeleteConfirmVisible(false);
  };

  const handleAddFromFile = async () => {
    if (!mod || !profileId) return;

    try {
      const result = await systemService.openFileDialog({
        title: t("mods.preview.selectImage"),
        filters: [
          {
            name: t("common.imageFiles"),
            extensions: ["png", "jpg", "jpeg", "gif", "bmp", "webp"],
          },
        ],
        rememberPathKey: "mod-preview-import",
      });

      if (result.success && result.filePath) {
        await modService.importPreviewImage(
          profileId,
          mod.id,
          result.filePath,
        );
        notification.success(t("mods.preview.imageAdded"));
        // Preview paths will be refreshed automatically by PREVIEW_IMPORTED event
      }
    } catch (error: unknown) {
      notification.error(t("mods.preview.imageAddFailed"));
    }
    onClose();
  };

  const handlePasteFromClipboard = async () => {
    if (!mod || !profileId) return;

    try {
      // Let backend handle clipboard access (no browser permission issues)
      await modService.importPreviewFromClipboard(profileId, mod.id);
      notification.success(t("mods.preview.imageAdded"));
      // Preview paths will be refreshed automatically by PREVIEW_IMPORTED event
    } catch (error: unknown) {
      notification.error(t("mods.preview.clipboardPasteFailed"));
    }
    onClose();
  };

  const handleTogglePreview = async () => {
    if (!mod) return;

    try {
      const newValue = !mod.disablePreview;
      await onUpdateMod(mod.id, { disablePreview: newValue });
      notification.success(
        newValue
          ? t("mods.preview.previewDisabled")
          : t("mods.preview.previewEnabled")
      );
    } catch (error: unknown) {
      notification.error(t("mods.preview.togglePreviewFailed"));
    }
    onClose();
  };


  // Get current image info
  const currentImagePath = allImagePaths[currentImageIndex] || "";
  const isCurrentImageThumbnail = currentImageIndex === 0; // First image is always the thumbnail
  const hasImages = allImagePaths.length > 0;

  // Context menu items (show only relevant items based on state)
  const contextMenuItems: ContextMenuItem[] = hasImages
    ? [
        // Group 1: Common Actions (Clipboard Operations)
        {
          key: "copy-image",
          label: t("mods.preview.copyToClipboard"),
          icon: <CopyOutlined />,
          onClick: handleCopyImageToClipboard,
        },
        {
          key: "paste-clipboard",
          label: t("mods.preview.pasteFromClipboard"),
          icon: <SnippetsOutlined />,
          onClick: handlePasteFromClipboard,
          disabled: !clipboardHasImage,
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
        // Group 2: File/Folder Navigation
        {
          key: "open-mod-folder",
          label: t("mods.preview.openModFolder"),
          icon: <FolderOpenOutlined />,
          onClick: async () => {
            if (!mod || !profileId) return;
            try {
              if (mod.archiveFolderPath && mod.id && mod.isAvailable) {
                // Construct the archive file path (archives are stored without extension)
                const archiveFilePath = `${mod.archiveFolderPath}\\${mod.id}`;
                await systemService.openFileInExplorer(archiveFilePath);
              } else {
                notification.warning(t("mods.preview.modFolderNotFound"));
              }
            } catch (error: unknown) {
              notification.error(t("mods.preview.openExplorerFailed"));
            }
            onClose();
          },
          disabled: !mod?.archiveFolderPath || !mod?.isAvailable,
        },
        {
          key: "open-cache-folder",
          label: t("mods.preview.openCacheFolder"),
          icon: <FolderOpenOutlined />,
          onClick: handleOpenCacheFolder,
          disabled: !mod?.hasCache,
        },
        {
          key: "open-preview-folder",
          label: t("mods.preview.openPreviewFolder"),
          icon: <FolderOpenOutlined />,
          onClick: handleOpenPreviewFolder,
          disabled: !mod?.hasPreviewFolder,
        },
        {
          type: "divider",
        },
        // Group 3: Less Common / Destructive Actions
        {
          key: "add-from-file",
          label: t("mods.preview.addFromFile"),
          icon: <PlusOutlined />,
          onClick: handleAddFromFile,
        },
        {
          key: "copy-path",
          label: t("mods.preview.copyImagePath"),
          icon: <CopyOutlined />,
          onClick: handleCopyImagePath,
        },
        {
          key: "toggle-preview",
          label: mod?.disablePreview
            ? t("mods.preview.enablePreview")
            : t("mods.preview.disablePreview"),
          icon: mod?.disablePreview ? <EyeOutlined /> : <EyeInvisibleOutlined />,
          onClick: handleTogglePreview,
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
          key: "open-mod-folder",
          label: t("mods.preview.openModFolder"),
          icon: <FolderOpenOutlined />,
          onClick: async () => {
            if (!mod || !profileId) return;
            try {
              if (mod.archiveFolderPath && mod.id && mod.isAvailable) {
                // Construct the archive file path (archives are stored without extension)
                const archiveFilePath = `${mod.archiveFolderPath}\\${mod.id}`;
                await systemService.openFileInExplorer(archiveFilePath);
              } else {
                notification.warning(t("mods.preview.modFolderNotFound"));
              }
            } catch (error: unknown) {
              notification.error(t("mods.preview.openExplorerFailed"));
            }
            onClose();
          },
          disabled: !mod?.archiveFolderPath || !mod?.isAvailable,
        },
        {
          key: "open-cache-folder",
          label: t("mods.preview.openCacheFolder"),
          icon: <FolderOpenOutlined />,
          onClick: handleOpenCacheFolder,
          disabled: !mod?.hasCache,
        },
        {
          key: "open-preview-folder",
          label: t("mods.preview.openPreviewsFolder"),
          icon: <FolderOpenOutlined />,
          disabled: !mod?.hasPreviewFolder,
          onClick: handleOpenPreviewFolder,
        },
        {
          type: "divider",
        },
        {
          key: "toggle-preview",
          label: mod?.disablePreview
            ? t("mods.preview.enablePreview")
            : t("mods.preview.disablePreview"),
          icon: mod?.disablePreview ? <EyeOutlined /> : <EyeInvisibleOutlined />,
          onClick: handleTogglePreview,
        },
      ];

  return (
    <>
      <ContextMenu
        items={contextMenuItems}
        visible={visible}
        position={position}
        onClose={onClose}
      />

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        visible={deleteConfirmVisible}
        title={t("mods.preview.deleteImageTitle")}
        content={t("mods.preview.deleteImageConfirm")}
        okText={t("common.delete")}
        cancelText={t("common.cancel")}
        okType="danger"
        icon={<DeleteOutlined />}
        onOk={handleDeleteConfirm}
        onCancel={() => setDeleteConfirmVisible(false)}
      />
    </>
  );
};
