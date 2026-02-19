/**
 * Base class for module-specific services
 * Provides type-safe IPC communication with a specific backend module
 */

import { photinoService } from './photinoService';
import { ModuleName } from '../types/message.types';

/**
 * Abstract base class for module services
 * Each module service extends this to provide typed operations
 */
export abstract class BaseModuleService {
  protected readonly moduleName: ModuleName;

  constructor(moduleName: ModuleName) {
    this.moduleName = moduleName;
  }

  /**
   * Send a message to this module's backend facade
   * @param type - The action type (e.g., 'GET_ALL', 'LOAD', 'CREATE')
   * @param payload - Optional payload data
   * @returns Promise with typed response data
   */
  protected async sendMessage<T>(type: string, profileId?: string, payload?: any): Promise<T> {
    return photinoService.sendMessage<T>({ module: this.moduleName, type, profileId, payload });
  }

  /**
   * Send a message and return a boolean result
   * Convenience method for operations that return success/failure
   */
  protected async sendBooleanMessage(type: string, profileId?: string, payload?: any): Promise<boolean> {
    return this.sendMessage<boolean>(type, profileId, payload);
  }

  /**
   * Send a message and return an array result
   * Convenience method for list operations
   */
  protected async sendArrayMessage<T>(type: string, profileId?: string, payload?: any): Promise<T[]> {
    return this.sendMessage<T[]>(type, profileId, payload);
  }

  /**
   * Send a message and return a nullable result
   * Convenience method for get-by-id operations that might not find a result
   */
  protected async sendNullableMessage<T>(type: string, profileId?: string, payload?: any): Promise<T | null> {
    return this.sendMessage<T | null>(type, profileId, payload);
  }
}
