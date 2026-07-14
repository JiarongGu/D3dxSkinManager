/**
 * Migration Types - Shared across the application
 * Types for migrating data from Python d3dxSkinManage to React version
 */

// C# enums serialize as camelCase strings (JsonStringEnumConverter(CamelCase)); the wire value the
// backend sends is camelCase, so these MUST be camelCase to match (see .claude/knowledge/enum-serialization.md).
export enum MigrationStage {
  Analyzing = 'analyzing',
  CreatingDatabase = 'creatingDatabase',
  MigratingMetadata = 'migratingMetadata',
  CopyingArchives = 'copyingArchives',
  CopyingPreviews = 'copyingPreviews',
  ConvertingConfiguration = 'convertingConfiguration',
  ConvertingCategories = 'convertingCategories',
  Verifying = 'verifying',
  Finalizing = 'finalizing',
  Complete = 'complete',
  Error = 'error'
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
  modId?: string;
  stepCode?: string;
  categoryCode?: string;
  timestamp: string;
  parameters?: Record<string, string>;
}

// Mirrors C# MigrationResult (Modules/Migration/Models/MigrationResult.cs). Fields must match the
// serialized model exactly — the old configurationMigrated/CategoriesMigrated/startTime/endTime never
// existed on the backend and always read as undefined.
export interface MigrationResult {
  success: boolean;
  modsMigrated: number;
  archivesCopied: number;
  previewsCopied: number;
  categoryRulesCreated: number;
  totalBytesProcessed: number;
  errors: string[];
  warnings: string[];
  detailedErrors: MigrationError[];
  logFilePath?: string;
  duration: string; // C# TimeSpan serializes as an "hh:mm:ss" string
  failedAtStep?: number;
  failedStepName?: string;
}
