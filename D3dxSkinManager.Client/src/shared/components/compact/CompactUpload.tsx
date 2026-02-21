import React, { useState } from 'react';
import { InboxOutlined } from '@ant-design/icons';
import './CompactUpload.css';

/**
 * CompactUpload - Compact upload area with click-to-select and drag-and-drop
 *
 * Features:
 * - Click to trigger file selection dialog
 * - Drag and drop files from file system
 * - Theme-aware hover colors
 * - Visual feedback during drag operations
 * - Dashed border design
 * - Icon + text layout
 *
 * Usage:
 * <CompactUpload
 *   onSelect={handleFileSelect}
 *   onDrop={handleFileDrop}
 *   accept="image/*"
 *   title="Click or drag to select image file"
 *   subtitle="PNG, JPG, JPEG, GIF, BMP, WEBP"
 * />
 */

export interface CompactUploadProps {
  /** Callback when file is selected via click (opens file dialog) */
  onSelect: () => void;
  /** Callback when file is dropped - receives the File object and optional path */
  onDrop?: (file: File, filePath?: string) => void;
  /** Accept attribute for file input (e.g., "image/*") */
  accept?: string;
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
}

export const CompactUpload: React.FC<CompactUploadProps> = ({
  onSelect,
  onDrop,
  accept,
  title = 'Click or drag to select file',
  subtitle,
  icon,
  size = 'medium',
  className = ''
}) => {
  const [isDragging, setIsDragging] = useState(false);

  const handleDragEnter = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    if (onDrop && e.dataTransfer.items && e.dataTransfer.items.length > 0) {
      const item = e.dataTransfer.items[0];

      // Try to get file path using webkitGetAsEntry (Chromium API)
      let filePath: string | undefined;

      if ((item as any).webkitGetAsEntry) {
        const entry = (item as any).webkitGetAsEntry();
        if (entry) {
          filePath = entry.fullPath;
          console.log('[CompactUpload] webkitGetAsEntry fullPath:', filePath);
        }
      }

      // Get the File object
      const file = item.getAsFile();

      if (file) {
        // Also check for electron-style file.path
        const electronPath = (file as any).path;

        console.log('[CompactUpload] File dropped:', {
          fileName: file.name,
          webkitFullPath: filePath,
          electronPath: electronPath,
          fileSize: file.size,
          fileType: file.type,
          // Check all possible path properties
          allProperties: Object.keys(file),
          // Try to get the internal file path if available
          webkitRelativePath: (file as any).webkitRelativePath,
          // Log the entire file object
          fileObject: file
        });

        // Pass both file and path (if available)
        onDrop(file, filePath || electronPath);
      }
    } else if (onDrop && e.dataTransfer.files.length > 0) {
      // Fallback to files array
      const file = e.dataTransfer.files[0];
      const electronPath = (file as any).path;

      console.log('[CompactUpload] File dropped (fallback):', {
        fileName: file.name,
        electronPath: electronPath,
        fileSize: file.size,
        fileType: file.type
      });

      onDrop(file, electronPath);
    }
  };

  const uploadClassName = `compact-upload compact-upload-${size} ${isDragging ? 'compact-upload-dragging' : ''} ${className}`.trim();

  return (
    <div
      className={uploadClassName}
      onClick={onSelect}
      onDragEnter={handleDragEnter}
      onDragLeave={handleDragLeave}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
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
