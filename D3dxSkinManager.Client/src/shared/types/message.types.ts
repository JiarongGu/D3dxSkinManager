/**
 * Module names for routing IPC messages
 */
export type ModuleName =
  | 'MOD'
  | 'LAUNCH'
  | 'WAREHOUSE'
  | 'TOOLS'
  | 'PLUGINS'
  | 'SETTINGS'
  | 'MIGRATION'
  | 'PROFILE';

/**
 * Message types for module-based routing
 */
export type MessageType = string;

/**
 * IPC message sent to backend (new format with module field)
 * @template TPayload - Type of the payload data (defaults to unknown for type safety)
 */
export interface PhotinoMessage<TPayload = unknown> {
  id: string;
  module: ModuleName;
  type: MessageType;
  profileId?: string;
  payload?: TPayload;
}

/**
 * IPC response from backend
 * @template TData - Type of the response data (defaults to unknown for type safety)
 */
export interface PhotinoResponse<TData = unknown> {
  id: string;
  success: boolean;
  data?: TData;
  error?: string;
}
