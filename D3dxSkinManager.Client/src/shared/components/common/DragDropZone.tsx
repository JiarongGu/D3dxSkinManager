import { notification } from '../../../shared/utils/notification';
import React, { useState, useCallback, DragEvent } from 'react';
import { InboxOutlined } from '@ant-design/icons';
import { FileTypeRouter } from '../../../shared/utils/fileTypeRouter';
import { useTranslation } from 'react-i18next';
import './DragDropZone.css';

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
  const { t } = useTranslation();
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
    // Ignore if this is internal drag-and-drop (mods or tree nodes)
    if (e.dataTransfer.types.includes('application/mod-sha') ||
        e.dataTransfer.types.includes('application/tree-node-id')) {
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
    // Ignore if this is internal drag-and-drop (mods or tree nodes)
    if (e.dataTransfer.types.includes('application/mod-sha') ||
        e.dataTransfer.types.includes('application/tree-node-id')) {
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
    // Ignore if this is internal drag-and-drop (mods or tree nodes)
    if (e.dataTransfer.types.includes('application/mod-sha') ||
        e.dataTransfer.types.includes('application/tree-node-id')) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();
  }, []);

  const handleDrop = useCallback((e: DragEvent<HTMLDivElement>) => {
    // Ignore if this is internal drag-and-drop (mods or tree nodes)
    if (e.dataTransfer.types.includes('application/mod-sha') ||
        e.dataTransfer.types.includes('application/tree-node-id')) {
      return;
    }

    e.preventDefault();
    e.stopPropagation();

    if (disabled) return;

    setIsDragging(false);
    setDragCounter(0);

    const { files, items } = e.dataTransfer;

    if (!files || files.length === 0) {
      notification.warning(t('dragDrop.noFiles'));
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
      notification.warning(
        t('dragDrop.filesRejected', { count: rejectedFiles.length, types: accept?.join(', ') })
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
            messages.push(t('dragDrop.previewImages', { count: summary.byType.image }));
          }
          if (summary.byType.archive > 0) {
            messages.push(t('dragDrop.modArchives', { count: summary.byType.archive }));
          }
          if (summary.skipped > 0) {
            messages.push(t('dragDrop.skipped', { count: summary.skipped }));
          }

          if (messages.length > 0) {
            notification.success(t('dragDrop.processed', { summary: messages.join(', ') }));
          }
        }).catch(error => {
          console.error('File routing error:', error);
          notification.error(t('dragDrop.processFailed'));
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
          notification.success(t('dragDrop.previewImagesReady', { count: images.length }));
        }
        if (archives.length > 0) {
          notification.success(t('dragDrop.modArchivesReady', { count: archives.length }));
        }
        if (others.length > 0) {
          notification.info(t('dragDrop.otherFilesDetected', { count: others.length }));
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
      className="drag-drop-zone-container"
    >
      {children}

      {/* Drag overlay */}
      {showOverlay && isDragging && (
        <div className="drag-drop-zone-overlay">
          <div className="drag-drop-zone-overlay-content">
            <InboxOutlined className="drag-drop-zone-overlay-icon" />
            <div>{t('dragDrop.dropFilesHere')}</div>
            {accept && accept.length > 0 && (
              <div className="drag-drop-zone-accepted-types">
                {t('dragDrop.accepted', { types: accept.join(', ') })}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
