import { notification } from "../../../../shared/utils/notification";
import React, { useState } from "react";
import { Typography, Button, Empty, Spin } from "antd";
import { useTranslation } from "react-i18next";
import classNames from "classnames";
import { CopyOutlined, KeyOutlined, FolderOutlined } from "@ant-design/icons";
import { CompactTextButton } from "../../../../shared/components/compact";
import { FullScreenPreview } from "./FullScreenPreview";
import { KeybindingPreview } from "./KeybindingPreview";
import { ModInfoSection } from "./ModInfoSection";
import { PreviewImageCarousel } from "./PreviewImageCarousel";
import { PreviewImageContextMenu } from "./PreviewImageContextMenu";
import { toAppUrl } from "../../../../shared/utils/imageUrlHelper";
import * as modOps from '../../operations/modOperations';
import { useProfile } from "../../../../shared/context/ProfileContext";
import { useModsStore } from "../../store/modsStore";
import type { ModInfo } from "../../../../shared/types/mod.types";
import "./ModPreviewPanel.css";

const { Text, Title } = Typography;

export const ModPreviewPanel: React.FC = () => {
  const { t } = useTranslation();
  const { state: profileState } = useProfile();
  const selectedProfileId = profileState.selectedProfile?.id;

  // Subscribe to store state
  const mod = useModsStore((s) => s.selectedMod);
  const previewLoading = useModsStore((s) => s.previewLoading);
  const previewPaths = useModsStore((s) => s.previewPaths);
  const cacheTimestamp = useModsStore((s) => s.previewCacheTimestamp);

  const [fullScreenVisible, setFullScreenVisible] = useState(false);
  const [fullScreenImageSrc, setFullScreenImageSrc] = useState<string>("");
  const [currentImageIndex, setCurrentImageIndex] = useState(0);
  const [contextMenuVisible, setContextMenuVisible] = useState(false);
  const [contextMenuPosition, setContextMenuPosition] = useState({
    x: 0,
    y: 0,
  });
  const [showKeybindings, setShowKeybindings] = useState(false);
  const [isKeybindingsClosing, setIsKeybindingsClosing] = useState(false);

  // Load preview paths when mod changes, when isLoaded status changes, or when disablePreview changes
  React.useEffect(() => {
    if (mod?.sha && selectedProfileId) {
      void modOps.loadPreviewPaths(selectedProfileId, mod.sha);
    } else {
      // Clear previews when no mod is selected
      useModsStore.getState().setPreviewPaths([]);
    }
  }, [mod?.sha, mod?.isLoaded, mod?.disablePreview, selectedProfileId]);

  // Reset image index and keybinding visibility when mod changes (must be before early return)
  React.useEffect(() => {
    setCurrentImageIndex(0);
    setShowKeybindings(false);
    setIsKeybindingsClosing(false);
  }, [mod?.sha]);

  // Handle keybinding toggle with animation
  const handleKeybindingToggle = () => {
    if (showKeybindings) {
      // Start closing animation
      setIsKeybindingsClosing(true);
      // Wait for animation to complete before hiding
      setTimeout(() => {
        setShowKeybindings(false);
        setIsKeybindingsClosing(false);
      }, 120); // Match animation duration (0.15s in CSS) minus a bit for smoother UX
    } else {
      setShowKeybindings(true);
    }
  };

  const handleCopySHA = () => {
    if (!mod) return;
    navigator.clipboard.writeText(mod.sha);
    notification.success(t("mods.notifications.shaCopied"));
  };

  const handleImageClick = (imageSrc: string) => {
    setFullScreenImageSrc(imageSrc);
    setFullScreenVisible(true);
  };

  // Wrapper for updateMod that always returns a Promise
  const handleUpdateMod = async (sha: string, data: Partial<ModInfo>): Promise<void> => {
    if (!selectedProfileId) return;
    await modOps.updateMod(selectedProfileId, sha, data);
  };

  // Context menu handlers
  const handleImageContextMenu = async (e: React.MouseEvent) => {
    e.preventDefault();
    setContextMenuPosition({ x: e.clientX, y: e.clientY });
    setContextMenuVisible(true);
  };

  // Determine which images to show (preview paths, with thumbnail first)
  const allImagePaths: string[] = [];
  if (previewPaths && previewPaths.length > 0) {
    allImagePaths.push(...previewPaths);
  }

  // Convert all image paths to URLs for fullscreen preview (with cache busting)
  const allImageUrls = allImagePaths.map((path) => toAppUrl(path, cacheTimestamp) || "");

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

  return (
    <div className="mod-preview-panel">
      {/* Header Section */}
      <div className="mod-preview-header">
        <div className="mod-preview-header-content">
          <div className="mod-preview-header-title">
            <Title level={4} className="mod-preview-title">
              {mod.name}
            </Title>
            <Text type="secondary" className="mod-preview-category">
              <FolderOutlined className="mod-preview-category-icon" />
              {mod.categoryName || t('category.unclassified')}
            </Text>
          </div>
          {mod.hasCache && (
            <CompactTextButton
              size="medium"
              icon={<KeyOutlined />}
              onClick={handleKeybindingToggle}
              className={classNames('mod-preview-keybinding-toggle', {
                active: showKeybindings,
              })}
              title={t('mods.keybindings.toggleTooltip')}
            >
              {t('mods.keybindings.toggle')}
            </CompactTextButton>
          )}
        </div>
      </div>

      {/* Image Preview Section */}
      <Spin
        spinning={previewLoading}
        description={t("mods.preview.loadingImages")}
        classNames={{ root: "mod-preview-spin-wrapper" }}
      >
        <div className="mod-preview-image-section">
          <PreviewImageCarousel
            mod={mod}
            allImagePaths={allImagePaths}
            cacheTimestamp={cacheTimestamp}
            currentImageIndex={currentImageIndex}
            onImageIndexChange={setCurrentImageIndex}
            onImageClick={handleImageClick}
            onContextMenu={handleImageContextMenu}
          />

          {/* Keybinding Preview Overlay */}
          {showKeybindings && (
            <div
              className={classNames('mod-preview-keybinding-overlay', {
                closing: isKeybindingsClosing,
              })}
            >
              <KeybindingPreview modSha={mod.sha} />
            </div>
          )}
        </div>
      </Spin>

      {/* Info Section */}
      <ModInfoSection mod={mod} />

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
      <PreviewImageContextMenu
        mod={mod}
        profileId={selectedProfileId!}
        allImagePaths={allImagePaths}
        currentImageIndex={currentImageIndex}
        visible={contextMenuVisible}
        position={contextMenuPosition}
        onClose={() => setContextMenuVisible(false)}
        onImageIndexChange={setCurrentImageIndex}
        onUpdateMod={handleUpdateMod}
      />
    </div>
  );
};