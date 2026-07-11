import { BaseModuleService } from '../baseModuleService';

/** A model importer (ZZMI/EFMI/...) discovered in an XXMI Launcher install. */
export interface XxmiImporter {
  name: string;
  /** Importer folder (parent of Mods) — bind the profile work-dir to this. */
  importerDir: string;
  /** Resolved Mods folder (= importerDir/Mods) — XXMI's deploy target. */
  modsDir: string;
  gameFolder?: string;
  isActive: boolean;
  isInstalled: boolean;
}

export interface XxmiDetectResult {
  found: boolean;
  launcherExe?: string;
  configPath?: string;
  importers: XxmiImporter[];
}

/** Latest XXMI-Launcher installer release (GitHub) — the "get XXMI" assist. */
export interface XxmiInstallerInfo {
  version: string;
  fileName: string;
  sizeBytes: number;
  url: string;
}

export class LaunchService extends BaseModuleService {
  constructor() {
    super('LAUNCH');
  }

  // Game methods
  async launchCustomProgram(profileId: string, executablePath: string, args?: string): Promise<boolean> {
    return this.sendBooleanMessage('LAUNCH_CUSTOM', profileId, {
      executablePath,
      ...(args && { arguments: args })
    });
  }

  // XXMI methods

  /**
   * Probe a folder for an XXMI Launcher install and return its importers with resolved Mods paths.
   * Backend: LaunchFacade.DetectXxmiAsync
   */
  async detectXxmi(profileId: string, folderPath: string): Promise<XxmiDetectResult> {
    return this.sendMessage<XxmiDetectResult>('LAUNCH_XXMI_DETECT', profileId, { folderPath });
  }

  /**
   * Latest XXMI-Launcher installer (.msi) from the GitHub API — shown in the download confirm.
   * Backend: LaunchFacade.GetXxmiInstallerAsync
   */
  async getXxmiInstaller(profileId: string): Promise<XxmiInstallerInfo> {
    return this.sendMessage<XxmiInstallerInfo>('LAUNCH_XXMI_INSTALLER_INFO', profileId);
  }

  /**
   * Start downloading the installer (fire-and-forget — the IPC acks immediately; progress shows in
   * the Activity panel and the installer opens itself when the download lands).
   * Backend: LaunchFacade.StartXxmiInstallerDownload
   */
  async downloadXxmiInstaller(profileId: string, info: XxmiInstallerInfo): Promise<void> {
    await this.sendMessage<{ started: boolean }>('LAUNCH_XXMI_INSTALLER_DOWNLOAD', profileId, {
      version: info.version,
      fileName: info.fileName,
      sizeBytes: info.sizeBytes,
      url: info.url,
    });
  }
}
