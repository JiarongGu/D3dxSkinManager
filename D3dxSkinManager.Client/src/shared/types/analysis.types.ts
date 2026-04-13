// NOTE: Enums are camelCase because IpcHandler serializes with JsonStringEnumConverter(CamelCase)
export type HealthIssueSeverity = 'error' | 'warning' | 'info';
export type HealthIssueType = 'noIniFile' | 'emptyMod' | 'missingResource' | 'invalidIniSyntax' | 'emptyIniFile' | 'staleHash' | 'missingPlugin';
export type DuplicateType = 'identical' | 'textureVariant';
export type AnalysisStatus = 'idle' | 'running' | 'paused' | 'completed' | 'cancelled';

export interface ModHealthIssue {
  type: HealthIssueType;
  severity: HealthIssueSeverity;
  message: string;
  filePath?: string;
}

export interface ModAnalysisResult {
  modId: string;
  modName: string;
  categoryName: string;
  isLoaded: boolean;
  hasCache: boolean;
  isAvailable: boolean;
  healthStatus: string;
  issues: ModHealthIssue[];
  iniFileCount: number;
  resourceFileCount: number;
  textureOverrideCount: number;
  targetHashes: string[];
  bufferHash: string;
  textureHash: string;
  bufferSizeBytes: number;
  textureSizeBytes: number;
  pluginDependencies: string[];
  previewPath?: string;
}

export interface DuplicateGroup {
  type: DuplicateType;
  groupLabel: string;
  sharedHashes: string[];
  mods: ModAnalysisResult[];
  allHashesMatch: boolean;
}

export interface ModConflict {
  hash: string;
  mods: ModAnalysisResult[];
}

export interface HashFrequency {
  hash: string;
  modCount: number;
  isSuspicious: boolean;
}

export interface FullAnalysisReport {
  sessionId: string;
  categoryId?: string;
  status: AnalysisStatus;
  totalMods: number;
  analyzedCount: number;
  skippedCount: number;
  healthyCount: number;
  warningCount: number;
  errorCount: number;
  results: ModAnalysisResult[];
  duplicateGroups: DuplicateGroup[];
  identicalCount: number;
  textureVariantCount: number;
  conflicts: ModConflict[];
  conflictCount: number;
  affectedModCount: number;
  suspiciousHashes: HashFrequency[];
}

export interface AnalysisSessionSummary {
  id: string;
  categoryId?: string;
  categoryName?: string;
  status: string;
  totalMods: number;
  analyzedCount: number;
  healthyCount: number;
  warningCount: number;
  errorCount: number;
  identicalCount: number;
  textureVariantCount: number;
  conflictCount: number;
  startedAt: string;
  completedAt?: string;
}

export interface AnalysisProgress {
  sessionId: string;
  stage: string;
  current: number;
  total: number;
  currentModName: string;
  status: AnalysisStatus;
  healthyCount: number;
  warningCount: number;
  errorCount: number;
  lastModName?: string;
  lastHealthStatus?: string;
}
