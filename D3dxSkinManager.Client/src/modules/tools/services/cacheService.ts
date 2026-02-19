import { BaseModuleService } from '../../../shared/services/baseModuleService';

/**
 * Cache category classification
 */
export enum CacheCategory {
  Invalid = 'Invalid',
  RarelyUsed = 'RarelyUsed',
  FrequentlyUsed = 'FrequentlyUsed',
}

/**
 * Cache item information
 */
export interface CacheItem {
  path: string;
  sha: string;
  sizeBytes: number;
  category: CacheCategory;
  lastModified: string;
}

/**
 * Cache statistics summary
 */
export interface CacheStatistics {
  invalidCount: number;
  invalidSizeBytes: number;
  rarelyUsedCount: number;
  rarelyUsedSizeBytes: number;
  frequentlyUsedCount: number;
  frequentlyUsedSizeBytes: number;
  totalCount: number;
  totalSizeBytes: number;
}

/**
 * Service for cache management operations
 * Provides type-safe communication with the TOOLS module backend
 */
export class CacheService extends BaseModuleService {
  constructor() {
    super('TOOLS');
  }

  /**
   * Scan cache directories and return categorized cache items
   */
  async scanCache(): Promise<CacheItem[]> {
    return this.sendArrayMessage<CacheItem>('SCAN_CACHE');
  }

  /**
   * Get cache statistics
   */
  async getStatistics(): Promise<CacheStatistics> {
    return this.sendMessage<CacheStatistics>('GET_CACHE_STATISTICS');
  }

  /**
   * Clean cache by category
   * @param category Cache category to clean
   * @returns Number of items deleted
   */
  async cleanCache(profileId: string, category: CacheCategory): Promise<number> {
    return this.sendMessage<number>('CLEAN_CACHE', profileId, { category });
  }

  /**
   * Delete specific cache item by SHA
   */
  async deleteCacheItem(profileId: string, sha: string): Promise<boolean> {
    return this.sendBooleanMessage('DELETE_CACHE_ITEM', profileId, { sha });
  }

  /**
   * Format bytes to human-readable string
   */
  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';

    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return `${(bytes / Math.pow(k, i)).toFixed(2)} ${sizes[i]}`;
  }

  /**
   * Format bytes to MiB (Mebibytes) for consistency with Python version
   */
  formatBytesToMiB(bytes: number): string {
    const mib = bytes / (1024 * 1024);
    return `${mib.toFixed(2)} MiB`;
  }
}

export const cacheService = new CacheService();
