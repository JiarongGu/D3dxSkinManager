import React, { useRef } from 'react';
import { InboxOutlined } from '@ant-design/icons';
import { useDropZone } from '../../hooks/useDropZone';
import './CompactUpload.css';

/**
 * CompactUpload - Compact upload area with click-to-select and OS-level drag-and-drop
 *
 * Features:
 * - Click to trigger file selection dialog
 * - OS-level drag and drop (gets real file paths, not blob URLs)
 * - Theme-aware hover colors
 * - Visual feedback during drag operations via useDropZone CSS classes
 * - Dashed border design
 * - Icon + text layout
 *
 * Usage:
 * <CompactUpload
 *   onSelect={handleFileSelect}
 *   onDrop={handleFileDrop}
 *   title="Click or drag to select image file"
 *   subtitle="PNG, JPG, JPEG, GIF, BMP, WEBP"
 * />
 */

export interface CompactUploadProps {
  /** Callback when file is selected via click (opens file dialog) */
  onSelect: () => void;
  /** Callback when files are dropped - receives real OS file paths */
  onDrop?: (files: string[]) => void;
  /** Main title text */
  title?: string;
  /** Subtitle/hint text */
  subtitle?: string;
  /** Icon to display - defaults to InboxOutlined */
  icon?: React.ReactNode;
  /** Size variant */
  size?: 'small' | 'medium' | 'large';
  /** Additional CSS class names */
  className?: string;
  /** Enable/disable drop zone (default: true when onDrop is provided) */
  enabled?: boolean;
}

export const CompactUpload: React.FC<CompactUploadProps> = ({
  onSelect,
  onDrop,
  title = 'Click or drag to select file',
  subtitle,
  icon,
  size = 'medium',
  className = '',
  enabled = true
}) => {
  const dropZoneRef = useRef<HTMLDivElement>(null);

  // Use OS-level drop zone when onDrop is provided
  useDropZone({
    targetRef: dropZoneRef,
    enabled: enabled && !!onDrop,
    onDrop: (files) => {
      if (onDrop && files.length > 0) {
        onDrop(files);
      }
    },
  });

  const uploadClassName = `compact-upload compact-upload-${size} ${className}`.trim();

  return (
    <div
      ref={dropZoneRef}
      className={uploadClassName}
      onClick={onSelect}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onSelect();
        }
      }}
    >
      <div className="compact-upload-icon">
        {icon || <InboxOutlined />}
      </div>
      <div className="compact-upload-title">{title}</div>
      {subtitle && <div className="compact-upload-subtitle">{subtitle}</div>}
    </div>
  );
};

export default CompactUpload;
