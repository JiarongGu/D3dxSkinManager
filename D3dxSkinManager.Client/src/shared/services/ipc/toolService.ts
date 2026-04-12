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
}

// Export singleton instance
export const toolService = new ToolService();
