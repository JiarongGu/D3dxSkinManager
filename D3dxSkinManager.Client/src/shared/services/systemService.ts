import { BaseModuleService } from './baseModuleService';

export interface FileDialogOptions {
  title?: string;
  defaultPath?: string;
  filters?: { name: string; extensions: string[] }[];
  multiSelect?: boolean;
  rememberPathKey?: string;
}

export interface FileDialogResult {
  success: boolean;
  filePath?: string;
  error?: string;
}

export interface SystemSettings {
  fileDialogPaths: Record<string, string>;
  lastUpdated: string;
}

/**
 * System service for file operations, dialogs, and system settings
 * Handles all system-level operations and configuration
 */
class SystemService extends BaseModuleService {
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

  async openFolderDialog(options: Omit<FileDialogOptions, 'filters' | 'multiSelect'> = {}): Promise<FileDialogResult> {
    return this.sendMessage<FileDialogResult>('OPEN_FOLDER_DIALOG', undefined, {
      title: options.title || 'Select Folder',
      defaultPath: options.defaultPath,
      rememberPathKey: options.rememberPathKey
    });
  }

  async saveFileDialog(options: FileDialogOptions = {}): Promise<FileDialogResult> {
    return this.sendMessage<FileDialogResult>('SAVE_FILE_DIALOG', undefined, {
      title: options.title || 'Save File',
      defaultPath: options.defaultPath,
      filters: options.filters,
      rememberPathKey: options.rememberPathKey
    });
  }

  // File System Operations

  async openFile(filePath: string): Promise<void> {
    await this.sendMessage('OPEN_FILE', undefined, { filePath });
  }

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

  // System Settings Operations

  async getSystemSettings(): Promise<SystemSettings> {
    return this.sendMessage<SystemSettings>('GET_SETTINGS');
  }

  async updateSystemSettings(settings: SystemSettings): Promise<void> {
    await this.sendMessage('UPDATE_SETTINGS', undefined, { settings });
  }

  async resetSystemSettings(): Promise<SystemSettings> {
    const result = await this.sendMessage<{ settings: SystemSettings }>('RESET_SETTINGS');
    return result.settings;
  }

  // Drag-Drop Operations

  async startDropListening(): Promise<void> {
    await this.sendMessage('START_DROP_LISTENING');
  }

  async stopDropListening(): Promise<void> {
    await this.sendMessage('STOP_DROP_LISTENING');
  }
}

export const systemService = new SystemService();

// Export as fileDialogService for backward compatibility
export const fileDialogService = systemService;
