/**
 * File Type Router - Routes dropped files to appropriate handlers based on file type
 * Based on Python implementation: additional/hook_dropfiles.py
 */

export type FileType = 'image' | 'archive' | 'folder' | 'unknown';

export type FileHandler = (files: File[]) => void | Promise<void>;

export interface FileRouteRule {
  extensions: string[];
  fileType: FileType;
  handler: FileHandler;
  priority?: number; // Higher priority rules checked first
}

export interface FileRouterConfig {
  rules: FileRouteRule[];
  defaultHandler?: FileHandler;
  enableLogging?: boolean;
}

export class FileTypeRouter {
  private config: FileRouterConfig;

  constructor(config: FileRouterConfig) {
    this.config = {
      enableLogging: false,
      ...config
    };

    // Sort rules by priority (descending)
    this.config.rules.sort((a, b) => (b.priority || 0) - (a.priority || 0));
  }

  /**
   * Get file extension from filename
   */
  private getFileExtension(fileName: string): string {
    return fileName.split('.').pop()?.toLowerCase() || '';
  }

  /**
   * Determine file type based on extension
   */
  getFileType(fileName: string): FileType {
    const ext = this.getFileExtension(fileName);

    // Check rules in priority order
    for (const rule of this.config.rules) {
      if (rule.extensions.includes(ext)) {
        return rule.fileType;
      }
    }

    return 'unknown';
  }

  /**
   * Get handler for a file based on its type
   */
  private getHandlerForFile(file: File): FileHandler | undefined {
    const ext = this.getFileExtension(file.name);

    // Find matching rule
    for (const rule of this.config.rules) {
      if (rule.extensions.includes(ext)) {
        return rule.handler;
      }
    }

    return this.config.defaultHandler;
  }

  /**
   * Group files by their handlers
   */
  private groupFilesByHandler(files: File[]): Map<FileHandler, File[]> {
    const groups = new Map<FileHandler, File[]>();

    files.forEach(file => {
      const handler = this.getHandlerForFile(file);
      if (handler) {
        if (!groups.has(handler)) {
          groups.set(handler, []);
        }
        groups.get(handler)!.push(file);
      }
    });

    return groups;
  }

  /**
   * Route files to appropriate handlers
   * Returns a summary of how files were routed
   */
  async routeFiles(files: File[]): Promise<{
    processed: number;
    skipped: number;
    byType: Record<FileType, number>;
  }> {
    const summary = {
      processed: 0,
      skipped: 0,
      byType: {
        image: 0,
        archive: 0,
        folder: 0,
        unknown: 0
      } as Record<FileType, number>
    };

    // Group files by handler
    const handlerGroups = this.groupFilesByHandler(files);

    if (this.config.enableLogging) {
      console.log(`[FileTypeRouter] Routing ${files.length} files to ${handlerGroups.size} handlers`);
    }

    // Execute handlers
    const handlerPromises: Promise<void>[] = [];

    // Convert entries to array for iteration compatibility
    const handlerEntries = Array.from(handlerGroups.entries());

    for (const [handler, handlerFiles] of handlerEntries) {
      // Count by type
      handlerFiles.forEach(file => {
        const fileType = this.getFileType(file.name);
        summary.byType[fileType]++;
      });

      // Execute handler
      const promise = Promise.resolve(handler(handlerFiles));
      handlerPromises.push(promise);
      summary.processed += handlerFiles.length;
    }

    // Count skipped files (no handler)
    summary.skipped = files.length - summary.processed;

    // Wait for all handlers to complete
    await Promise.all(handlerPromises);

    if (this.config.enableLogging) {
      console.log('[FileTypeRouter] Routing complete:', summary);
    }

    return summary;
  }

  /**
   * Check if a file is accepted by any rule
   */
  isFileAccepted(fileName: string): boolean {
    const ext = this.getFileExtension(fileName);
    return this.config.rules.some(rule => rule.extensions.includes(ext));
  }

  /**
   * Get all accepted extensions
   */
  getAcceptedExtensions(): string[] {
    const extensions = new Set<string>();
    this.config.rules.forEach(rule => {
      rule.extensions.forEach(ext => extensions.add(ext));
    });
    return Array.from(extensions);
  }
}

/**
 * Create default file router for mod manager
 */
export function createDefaultFileRouter(handlers: {
  onImageDrop?: FileHandler;
  onArchiveDrop?: FileHandler;
  onFolderDrop?: FileHandler;
  onUnknownDrop?: FileHandler;
}): FileTypeRouter {
  const rules: FileRouteRule[] = [];

  // Image files (preview images) - all web-renderable formats
  if (handlers.onImageDrop) {
    rules.push({
      extensions: ['png', 'jpg', 'jpeg', 'gif', 'bmp', 'webp', 'svg', 'ico', 'avif', 'jxl', 'apng', 'tif', 'tiff'],
      fileType: 'image',
      handler: handlers.onImageDrop,
      priority: 10
    });
  }

  // Archive files (mod imports)
  if (handlers.onArchiveDrop) {
    rules.push({
      extensions: ['zip', 'rar', '7z', 'tar', 'gz', 'bz2'],
      fileType: 'archive',
      handler: handlers.onArchiveDrop,
      priority: 5
    });
  }

  // Unknown files
  if (handlers.onUnknownDrop) {
    rules.push({
      extensions: [],
      fileType: 'unknown',
      handler: handlers.onUnknownDrop,
      priority: 0
    });
  }

  return new FileTypeRouter({
    rules,
    enableLogging: true
  });
}

/**
 * File type constants
 */
export const FILE_TYPES = {
  // All web-renderable image formats
  IMAGE: [
    'png', 'jpg', 'jpeg', 'gif', 'bmp', 'webp',  // Common raster formats
    'svg',                                         // Vector format
    'ico',                                         // Icon format
    'avif', 'jxl',                                 // Modern formats
    'apng',                                        // Animated PNG
    'tif', 'tiff'                                  // TIFF format
  ],
  ARCHIVE: ['zip', 'rar', '7z', 'tar', 'gz', 'bz2'],
} as const;
