/**
 * Migration Types - Shared across the application
 * Types for migrating data from Python d3dxSkinManage to React version
 */

export enum MigrationStage {
  Analyzing = 'Analyzing',
  CreatingDatabase = 'CreatingDatabase',
  MigratingMetadata = 'MigratingMetadata',
  CopyingArchives = 'CopyingArchives',
  CopyingPreviews = 'CopyingPreviews',
  ConvertingConfiguration = 'ConvertingConfiguration',
  ConvertingCategories = 'ConvertingCategories',
  Verifying = 'Verifying',
  Finalizing = 'Finalizing',
  Complete = 'Complete',
  Error = 'Error'
}

export enum ArchiveHandling {
  Copy = 'Copy',
  Move = 'Move'
}

export enum PostMigrationAction {
  Keep = 'Keep'
}

export interface PythonConfiguration {
  styleTheme?: string;
  uuid?: string;
  ocdWindowName?: string;
  ocdWindowWidth?: number;
  ocdWindowHeight?: number;
  workDirectory?: string;
}

export interface MigrationAnalysis {
  isValid: boolean;
  sourcePath: string;
  totalMods: number;
  totalArchiveSize: number;
  totalArchiveSizeFormatted: string;
  totalPreviewSize: number;
  totalPreviewSizeFormatted: string;
  environments: string[];
  activeEnvironment: string;
  configuration?: PythonConfiguration;
  errors: string[];
  warnings: string[];
}

export interface MigrationOptions {
  sourcePath: string;
  environmentName: string;
  migrateArchives: boolean;
  migrateMetadata: boolean;
  migratePreviews: boolean;
  migrateConfiguration: boolean;
  migrateCategories: boolean;
  archiveMode: ArchiveHandling;
  postAction?: PostMigrationAction; // Optional - defaults to Keep on backend
}

export interface MigrationProgress {
  stage: MigrationStage;
  currentTask: string;
  processedItems: number;
  totalItems: number;
  percentComplete: number;
  bytesProcessed: number;
  totalBytes: number;
  speedBytesPerSecond: number;
  estimatedTimeRemainingSeconds: number;

  // Step tracking
  currentStep: number;
  totalSteps: number;
  stepName: string;
  stepProgress: number;  // Progress within current step (0-100)
}

export interface MigrationError {
  message: string;
  messageCode?: string;
  modName?: string;
  modSha?: string;
  stepCode?: string;
  categoryCode?: string;
  timestamp: string;
  parameters?: Record<string, string>;
}

export interface MigrationResult {
  success: boolean;
  modsMigrated: number;
  archivesCopied: number;
  previewsCopied: number;
  configurationMigrated: boolean;
  CategoriesMigrated: boolean;
  errors: string[];
  warnings: string[];
  detailedErrors: MigrationError[];
  logFilePath: string;
  duration: string;
  startTime: string;
  endTime: string;
  failedAtStep?: number;
  failedStepName?: string;
}
