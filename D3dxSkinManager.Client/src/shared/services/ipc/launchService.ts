import { BaseModuleService } from '../baseModuleService';

export interface D3DMigotoVersion {
  name: string;
  filePath: string;
  sizeBytes: number;
  sizeFormatted: string;
  isDeployed: boolean;
}

export interface DeploymentResult {
  success: boolean;
  message?: string;
  error?: string;
}

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

export class LaunchService extends BaseModuleService {
  constructor() {
    super('LAUNCH');
  }

  // 3DMigoto methods
  async getAvailableVersions(profileId: string): Promise<D3DMigotoVersion[]> {
    return this.sendArrayMessage<D3DMigotoVersion>('LAUNCH_GET_VERSIONS', profileId);
  }

  async getCurrentVersion(profileId: string): Promise<string | undefined> {
    return this.sendOptionalMessage<string>('LAUNCH_GET_CURRENT', profileId);
  }

  async deployVersion(profileId: string, versionName: string): Promise<DeploymentResult> {
    return this.sendMessage<DeploymentResult>('LAUNCH_DEPLOY', profileId,{ versionName });
  }

  async launch3DMigoto(profileId: string): Promise<boolean> {
    return this.sendBooleanMessage('LAUNCH_3DMIGOTO', profileId);
  }

  // Game methods
  async launchGame(profileId: string, args?: string): Promise<boolean> {
    return this.sendBooleanMessage('LAUNCH_GAME', profileId, args ? { arguments: args } : undefined);
  }

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
}
