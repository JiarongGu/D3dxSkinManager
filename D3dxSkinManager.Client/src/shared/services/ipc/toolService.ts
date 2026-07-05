import { BaseModuleService } from '../baseModuleService';
import type {
  ScreenCaptureProfile,
  ScreenCaptureConfig,
  ScreenCaptureResult,
  SaveScreenCaptureProfileRequest,
} from '../../types/capture.types';
import type {
  ExportConfig,
  ExportResult,
  PackageAnalysis,
  ImportConfig,
  ImportResult,
} from '../../types/modPackage.types';
import type {
  OrphanCategory,
  OrphanScanResult,
  CleanupResult,
} from '../../types/cleanup.types';
import type { FullAnalysisReport, AnalysisSessionSummary, ModHealthSummary } from '../../types/analysis.types';
import type { ModFixTool } from '../../types/modFix.types';

/**
 * Service for tool operations (screen capture, etc.)
 * Module: TOOL
 * Handles screen capture profiles and operations
 */
export class ToolService extends BaseModuleService {
  constructor() {
    super('TOOL');
  }

  // ===== Screen Capture Profile Management =====

  /**
   * Get all capture profiles
   */
  async getProfiles(profileId: string): Promise<ScreenCaptureProfile[]> {
    return this.sendArrayMessage<ScreenCaptureProfile>('SCREEN_CAPTURE_GET_PROFILES', profileId, undefined);
  }

  /**
   * Get a specific capture profile by ID
   */
  async getProfile(profileId: string, id: string): Promise<ScreenCaptureProfile | undefined> {
    return this.sendOptionalMessage<ScreenCaptureProfile>('SCREEN_CAPTURE_GET_PROFILE', profileId, { id });
  }

  /**
   * Save a capture profile (create or update)
   */
  async saveProfile(profileId: string, request: SaveScreenCaptureProfileRequest): Promise<string> {
    return this.sendMessage<string>('SCREEN_CAPTURE_SAVE_PROFILE', profileId, request);
  }

  /**
   * Delete a capture profile
   */
  async deleteProfile(profileId: string, id: string): Promise<void> {
    return this.sendMessage<void>('SCREEN_CAPTURE_DELETE_PROFILE', profileId, { id });
  }

  // ===== Screen Capture Operations =====

  /**
   * Capture screen with specific configuration
   */
  async captureScreen(profileId: string, config: ScreenCaptureConfig): Promise<ScreenCaptureResult> {
    return this.sendMessage<ScreenCaptureResult>('SCREEN_CAPTURE_SCREEN', profileId, config);
  }

  // ===== Border Overlay =====

  /**
   * Show border overlay at specified position and size
   * Note: Requires profileId because TOOL module is profile-scoped
   */
  async showBorder(profileId: string, x: number, y: number, width: number, height: number): Promise<void> {
    return this.sendMessage<void>('SCREEN_CAPTURE_SHOW_BORDER', profileId, { x, y, width, height });
  }

  /**
   * Hide the border overlay
   * Note: Requires profileId because TOOL module is profile-scoped
   */
  async hideBorder(profileId: string): Promise<void> {
    return this.sendMessage<void>('SCREEN_CAPTURE_HIDE_BORDER', profileId, undefined);
  }

  /**
   * Toggle the standalone capture control panel (topmost WinForms window)
   * If the panel is open, it will be closed. If it's closed, it will be opened.
   * Note: Requires profileId even though capture profiles are global, because TOOL module is profile-scoped
   */
  async toggleControlPanel(profileId: string): Promise<void> {
    return this.sendMessage<void>('SCREEN_CAPTURE_TOGGLE_CONTROL_PANEL', profileId, undefined);
  }

  /** Pop the analyzer out into a separate window (toggle: open if closed, close if open). */
  async toggleAnalyzerWindow(profileId: string): Promise<void> {
    return this.sendMessage<void>('ANALYZER_TOGGLE_WINDOW', profileId, undefined);
  }

  /** From the analyzer pop-out window: ask the MAIN window to locate these mods in the list. */
  async requestLocate(profileId: string, modIds: string[], categoryId?: string): Promise<void> {
    return this.sendMessage<void>('ANALYZER_REQUEST_LOCATE', profileId, { modIds, categoryId });
  }

  // ===== Mod Package Export/Import =====

  async exportModPackage(profileId: string, config: Omit<ExportConfig, 'outputPath'> & { outputPath: string }): Promise<ExportResult> {
    return this.sendMessage<ExportResult>('MOD_PACKAGE_EXPORT', profileId, config);
  }

  async analyzeModPackage(profileId: string, packagePath: string): Promise<PackageAnalysis> {
    return this.sendMessage<PackageAnalysis>('MOD_PACKAGE_ANALYZE', profileId, { packagePath });
  }

  async importModPackage(profileId: string, config: ImportConfig): Promise<ImportResult> {
    return this.sendMessage<ImportResult>('MOD_PACKAGE_IMPORT', profileId, config);
  }

  // ===== File Cleanup =====

  /**
   * Scan for orphaned items in a specific category
   * Backend: ToolFacade.ScanOrphansAsync
   */
  async scanOrphans(profileId: string, category: OrphanCategory): Promise<OrphanScanResult> {
    return this.sendMessage<OrphanScanResult>('SCAN_ORPHANS', profileId, { category });
  }

  /**
   * Scan all orphan categories at once
   * Backend: ToolFacade → FileCleanupService.ScanAllOrphansAsync
   */
  async scanAllOrphans(profileId: string): Promise<OrphanScanResult[]> {
    return this.sendArrayMessage<OrphanScanResult>('SCAN_ALL_ORPHANS', profileId);
  }

  /**
   * Delete specified orphaned items
   * Backend: ToolFacade.CleanOrphansAsync
   */
  async cleanOrphans(profileId: string, category: OrphanCategory, paths: string[]): Promise<CleanupResult> {
    return this.sendMessage<CleanupResult>('CLEAN_ORPHANS', profileId, { category, paths });
  }

  // ===== Mod Analysis =====

  async startAnalysis(profileId: string, categoryId?: string): Promise<void> {
    return this.sendMessage<void>('ANALYSIS_START', profileId, { categoryId });
  }

  async pauseAnalysis(profileId: string): Promise<void> {
    return this.sendMessage<void>('ANALYSIS_PAUSE', profileId);
  }

  async resumeAnalysis(profileId: string, sessionId?: string): Promise<void> {
    return this.sendMessage<void>('ANALYSIS_RESUME', profileId, sessionId ? { sessionId } : undefined);
  }

  async cancelAnalysis(profileId: string): Promise<void> {
    return this.sendMessage<void>('ANALYSIS_CANCEL', profileId);
  }

  async getAnalysisReport(profileId: string, sessionId: string): Promise<FullAnalysisReport> {
    return this.sendMessage<FullAnalysisReport>('ANALYSIS_GET_REPORT', profileId, { sessionId });
  }

  async getAnalysisHistory(profileId: string): Promise<AnalysisSessionSummary[]> {
    return this.sendArrayMessage<AnalysisSessionSummary>('ANALYSIS_GET_HISTORY', profileId);
  }

  /** Latest per-mod health (warning/error only) from the most recent scan — for the mod-list badge. */
  async getLatestHealth(profileId: string): Promise<ModHealthSummary[]> {
    return this.sendArrayMessage<ModHealthSummary>('ANALYSIS_GET_LATEST_HEALTH', profileId);
  }

  async deleteAnalysisSession(profileId: string, sessionId: string): Promise<void> {
    return this.sendMessage<void>('ANALYSIS_DELETE_SESSION', profileId, { sessionId });
  }

  async clearAllAnalysis(profileId: string): Promise<void> {
    return this.sendMessage<void>('ANALYSIS_CLEAR_ALL', profileId);
  }

  // ===== Mod ID Migration (fire-and-forget — results via events) =====

  /**
   * Scan for mods with non-GUID IDs that need migration.
   * Fire-and-forget: result arrives via MOD_ID_MIGRATION_SCAN_COMPLETE event.
   * Backend: ToolFacade.StartModIdMigrationScan → ModIdMigrationService.ScanAsync
   */
  async scanModIdMigration(profileId: string): Promise<void> {
    return this.sendMessage<void>('MOD_ID_MIGRATION_SCAN', profileId);
  }

  /**
   * Execute mod ID migration (rename files, update database).
   * Fire-and-forget: progress via MOD_ID_MIGRATION_PROGRESS, result via MOD_ID_MIGRATION_COMPLETE.
   * Backend: ToolFacade.StartModIdMigrationExecute → ModIdMigrationService.MigrateAsync
   */
  async executeModIdMigration(profileId: string): Promise<void> {
    return this.sendMessage<void>('MOD_ID_MIGRATION_EXECUTE', profileId);
  }

  // ===== Mod Fix (hash-fix script runner — fire-and-forget) =====

  /**
   * Run a fix script (.py/.exe/.bat/.cmd) against one or all mods.
   * Empty/omitted modIds = run against ALL mods. Fire-and-forget: progress via MOD_FIX_PROGRESS,
   * final result via MOD_FIX_COMPLETE; the run also appears in the Activity panel (ProcessRegistry).
   * Backend: ToolFacade.StartModFix → ModFixService.RunFixAsync
   */
  async runModFix(
    profileId: string,
    request: { scriptPath: string; modIds?: string[]; recompress?: boolean },
  ): Promise<void> {
    return this.sendMessage<void>('RUN_MOD_FIX', profileId, request);
  }

  // ===== Fix-tool library (per-profile collection of named fix tools) =====

  async getFixTools(profileId: string): Promise<ModFixTool[]> {
    return this.sendArrayMessage<ModFixTool>('FIX_TOOLS_GET', profileId);
  }

  /** Import a fix tool by copying a file or folder into the profile's fix-tool library. */
  async importFixTool(
    profileId: string,
    request: { name: string; sourcePath: string; isFolder: boolean; entryFile?: string; description?: string },
  ): Promise<ModFixTool> {
    return this.sendMessage<ModFixTool>('FIX_TOOLS_IMPORT', profileId, request);
  }

  async deleteFixTool(profileId: string, id: string): Promise<void> {
    return this.sendMessage<void>('FIX_TOOLS_DELETE', profileId, { id });
  }

  /** Rename a folder-based fix tool. Returns the new id (sanitized + uniquified). */
  async renameFixTool(profileId: string, id: string, newName: string): Promise<{ id: string }> {
    return this.sendMessage<{ id: string }>('FIX_TOOLS_RENAME', profileId, { id, newName });
  }

  /** Choose which file(s) inside a folder tool are its runnable entries (empty = revert to auto). */
  async setFixToolEntries(profileId: string, id: string, entries: string[]): Promise<void> {
    return this.sendMessage<void>('FIX_TOOLS_SET_ENTRIES', profileId, { id, entries });
  }

  /** Probe for an installed Python interpreter (py/python/python3); returns the command or undefined. */
  async detectPython(profileId: string): Promise<string | undefined> {
    const res = await this.sendMessage<{ python?: string }>('FIX_TOOLS_DETECT_PYTHON', profileId);
    return res?.python ?? undefined;
  }
}

// Export singleton instance
export const toolService = new ToolService();
