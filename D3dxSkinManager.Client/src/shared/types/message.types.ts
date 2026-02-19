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
 */
export interface PhotinoMessage {
  id: string;
  module: ModuleName;
  type: MessageType;
  profileId?: string;
  payload?: any;
}

/**
 * IPC response from backend
 */
export interface PhotinoResponse {
  id: string;
  success: boolean;
  data?: any;
  error?: string;
}
