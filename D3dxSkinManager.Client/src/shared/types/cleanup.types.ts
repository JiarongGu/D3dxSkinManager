// NOTE: Enums are camelCase because IpcHandler serializes with JsonStringEnumConverter(CamelCase)
export type OrphanCategory = 'thumbnail' | 'preview' | 'tempFile' | 'modCache' | 'orphanedArchive' | 'missingArchive';

export interface OrphanedItem {
  path: string;
  name: string;
  sizeBytes: number;
  lastModified: string;
  category: OrphanCategory;
  /** Set by the backend scanner — do not guess from the name (archives are extensionless files). */
  isDirectory: boolean;
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
