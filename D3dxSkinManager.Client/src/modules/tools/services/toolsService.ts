/**
 * Tools module service
 * Handles cache management, classification, and validation operations
 */

import { BaseModuleService } from '../../../shared/services/baseModuleService';

// Re-export from existing services for backward compatibility
export { cacheService } from './cacheService';
export { validationService } from './validationService';

/**
 * Unified Tools service
 * Aggregates cache, d3dmigoto, and validation services
 */
class ToolsService extends BaseModuleService {
  constructor() {
    super('TOOLS');
  }

  // Cache operations are handled by cacheService
  // D3DMigoto operations are handled by d3dMigotoService
  // Validation operations are handled by validationService
}

// Export singleton instance
export const toolsService = new ToolsService();
