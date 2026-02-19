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

class FileDialogService extends BaseModuleService {
  constructor() {
    super('SETTINGS');
  }

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

  async openFile(filePath: string): Promise<void> {
    await this.sendMessage('OPEN_FILE', undefined, { filePath });
  }

  async openDirectory(directoryPath: string): Promise<void> {
    await this.sendMessage('OPEN_DIRECTORY', undefined, { directoryPath });
  }

  async openFileInExplorer(filePath: string): Promise<void> {
    await this.sendMessage('OPEN_FILE_IN_EXPLORER', undefined, { filePath });
  }

  async launchProcess(path: string, args?: string): Promise<void> {
    await this.sendMessage('LAUNCH_PROCESS', undefined, { path, args });
  }
}

export const fileDialogService = new FileDialogService();
