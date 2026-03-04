import { BaseModuleService } from '../baseModuleService';

export enum ValidationSeverity {
  Info = 'Info',
  Warning = 'Warning',
  Error = 'Error'
}

export interface ValidationResult {
  checkName: string;
  isValid: boolean;
  message: string;
  severity: ValidationSeverity;
}

export interface StartupValidationReport {
  isValid: boolean;
  results: ValidationResult[];
  errorCount: number;
  warningCount: number;
  infoCount: number;
}

export class ValidationService extends BaseModuleService {
  constructor() {
    super('TOOL');
  }

  async validateStartup(): Promise<StartupValidationReport> {
    return this.sendMessage<StartupValidationReport>('VALIDATE_STARTUP');
  }
}