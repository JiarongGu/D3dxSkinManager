/**
 * Module names for routing IPC messages
 */
export type ModuleName =
  | 'APP' // Application-level messages (ping, version, webview lifecycle)
  | 'MOD'
  | 'CATEGORY'
  | 'LAUNCH'
  | 'WAREHOUSE'
  | 'TOOL'
  | 'PLUGIN'
  | 'SETTING'
  | 'SYSTEM'
  | 'MIGRATION'
  | 'PROFILE'
  | 'DROP_ZONE' // WinForms drop zone overlay management
  | 'WORKFLOW'; // Workflow management (replaces TASK_QUEUE)

/**
 * Message types for module-based routing
 */
export type MessageType = string;

/**
 * IPC message sent to backend (module-based routing)
 * @template TPayload - Type of the payload data (defaults to unknown for type safety)
 */
export interface BridgeMessage<TPayload = unknown> {
  id: string;
  module: ModuleName;
  type: MessageType;
  profileId?: string;
  payload?: TPayload;
}

/**
 * Error details from backend ModException
 */
export interface ErrorDetails {
  errorCode: string;
  data?: unknown;
}

/**
 * IPC response from backend
 * @template TData - Type of the response data (defaults to unknown for type safety)
 */
export interface BridgeResponse<TData = unknown> {
  id: string;
  success: boolean;
  data?: TData;
  error?: string;
  errorDetails?: ErrorDetails;
}
