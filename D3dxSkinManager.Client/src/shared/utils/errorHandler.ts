import { message } from 'antd';
import { ErrorCodes } from '../constants/errorCodes';
import { ErrorDetails } from '../types/message.types';

/**
 * User-friendly error messages for error codes
 */
const ERROR_MESSAGES: Record<string, string> = {
  [ErrorCodes.MOD_FOLDER_IN_USE]:
    'Cannot load/unload this mod because its folder is currently being used by another program. Please close any programs that might be accessing the mod folder (such as File Explorer, image viewers, or editors) and try again.',

  [ErrorCodes.MOD_ARCHIVE_NOT_FOUND]:
    'The mod archive file was not found. The file may have been deleted or moved.',

  [ErrorCodes.MOD_NOT_FOUND]:
    'The mod was not found in the database.',

  [ErrorCodes.MOD_EXTRACTION_FAILED]:
    'Failed to extract the mod archive. The file may be corrupted or in an unsupported format.',

  [ErrorCodes.MOD_CATEGORY_CONFLICT]:
    'Cannot load this mod because another mod in the same category is already loaded.',

  [ErrorCodes.FILE_IN_USE]:
    'The file is currently being used by another program. Please close any programs accessing the file and try again.',

  [ErrorCodes.FILE_NOT_FOUND]:
    'The file was not found.',

  [ErrorCodes.FILE_ACCESS_DENIED]:
    'Access denied. Please run the application with appropriate permissions.',

  [ErrorCodes.INVALID_OPERATION]:
    'Invalid operation.',

  [ErrorCodes.UNKNOWN_ERROR]:
    'An unknown error occurred.',
};

/**
 * Enhanced error class with error code support
 */
export class ModOperationError extends Error {
  constructor(
    public errorCode: string,
    message: string,
    public data?: unknown
  ) {
    super(message);
    this.name = 'ModOperationError';
  }
}

/**
 * Handle error from operation
 * Displays user-friendly error message and returns structured error
 */
export function handleError(error: unknown): ModOperationError {
  // Check if it's a standard Error with errorDetails
  if (error instanceof Error) {
    const errorWithDetails = error as Error & { errorDetails?: ErrorDetails };

    if (errorWithDetails.errorDetails?.errorCode) {
      const { errorCode, data } = errorWithDetails.errorDetails;
      const userMessage = ERROR_MESSAGES[errorCode] || errorWithDetails.message || 'An error occurred';

      // Show user-friendly message
      message.error(userMessage, 5); // Show for 5 seconds

      return new ModOperationError(errorCode, userMessage, data);
    }
  }

  // Fallback for unknown errors
  const errorMessage = error instanceof Error ? error.message : 'An unknown error occurred';
  message.error(errorMessage, 3);

  return new ModOperationError(ErrorCodes.UNKNOWN_ERROR, errorMessage);
}

/**
 * Get user-friendly error message for an error code
 */
export function getErrorMessage(errorCode: string, fallbackMessage?: string): string {
  return ERROR_MESSAGES[errorCode] || fallbackMessage || 'An error occurred';
}

/**
 * Check if an error is a specific error code
 */
export function isErrorCode(error: unknown, errorCode: string): boolean {
  if (error instanceof ModOperationError) {
    return error.errorCode === errorCode;
  }

  if (error instanceof Error) {
    const errorWithDetails = error as Error & { errorDetails?: ErrorDetails };
    return errorWithDetails.errorDetails?.errorCode === errorCode;
  }

  return false;
}
