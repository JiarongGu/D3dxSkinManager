import React, { useState, useCallback, DragEvent } from 'react';
import { message } from 'antd';
import { InboxOutlined } from '@ant-design/icons';
import { FileTypeRouter } from '../../../shared/utils/fileTypeRouter';

export interface DragDropZoneProps {
  onFilesDrop: (files: File[]) => void;
  onFolderDrop?: (path: string) => void;
  accept?: string[]; // File extensions to accept, e.g., ['.zip', '.rar', '.png', '.jpg']
  children: React.ReactNode;
  disabled?: boolean;
  showOverlay?: boolean;
  router?: FileTypeRouter; // Optional file type router for advanced routing
  enableRouting?: boolean; // Enable/disable routing
}

/**
 * Drag and drop zone component for files and folders
 * Supports visual feedback and file type filtering
 */
export const DragDropZone: React.FC<DragDropZoneProps> = ({
  onFilesDrop,
  onFolderDrop,
  accept,
  children,
  disabled = false,
  showOverlay = true,
  router,
  enableRouting = false,
}) => {
  const [isDragging, setIsDragging] = useState(false);
  const [dragCounter, setDragCounter] = useState(0);

  // Check if file type is accepted
  const isFileAccepted = (fileName: string): boolean => {
    if (!accept || accept.length === 0) return true;

    const fileExt = '.' + fileName.split('.').pop()?.toLowerCase();
    return accept.some(ext => ext.toLowerCase() === fileExt);
  };

  // Get file extension
  const getFileExtension = (fileName: string): string => {
    return fileName.split('.').pop()?.toLowerCase() || '';
  };

  // Determine file type category
  const getFileCategory = (fileName: string): 'image' | 'archive' | 'folder' | 'unknown' => {
    const ext = getFileExtension(fileName);

    if (['png', 'jpg', 'jpeg', 'gif', 'bmp', 'webp'].includes(ext)) {
      return 'image';
    }

    if (['zip', 'rar', '7z', 'tar', 'gz'].includes(ext)) {
      return 'archive';
    }

    return 'unknown';
  };

  const handleDragEnter = useCallback((e: DragEvent<HTMLDivElement>) => {
    // Ignore if this is a mod being dragged (internal drag-and-drop)
    if (e.dataTransfer.types.includes('application/mod-sha')) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();

    if (disabled) return;

    setDragCounter(prev => prev + 1);
    if (e.dataTransfer.items && e.dataTransfer.items.length > 0) {
      setIsDragging(true);
    }
  }, [disabled]);

  const handleDragLeave = useCallback((e: DragEvent<HTMLDivElement>) => {
    // Ignore if this is a mod being dragged (internal drag-and-drop)
    if (e.dataTransfer.types.includes('application/mod-sha')) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();

    if (disabled) return;

    setDragCounter(prev => {
      const newCount = prev - 1;
      if (newCount === 0) {
        setIsDragging(false);
      }
      return newCount;
    });
  }, [disabled]);

  const handleDragOver = useCallback((e: DragEvent<HTMLDivElement>) => {
    // Ignore if this is a mod being dragged (internal drag-and-drop)
    if (e.dataTransfer.types.includes('application/mod-sha')) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();
  }, []);

  const handleDrop = useCallback((e: DragEvent<HTMLDivElement>) => {
    // Ignore if this is a mod being dragged (internal drag-and-drop)
    if (e.dataTransfer.types.includes('application/mod-sha')) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();

    if (disabled) return;

    setIsDragging(false);
    setDragCounter(0);

    const { files, items } = e.dataTransfer;

    if (!files || files.length === 0) {
      message.warning('No files detected');
      return;
    }

    // Convert FileList to Array
    const fileArray = Array.from(files);

    // Filter accepted files
    const acceptedFiles: File[] = [];
    const rejectedFiles: string[] = [];

    fileArray.forEach(file => {
      if (isFileAccepted(file.name)) {
        acceptedFiles.push(file);
      } else {
        rejectedFiles.push(file.name);
      }
    });

    // Show rejection message if any
    if (rejectedFiles.length > 0) {
      message.warning(
        `${rejectedFiles.length} file(s) rejected. Accepted types: ${accept?.join(', ')}`
      );
    }

    // Process accepted files
    if (acceptedFiles.length > 0) {
      // Use router if enabled, otherwise use default handler
      if (enableRouting && router) {
        // Route files using the router
        router.routeFiles(acceptedFiles).then(summary => {
          // Show summary message
          const messages: string[] = [];
          if (summary.byType.image > 0) {
            messages.push(`${summary.byType.image} preview image(s)`);
          }
          if (summary.byType.archive > 0) {
            messages.push(`${summary.byType.archive} mod archive(s)`);
          }
          if (summary.skipped > 0) {
            messages.push(`${summary.skipped} skipped`);
          }

          if (messages.length > 0) {
            message.success(`Processed: ${messages.join(', ')}`);
          }
        }).catch(error => {
          console.error('File routing error:', error);
          message.error('Failed to process some files');
        });
      } else {
        // Categorize files
        const images = acceptedFiles.filter(f => getFileCategory(f.name) === 'image');
        const archives = acceptedFiles.filter(f => getFileCategory(f.name) === 'archive');
        const others = acceptedFiles.filter(f => {
          const cat = getFileCategory(f.name);
          return cat !== 'image' && cat !== 'archive';
        });

        // Log categorization
        console.log('Dropped files:', {
          total: acceptedFiles.length,
          images: images.length,
          archives: archives.length,
          others: others.length,
        });

        onFilesDrop(acceptedFiles);

        // Show success message
        if (images.length > 0) {
          message.success(`${images.length} preview image(s) ready to add`);
        }
        if (archives.length > 0) {
          message.success(`${archives.length} mod archive(s) ready to add`);
        }
        if (others.length > 0) {
          message.info(`${others.length} other file(s) detected`);
        }
      }
    }
  }, [disabled, accept, onFilesDrop, router, enableRouting]);

  return (
    <div
      onDragEnter={handleDragEnter}
      onDragLeave={handleDragLeave}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
      style={{ position: 'relative', width: '100%', height: '100%' }}
    >
      {children}

      {/* Drag overlay */}
      {showOverlay && isDragging && (
        <div
          style={{
            position: 'absolute',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: 'rgba(24, 144, 255, 0.1)',
            border: '2px dashed #1890ff',
            borderRadius: '4px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            pointerEvents: 'none',
          }}
        >
          <div
            style={{
              textAlign: 'center',
              color: '#1890ff',
              fontSize: '24px',
              fontWeight: 600,
            }}
          >
            <InboxOutlined style={{ fontSize: '48px', marginBottom: '16px' }} />
            <div>Drop files here</div>
            {accept && accept.length > 0 && (
              <div style={{ fontSize: '14px', marginTop: '8px', opacity: 0.8 }}>
                Accepted: {accept.join(', ')}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
