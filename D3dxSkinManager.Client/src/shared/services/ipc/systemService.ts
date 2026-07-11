import { BaseModuleService } from '../baseModuleService';
import { ProcessInfo } from '../../store/processStore';

export interface FileDialogOptions {
  title?: string;
  defaultPath?: string;
  filters?: { name: string; extensions: string[] }[];
  multiSelect?: boolean;
  rememberPathKey?: string;

  // Folder dialog options
  allowFileSelection?: boolean; // For folder dialogs: allow both files and folders

  // Advanced dialog configuration (OpenFileDialog properties)
  checkFileExists?: boolean;  // Default: true for files, false for folders
  checkPathExists?: boolean;  // Default: true
  validateNames?: boolean;    // Default: true for files, false for folders
  fileName?: string;          // Initial filename to display
}

export interface FileDialogResult {
  success: boolean;
  filePath?: string;
  error?: string;
}

export interface ScreenResolution {
  width: number;
  height: number;
}

/** Result of an app self-update check (GitHub Releases). Mirrors backend UpdateInfo. */
export interface UpdateInfo {
  currentVersion: string;
  latestVersion: string;
  updateAvailable: boolean;
  releaseName: string;
  releaseNotes: string;
  releaseUrl: string;
  publishedAt: string;
  /** True when a file-level changeset was computed (release + local manifest both present). */
  hasManifest: boolean;
  /** Files that would change (added + updated + removed). Valid when hasManifest. */
  changedFileCount: number;
  /** Bytes to download for the update. Valid when hasManifest. */
  downloadSize: number;
}

/** Whether a downloaded update is staged and waiting to be applied on the next startup. */
export interface UpdateState {
  pending: boolean;
  pendingVersion: string;
}

/**
 * System service for file operations, dialogs, and system settings
 * Handles all system-level operations and configuration
 */
export class SystemService extends BaseModuleService {
  constructor() {
    super('SYSTEM');
  }

  // File Dialog Operations

  async openFileDialog(options: FileDialogOptions = {}): Promise<FileDialogResult> {
    return this.sendMessage<FileDialogResult>('OPEN_FILE_DIALOG', undefined, {
      title: options.title || 'Select File',
      defaultPath: options.defaultPath,
      filters: options.filters,
      multiSelect: options.multiSelect || false,
      rememberPathKey: options.rememberPathKey
    });
  }

  async openFolderDialog(options: Omit<FileDialogOptions, 'multiSelect'> = {}): Promise<FileDialogResult> {
    return this.sendMessage<FileDialogResult>('OPEN_FOLDER_DIALOG', undefined, {
      title: options.title || 'Select Folder',
      defaultPath: options.defaultPath,
      rememberPathKey: options.rememberPathKey,
      allowFileSelection: options.allowFileSelection,
      filters: options.filters
    });
  }

  // File System Operations

  async openDirectory(directoryPath: string): Promise<void> {
    await this.sendMessage('OPEN_DIRECTORY', undefined, { directoryPath });
  }

  async openFileInExplorer(filePath: string): Promise<void> {
    await this.sendMessage('OPEN_FILE_IN_EXPLORER', undefined, { filePath });
  }

  async getAbsolutePath(path: string): Promise<string> {
    const result = await this.sendMessage<{ absolutePath: string }>('GET_ABSOLUTE_PATH', undefined, { path });
    return result.absolutePath;
  }

  // Process Operations

  async launchProcess(path: string, args?: string): Promise<void> {
    await this.sendMessage('LAUNCH_PROCESS', undefined, { path, args });
  }

  // App Self-Update

  /**
   * Check GitHub for a newer app version.
   * Backend: SystemFacade.CheckForUpdateAsync
   */
  async checkForUpdate(): Promise<UpdateInfo> {
    return this.sendMessage<UpdateInfo>('CHECK_FOR_UPDATE');
  }

  /**
   * Open a URL (release download page) in the default browser.
   * Backend: SystemFacade.OpenUrlAsync
   */
  async openUrl(url: string): Promise<void> {
    await this.sendMessage('OPEN_URL', undefined, { url });
  }

  /**
   * Batch content-veil (sensitivity) verdicts for preview-image urls (app:// / proxy://image).
   * Pure-CPU heuristic — "sensitive" | "safe" | "unknown" per url; the veil treats unknown as safe.
   * Backend: SystemFacade.ContentVeilCheckHandler
   */
  async checkContentVeil(urls: string[]): Promise<Record<string, string>> {
    const result = await this.sendMessage<{ verdicts: Record<string, string> }>(
      'CONTENT_VEIL_CHECK', undefined, { urls });
    return result?.verdicts ?? {};
  }

  /**
   * Start downloading + staging the update in the background (applied by the launcher on next start).
   * Returns immediately; watch progress in the Activity panel, completion via getUpdateState().
   * Backend: SystemFacade.DownloadUpdateHandler
   */
  async downloadUpdate(): Promise<{ started: boolean }> {
    return this.sendMessage<{ started: boolean }>('DOWNLOAD_UPDATE');
  }

  /**
   * Whether a downloaded update is staged and waiting to be applied on the next startup.
   * Backend: SystemFacade.GetUpdateStateAsync
   */
  async getUpdateState(): Promise<UpdateState> {
    return this.sendMessage<UpdateState>('GET_UPDATE_STATE');
  }

  /**
   * Restart via the launcher to apply a staged update (the app exits shortly after).
   * Backend: SystemFacade.RestartForUpdateHandler
   */
  async restartForUpdate(): Promise<{ restarting: boolean }> {
    return this.sendMessage<{ restarting: boolean }>('RESTART_FOR_UPDATE');
  }

  // Screen Info

  async getScreenResolution(): Promise<ScreenResolution> {
    return this.sendMessage<ScreenResolution>('GET_SCREEN_RESOLUTION');
  }

  // Long-running process registry (Activity panel / status bar)

  async getProcesses(): Promise<{ processes: ProcessInfo[] }> {
    return this.sendMessage<{ processes: ProcessInfo[] }>('GET_PROCESSES');
  }

  async cancelProcess(id: string): Promise<void> {
    await this.sendMessage('CANCEL_PROCESS', undefined, { id });
  }

  async resumeProcess(id: string): Promise<void> {
    await this.sendMessage('RESUME_PROCESS', undefined, { id });
  }

  async clearCompletedProcesses(): Promise<void> {
    await this.sendMessage('CLEAR_COMPLETED_PROCESSES');
  }
}