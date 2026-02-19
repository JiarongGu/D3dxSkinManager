import { BaseModuleService } from '../../../shared/services/baseModuleService';

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

class LaunchService extends BaseModuleService {
  constructor() {
    super('LAUNCH');
  }

  // 3DMigoto methods
  async getAvailableVersions(profileId: string): Promise<D3DMigotoVersion[]> {
    return this.sendArrayMessage<D3DMigotoVersion>('LAUNCH_GET_VERSIONS', profileId);
  }

  async getCurrentVersion(profileId: string): Promise<string | null> {
    return this.sendNullableMessage<string>('LAUNCH_GET_CURRENT', profileId);
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
}

export const launchService = new LaunchService();
