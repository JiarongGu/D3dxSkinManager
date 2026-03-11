/**
 * Base class for module-specific services
 * Provides type-safe IPC communication with a specific backend module
 */

import { bridgeService } from './bridgeService';
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
   * Send a type-safe message with compile-time payload validation
   * @param type - The action type (must be a key of TRequests)
   * @param payload - Payload that matches TRequests[type]
   * @returns Promise with typed response data
   */
  protected async sendTypedMessage<TRequests, TResponse, TType extends keyof TRequests = keyof TRequests>(
    type: TType,
    profileId?: string,
    payload?: TRequests[TType]
  ): Promise<TResponse> {
    return bridgeService.sendMessage<TResponse, TRequests[TType]>({
      module: this.moduleName,
      type: type as string,
      profileId,
      payload
    });
  }

  /**
   * Send a type-safe message and return a boolean result
   * Convenience method for operations that return success/failure
   */
  protected async sendTypedBoolean<TRequests, TType extends keyof TRequests = keyof TRequests>(
    type: TType,
    profileId?: string,
    payload?: TRequests[TType]
  ): Promise<boolean> {
    return this.sendTypedMessage<TRequests, boolean, TType>(type, profileId, payload);
  }

  /**
   * Send a type-safe message and return an array result
   * Convenience method for list operations
   */
  protected async sendTypedArray<TRequests, TElement, TType extends keyof TRequests = keyof TRequests>(
    type: TType,
    profileId?: string,
    payload?: TRequests[TType]
  ): Promise<TElement[]> {
    return this.sendTypedMessage<TRequests, TElement[], TType>(type, profileId, payload);
  }

  /**
   * Send a type-safe message and return an optional result
   * Convenience method for get-by-id operations that might not find a result
   */
  protected async sendTypedOptional<TRequests, TElement, TType extends keyof TRequests = keyof TRequests>(
    type: TType,
    profileId?: string,
    payload?: TRequests[TType]
  ): Promise<TElement | undefined> {
    return this.sendTypedMessage<TRequests, TElement | undefined, TType>(type, profileId, payload);
  }

  // ============= Legacy untyped methods (for backwards compatibility) =============

  /**
   * Send an untyped message to this module's backend facade
   * @deprecated Use sendTypedMessage instead for compile-time type safety
   */
  protected async sendMessage<T, TPayload = unknown>(type: string, profileId?: string, payload?: TPayload): Promise<T> {
    return bridgeService.sendMessage<T, TPayload>({ module: this.moduleName, type, profileId, payload });
  }

  /**
   * Send an untyped message and return a boolean result
   * @deprecated Use sendTypedBoolean instead for compile-time type safety
   */
  protected async sendBooleanMessage<TPayload = unknown>(type: string, profileId?: string, payload?: TPayload): Promise<boolean> {
    return bridgeService.sendMessage<boolean, TPayload>({ module: this.moduleName, type, profileId, payload });
  }

  /**
   * Send an untyped message and return an array result
   * @deprecated Use sendTypedArray instead for compile-time type safety
   */
  protected async sendArrayMessage<T, TPayload = unknown>(type: string, profileId?: string, payload?: TPayload): Promise<T[]> {
    return bridgeService.sendMessage<T[], TPayload>({ module: this.moduleName, type, profileId, payload });
  }

  /**
   * Send an untyped message and return an optional result
   * @deprecated Use sendTypedOptional instead for compile-time type safety
   */
  protected async sendOptionalMessage<T, TPayload = unknown>(type: string, profileId?: string, payload?: TPayload): Promise<T | undefined> {
    return bridgeService.sendMessage<T | undefined, TPayload>({ module: this.moduleName, type, profileId, payload });
  }
}
