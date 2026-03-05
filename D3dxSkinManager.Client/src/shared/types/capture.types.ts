/**
 * Screen capture related type definitions
 */

/**
 * Screen capture profile for saving capture area configurations
 */
export interface ScreenCaptureProfile {
  id: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
}

/**
 * Configuration for a screen capture operation
 */
export interface ScreenCaptureConfig {
  profileId?: string;
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  targetWindow?: string;
  showSelectionUI?: boolean;
  copyToClipboard?: boolean;
  saveToFile?: boolean;
  outputPath?: string;
}

/**
 * Result of a screen capture operation
 */
export interface ScreenCaptureResult {
  success: boolean;
  errorMessage?: string;
  savedPath?: string;
  copiedToClipboard: boolean;
  capturedArea?: ScreenCaptureArea;
  timestamp: string;
}

/**
 * Represents the captured area bounds
 */
export interface ScreenCaptureArea {
  x: number;
  y: number;
  width: number;
  height: number;
}

/**
 * Request to save a screen capture profile
 */
export interface SaveScreenCaptureProfileRequest {
  id?: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
}
