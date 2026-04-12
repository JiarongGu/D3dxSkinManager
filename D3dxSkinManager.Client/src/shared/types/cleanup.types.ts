export type OrphanCategory = 'Thumbnail' | 'TempFile' | 'ModCache';

export interface OrphanedItem {
  path: string;
  name: string;
  sizeBytes: number;
  lastModified: string;
  category: OrphanCategory;
}

export interface OrphanScanResult {
  category: OrphanCategory;
  items: OrphanedItem[];
  totalCount: number;
  totalSizeBytes: number;
}

export interface CleanupResult {
  category: OrphanCategory;
  deletedCount: number;
  freedBytes: number;
  failedCount: number;
  errors: string[];
}
