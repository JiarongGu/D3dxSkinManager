import { BaseModuleService } from '../../../shared/services/baseModuleService';

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

class ValidationService extends BaseModuleService {
  constructor() {
    super('TOOLS');
  }

  async validateStartup(): Promise<StartupValidationReport> {
    return this.sendMessage<StartupValidationReport>('VALIDATE_STARTUP');
  }
}

export const validationService = new ValidationService();
