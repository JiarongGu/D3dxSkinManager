/**
 * Tools module service
 * Handles validation and utility operations
 */

import { BaseModuleService } from '../../../shared/services/baseModuleService';

// Re-export from existing services for backward compatibility
export { validationService } from '../../../shared/services/ipc';

/**
 * Unified Tools service
 * Aggregates validation and utility services
 */
export class ToolsService extends BaseModuleService {
  constructor() {
    super('TOOL');
  }

  // Validation operations are handled by validationService
}

// Export singleton instance
export const toolsService = new ToolsService();
