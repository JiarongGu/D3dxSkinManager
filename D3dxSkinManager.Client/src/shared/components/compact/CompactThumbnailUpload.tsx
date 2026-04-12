import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { PictureOutlined, DeleteOutlined, FileImageOutlined } from '@ant-design/icons';
import './CompactThumbnailUpload.css';

/**
 * CompactThumbnailUpload - Ant Design picture-card style thumbnail upload
 *
 * Features:
 * - Two-column layout: thumbnail preview + change button
 * - Matches Ant Design Upload listType="picture-card" appearance
 * - Remove overlay on thumbnail hover
 * - Error state when image fails to load
 * - Theme-aware styling
 *
 * Usage:
 * <CompactThumbnailUpload
 *   thumbnailUrl={profileThumbnail}
 *   onSelect={handleBrowseThumbnail}
 *   onRemove={handleRemoveThumbnail}
 *   buttonText="Change Thumbnail"
 * />
 */

export interface CompactThumbnailUploadProps {
  /** URL of the current thumbnail (optional) */
  thumbnailUrl?: string;
  /** Callback when user clicks to select a new thumbnail */
  onSelect: () => void;
  /** Callback when user clicks to remove the thumbnail */
  onRemove?: () => void;
  /** Text for the upload button (default: "Change Thumbnail") */
  buttonText?: string;
  /** Alt text for the image */
  alt?: string;
  /** Additional CSS class names */
  className?: string;
}

export const CompactThumbnailUpload: React.FC<CompactThumbnailUploadProps> = ({
  thumbnailUrl,
  onSelect,
  onRemove,
  buttonText,
  alt,
  className = '',
}) => {
  const { t } = useTranslation();
  const resolvedButtonText = buttonText ?? t('common.changeThumbnail');
  const resolvedAlt = alt ?? t('common.thumbnail');
  const [imageError, setImageError] = useState(false);
  const containerClassName = `compact-thumbnail-upload ${className}`.trim();

  // Reset error state when thumbnailUrl changes
  React.useEffect(() => {
    setImageError(false);
  }, [thumbnailUrl]);

  return (
    <div className={containerClassName}>
      {/* Thumbnail preview card (left side) */}
      {thumbnailUrl && (
        <div className="compact-thumbnail-card">
          {!imageError ? (
            <img
              src={thumbnailUrl}
              alt={resolvedAlt}
              className="compact-thumbnail-image"
              onError={() => {
                setImageError(true);
              }}
            />
          ) : (
            <div className="compact-thumbnail-error">
              <FileImageOutlined className="compact-thumbnail-error-icon" />
              <div className="compact-thumbnail-error-text">{t('common.failedToLoad')}</div>
            </div>
          )}
          {/* Remove overlay */}
          {onRemove && (
            <div className="compact-thumbnail-overlay" onClick={onRemove}>
              <DeleteOutlined className="compact-thumbnail-overlay-icon" />
              <div className="compact-thumbnail-overlay-text">{t('common.remove')}</div>
            </div>
          )}
        </div>
      )}

      {/* Upload button card (right side) */}
      <div className="compact-thumbnail-upload-card" onClick={onSelect}>
        <PictureOutlined className="compact-thumbnail-upload-icon" />
        <div className="compact-thumbnail-upload-text">{resolvedButtonText}</div>
      </div>
    </div>
  );
};
