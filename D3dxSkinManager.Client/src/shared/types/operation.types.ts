/**
 * Operation notification types and interfaces for backend → frontend push notifications
 * Type names match backend C# models exactly
 */

export type OperationStatus = 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export type OperationNotificationType =
  | 'OperationStarted'
  | 'ProgressUpdate'
  | 'OperationCompleted'
  | 'OperationFailed'
  | 'OperationCancelled';

/**
 * Matches backend OperationProgress model
 */
export interface OperationProgress {
  operationId: string;
  operationName: string;
  status: OperationStatus;
  percentComplete: number;
  currentStep?: string;
  startedAt: Date;
  completedAt?: Date;
  errorMessage?: string;
  metadata?: unknown;
}

/**
 * Matches backend OperationNotification model
 */
export interface OperationNotification {
  type: OperationNotificationType;
  operation: OperationProgress;
  timestamp: Date;
}

export interface OperationNotificationMessage {
  type: 'OPERATION_NOTIFICATION';
  notification: {
    type: string;
    operation: {
      operationId: string;
      operationName: string;
      status: string;
      percentComplete: number;
      currentStep?: string;
      startedAt: string;
      completedAt?: string;
      errorMessage?: string;
      metadata?: unknown;
    };
    timestamp: string;
  };
}
