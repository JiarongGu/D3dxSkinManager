import { BaseModuleService } from '../../../shared/services/baseModuleService';

// Migration Models (matching C# backend)

export enum MigrationStage {
  Analyzing = 'Analyzing',
  CreatingDatabase = 'CreatingDatabase',
  MigratingMetadata = 'MigratingMetadata',
  CopyingArchives = 'CopyingArchives',
  CopyingPreviews = 'CopyingPreviews',
  ConvertingConfiguration = 'ConvertingConfiguration',
  ConvertingClassifications = 'ConvertingClassifications',
  Verifying = 'Verifying',
  Finalizing = 'Finalizing',
  Complete = 'Complete',
  Error = 'Error'
}

export enum ArchiveHandling {
  Copy = 'Copy',
  Move = 'Move',
  Link = 'Link'
}

export enum PostMigrationAction {
  Keep = 'Keep',
  BackupAndRemove = 'BackupAndRemove',
  Remove = 'Remove'
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
  migrateClassifications: boolean;
  archiveMode: ArchiveHandling;
  postAction: PostMigrationAction;
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
}

export interface MigrationResult {
  success: boolean;
  modsMigrated: number;
  archivesCopied: number;
  previewsCopied: number;
  configurationMigrated: boolean;
  classificationsMigrated: boolean;
  errors: string[];
  warnings: string[];
  logFilePath: string;
  duration: string;
  startTime: string;
  endTime: string;
}

/**
 * Service for migrating data from Python d3dxSkinManage to React version
 * Provides type-safe communication with the MIGRATION module backend
 */
class MigrationService extends BaseModuleService {
  constructor() {
    super('MIGRATION');
  }

  /**
   * Auto-detect Python installation path
   */
  async autoDetect(): Promise<string | null> {
    return this.sendNullableMessage<string>('AUTO_DETECT');
  }

  /**
   * Analyze Python installation for migration
   */
  async analyzePythonInstallation(profileId: string, pythonPath: string): Promise<MigrationAnalysis> {
    return this.sendMessage<MigrationAnalysis>('ANALYZE', profileId, { pythonPath });
  }

  /**
   * Start migration process
   * Note: Progress tracking not yet implemented (requires polling or websocket)
   */
  async startMigration(profileId: string, options: MigrationOptions): Promise<MigrationResult> {
    return this.sendMessage<MigrationResult>('START', profileId, {
      sourcePath: options.sourcePath,
      environmentName: options.environmentName,
      migrateArchives: options.migrateArchives,
      migrateMetadata: options.migrateMetadata,
      migratePreviews: options.migratePreviews,
      migrateConfiguration: options.migrateConfiguration,
      migrateClassifications: options.migrateClassifications,
      archiveMode: options.archiveMode,
      postAction: options.postAction
    });
  }

  /**
   * Validate migration result by comparing source and destination
   */
  async validateMigration(profileId: string, pythonPath: string, reactDataPath: string): Promise<boolean> {
    return this.sendBooleanMessage('VALIDATE', profileId, { pythonPath, reactDataPath });
  }

  /**
   * Format bytes to human-readable string
   */
  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  }

  /**
   * Format duration in seconds to human-readable string
   */
  formatDuration(seconds: number): string {
    if (seconds < 60) return `${Math.round(seconds)}s`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${Math.round(seconds % 60)}s`;
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    return `${hours}h ${minutes}m`;
  }
}

// Export singleton instance
export const migrationService = new MigrationService();
