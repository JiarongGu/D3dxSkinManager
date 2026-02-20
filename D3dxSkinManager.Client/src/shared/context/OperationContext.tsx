import React, { createContext, useContext, useReducer, useEffect, useCallback, ReactNode } from 'react';
import { photinoService } from '../services/photinoService';
import { OperationProgress, OperationNotificationType } from '../types/operation.types';

/**
 * Operation Context for managing background operations
 * Receives push notifications from backend and maintains operation state
 */

interface OperationState {
  activeOperations: OperationProgress[];
  completedOperations: OperationProgress[]; // Keep last 50 completed operations
  failedOperations: OperationProgress[]; // Keep last 50 failed operations
  currentOperation: OperationProgress | null; // The most recent active operation (for status bar)
}

type OperationAction =
  | { type: 'OPERATION_STARTED'; payload: OperationProgress }
  | { type: 'PROGRESS_UPDATE'; payload: OperationProgress }
  | { type: 'OPERATION_COMPLETED'; payload: OperationProgress }
  | { type: 'OPERATION_FAILED'; payload: OperationProgress }
  | { type: 'OPERATION_CANCELLED'; payload: OperationProgress }
  | { type: 'CLEAR_COMPLETED' }
  | { type: 'CLEAR_FAILED' };

interface OperationContextValue {
  state: OperationState;
  actions: {
    clearCompleted: () => void;
    clearFailed: () => void;
  };
}

const OperationContext = createContext<OperationContextValue | undefined>(undefined);

const MAX_HISTORY = 50; // Keep last 50 completed/failed operations

function operationReducer(state: OperationState, action: OperationAction): OperationState {
  switch (action.type) {
    case 'OPERATION_STARTED': {
      return {
        ...state,
        activeOperations: [...state.activeOperations, action.payload],
        currentOperation: action.payload,
      };
    }

    case 'PROGRESS_UPDATE': {
      return {
        ...state,
        activeOperations: state.activeOperations.map(op =>
          op.operationId === action.payload.operationId ? action.payload : op
        ),
        currentOperation: state.currentOperation?.operationId === action.payload.operationId
          ? action.payload
          : state.currentOperation,
      };
    }

    case 'OPERATION_COMPLETED': {
      const remainingActive = state.activeOperations.filter(op => op.operationId !== action.payload.operationId);
      const newCompleted = [action.payload, ...state.completedOperations].slice(0, MAX_HISTORY);

      return {
        ...state,
        activeOperations: remainingActive,
        completedOperations: newCompleted,
        currentOperation: remainingActive.length > 0 ? remainingActive[remainingActive.length - 1] : null,
      };
    }

    case 'OPERATION_FAILED': {
      const remainingActive = state.activeOperations.filter(op => op.operationId !== action.payload.operationId);
      const newFailed = [action.payload, ...state.failedOperations].slice(0, MAX_HISTORY);

      return {
        ...state,
        activeOperations: remainingActive,
        failedOperations: newFailed,
        currentOperation: remainingActive.length > 0 ? remainingActive[remainingActive.length - 1] : null,
      };
    }

    case 'OPERATION_CANCELLED': {
      const remainingActive = state.activeOperations.filter(op => op.operationId !== action.payload.operationId);

      return {
        ...state,
        activeOperations: remainingActive,
        currentOperation: remainingActive.length > 0 ? remainingActive[remainingActive.length - 1] : null,
      };
    }

    case 'CLEAR_COMPLETED':
      return {
        ...state,
        completedOperations: [],
      };

    case 'CLEAR_FAILED':
      return {
        ...state,
        failedOperations: [],
      };

    default:
      return state;
  }
}

const initialState: OperationState = {
  activeOperations: [],
  completedOperations: [],
  failedOperations: [],
  currentOperation: null,
};

interface OperationProviderProps {
  children: ReactNode;
}

export const OperationProvider: React.FC<OperationProviderProps> = ({ children }) => {
  const [state, dispatch] = useReducer(operationReducer, initialState);

  // Subscribe to operation notifications from backend
  useEffect(() => {
    const unsubscribe = photinoService.subscribeToOperationNotifications((notification) => {
      const notificationType = notification.type as OperationNotificationType;

      // Convert the operation object from backend (dates are strings) to OperationProgress (dates are Date objects)
      const operation: OperationProgress = {
        ...notification.operation,
        startedAt: new Date(notification.operation.startedAt),
        completedAt: notification.operation.completedAt ? new Date(notification.operation.completedAt) : undefined,
      } as OperationProgress;

      console.log('[OperationContext] Received notification:', notificationType, operation);

      switch (notificationType) {
        case 'OperationStarted':
          dispatch({ type: 'OPERATION_STARTED', payload: operation });
          break;
        case 'ProgressUpdate':
          dispatch({ type: 'PROGRESS_UPDATE', payload: operation });
          break;
        case 'OperationCompleted':
          dispatch({ type: 'OPERATION_COMPLETED', payload: operation });
          break;
        case 'OperationFailed':
          dispatch({ type: 'OPERATION_FAILED', payload: operation });
          break;
        case 'OperationCancelled':
          dispatch({ type: 'OPERATION_CANCELLED', payload: operation });
          break;
      }
    });

    return unsubscribe;
  }, []);

  const clearCompleted = useCallback(() => {
    dispatch({ type: 'CLEAR_COMPLETED' });
  }, []);

  const clearFailed = useCallback(() => {
    dispatch({ type: 'CLEAR_FAILED' });
  }, []);

  const value: OperationContextValue = {
    state,
    actions: {
      clearCompleted,
      clearFailed,
    },
  };

  return <OperationContext.Provider value={value}>{children}</OperationContext.Provider>;
};

export const useOperation = (): OperationContextValue => {
  const context = useContext(OperationContext);
  if (!context) {
    throw new Error('useOperation must be used within an OperationProvider');
  }
  return context;
};
