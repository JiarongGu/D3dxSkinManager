import { BaseModuleService } from '../../../../../shared/services/baseModuleService';
import type {
  PythonConfiguration,
  MigrationAnalysis,
  MigrationOptions,
  MigrationProgress,
  MigrationError,
  MigrationResult
} from '../../../../../shared/types/migration.types';
import {
  MigrationStage,
  ArchiveHandling,
  PostMigrationAction
} from '../../../../../shared/types/migration.types';

// Re-export all types and enums for backward compatibility
export type {
  PythonConfiguration,
  MigrationAnalysis,
  MigrationOptions,
  MigrationProgress,
  MigrationError,
  MigrationResult
};

export {
  MigrationStage,
  ArchiveHandling,
  PostMigrationAction
};

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
  async autoDetect(): Promise<string | undefined> {
    return this.sendOptionalMessage<string>('AUTO_DETECT');
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
      migrateCategories: options.migrateCategories,
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
