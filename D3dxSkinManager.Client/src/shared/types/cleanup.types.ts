// NOTE: Enums are camelCase because IpcHandler serializes with JsonStringEnumConverter(CamelCase)
export type OrphanCategory = 'thumbnail' | 'tempFile' | 'modCache';

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
