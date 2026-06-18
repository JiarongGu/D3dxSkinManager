import { ErrorCodes } from '../constants/errorCodes';
import { ErrorDetails } from '../types/message.types';
import { notification } from './notification';
import i18n from '../services/i18n';

/**
 * Structured error format from backend
 * Backend uses camelCase naming policy, so properties are lowercase
 */
interface StructuredError {
  code: string;
  parameters?: Record<string, string>;
}

/**
 * Parsed error with fallback for legacy errors
 */
export interface ParsedError {
  code: string;
  parameters: Record<string, string>;
  fallbackMessage: string;
}

/**
 * Parse error message from backend
 *
 * Backend sends errors in two formats:
 * 1. New format (JSON): { "Code": "MOD_DELETE_FAILED", "Parameters": { "name": "MyMod" } }
 * 2. Legacy format (plain text): "Failed to delete mod..."
 *
 * This parser handles both formats gracefully
 *
 * @param errorMessage Error message from backend
 * @param defaultErrorCode Default error code if parsing fails (default: "UNKNOWN_ERROR")
 * @returns Parsed error with code, parameters, and fallback message
 */
export function parseError(
  errorMessage: string | undefined | null,
  defaultErrorCode: string = 'UNKNOWN_ERROR'
): ParsedError {
  // Handle empty/null error messages
  if (!errorMessage) {
    return {
      code: defaultErrorCode,
      parameters: { message: 'Unknown error' },
      fallbackMessage: 'Unknown error'
    };
  }

  try {
    // Try to parse as JSON (new structured format)
    const parsed = JSON.parse(errorMessage) as StructuredError;

    if (parsed.code) {
      return {
        code: parsed.code,
        parameters: parsed.parameters || {},
        fallbackMessage: errorMessage
      };
    }
  } catch {
    // Not JSON, treat as legacy plain text error
  }

  // Legacy format: plain text error message
  // Wrap it in default error code so frontend can still display it
  return {
    code: defaultErrorCode,
    parameters: { message: errorMessage },
    fallbackMessage: errorMessage
  };
}

/**
 * Get i18n key for error code
 * Uses unified pattern: errors.{errorCode}
 * Works for all error codes: MOD_*, WORKFLOW_*, etc.
 * @param code Error code (e.g., "MOD_DELETE_FAILED", "WORKFLOW_MI_DUPLICATE_MOD")
 * @returns i18n key (e.g., "errors.MOD_DELETE_FAILED")
 */
export function getErrorI18nKey(code: string): string {
  return `errors.${code}`;
}

/**
 * Enhanced error class with error code support
 * Generic error class for all operations (mod, workflow, file, etc.)
 * Matches backend OperationException structure: code + parameters
 */
export class OperationError extends Error {
  constructor(
    public code: string,
    message: string,
    public parameters?: Record<string, string>
  ) {
    super(message);
    this.name = 'OperationError';
  }
}

/**
 * Handle error from operation
 * Displays user-friendly error message and returns structured error
 */
export function handleError(error: unknown): OperationError {
  // Check if it's a standard Error with errorDetails
  if (error instanceof Error) {
    const errorWithDetails = error as Error & { errorDetails?: ErrorDetails };

    if (errorWithDetails.errorDetails?.code) {
      const { code, parameters } = errorWithDetails.errorDetails;

      // Get i18n message
      const i18nKey = getErrorI18nKey(code);
      let userMessage: string;
      if (i18n.exists(i18nKey)) {
        userMessage = i18n.t(i18nKey, parameters || {});
      } else {
        // Fallback: format code + parameters as readable text instead of showing raw JSON
        const paramStr = parameters
          ? Object.entries(parameters).map(([k, v]) => `${k}: ${v}`).join(', ')
          : '';
        userMessage = paramStr ? `${code} (${paramStr})` : code;
      }

      // Show user-friendly message
      notification.error(userMessage, 5); // Show for 5 seconds

      return new OperationError(code, userMessage, parameters);
    }
  }

  // Fallback: try to parse error message as structured JSON
  const rawMessage = error instanceof Error ? error.message : 'An unknown error occurred';
  const parsed = parseError(rawMessage);

  if (parsed.code !== 'UNKNOWN_ERROR') {
    const i18nKey = getErrorI18nKey(parsed.code);
    let userMessage: string;
    if (i18n.exists(i18nKey)) {
      userMessage = i18n.t(i18nKey, parsed.parameters || {});
    } else {
      const paramStr = Object.entries(parsed.parameters)
        .filter(([k]) => k !== 'message')
        .map(([k, v]) => `${k}: ${v}`)
        .join(', ');
      userMessage = paramStr ? `${parsed.code} (${paramStr})` : parsed.code;
    }
    notification.error(userMessage, 5);
    return new OperationError(parsed.code, userMessage, parsed.parameters);
  }

  notification.error(rawMessage, 3);
  return new OperationError(ErrorCodes.UNKNOWN_ERROR, rawMessage);
}

/**
 * Translate a structured error message (from workflow errorMessage field, etc.)
 * Parses JSON error string and returns translated message with parameters
 *
 * @param errorMessage JSON error string from backend (e.g., workflow.errorMessage)
 * @param defaultErrorCode Default error code if parsing fails
 * @returns Translated and interpolated error message
 *
 * @example
 * const message = translateErrorMessage(workflow.errorMessage);
 * // Returns: "This mod already exists in your library: MyMod"
 */
export function translateErrorMessage(
  errorMessage: string | undefined | null,
  defaultErrorCode: string = 'UNKNOWN_ERROR'
): string {
  const parsed = parseError(errorMessage, defaultErrorCode);
  const i18nKey = getErrorI18nKey(parsed.code);

  // Try to translate with parameters, fallback to original message if translation missing
  if (i18n.exists(i18nKey)) {
    return i18n.t(i18nKey, parsed.parameters) as string;
  }

  return parsed.fallbackMessage;
}
