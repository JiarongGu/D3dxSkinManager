export interface ModIdMigrationScanResult {
  totalMods: number;
  modsNeedingMigration: number;
  items: ModIdMigrationItem[];
}

export interface ModIdMigrationItem {
  oldId: string;
  newId: string;
  modName: string;
  hasArchive: boolean;
  hasCache: boolean;
  hasPreview: boolean;
}

export interface ModIdMigrationProgress {
  current: number;
  total: number;
  modName: string;
}

export interface ModIdMigrationResult {
  total: number;
  succeeded: number;
  failed: number;
  results: ModIdMigrationItemResult[];
}

export interface ModIdMigrationItemResult {
  oldId: string;
  newId: string;
  modName: string;
  success: boolean;
  error?: string;
}
