import { BaseModuleService } from '../baseModuleService';
import type {
  ScreenCaptureProfile,
  ScreenCaptureConfig,
  ScreenCaptureResult,
  SaveScreenCaptureProfileRequest,
} from '../../types/capture.types';

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
  async getProfiles(): Promise<ScreenCaptureProfile[]> {
    return this.sendArrayMessage<ScreenCaptureProfile>('SCREEN_CAPTURE_GET_PROFILES', undefined, undefined);
  }

  /**
   * Get a specific capture profile by ID
   */
  async getProfile(id: string): Promise<ScreenCaptureProfile | undefined> {
    return this.sendOptionalMessage<ScreenCaptureProfile>('SCREEN_CAPTURE_GET_PROFILE', undefined, { id });
  }

  /**
   * Save a capture profile (create or update)
   */
  async saveProfile(request: SaveScreenCaptureProfileRequest): Promise<string> {
    return this.sendMessage<string>('SCREEN_CAPTURE_SAVE_PROFILE', undefined, request);
  }

  /**
   * Delete a capture profile
   */
  async deleteProfile(id: string): Promise<void> {
    return this.sendMessage<void>('SCREEN_CAPTURE_DELETE_PROFILE', undefined, { id });
  }

  // ===== Screen Capture Operations =====

  /**
   * Capture screen with specific configuration
   */
  async captureScreen(config: ScreenCaptureConfig): Promise<ScreenCaptureResult> {
    return this.sendMessage<ScreenCaptureResult>('SCREEN_CAPTURE_SCREEN', undefined, config);
  }

  /**
   * Capture screen using a saved profile
   */
  async captureWithProfile(profileId: string): Promise<ScreenCaptureResult> {
    return this.sendMessage<ScreenCaptureResult>('SCREEN_CAPTURE_WITH_PROFILE', undefined, { profileId });
  }

  /**
   * Interactive capture with selection UI
   */
  async captureInteractive(isDarkTheme: boolean = false): Promise<ScreenCaptureResult> {
    return this.sendMessage<ScreenCaptureResult>('SCREEN_CAPTURE_INTERACTIVE', undefined, { isDarkTheme });
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
}

// Export singleton instance
export const toolService = new ToolService();
