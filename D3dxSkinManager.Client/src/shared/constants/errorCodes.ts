/**
 * Error codes for backend operations
 * These correspond to ErrorCodes.cs in the backend
 */
export const ErrorCodes = {
  // Mod Operation Errors (MOD_*)
  MOD_FOLDER_IN_USE: 'MOD_FOLDER_IN_USE',
  MOD_ARCHIVE_NOT_FOUND: 'MOD_ARCHIVE_NOT_FOUND',
  MOD_NOT_FOUND: 'MOD_NOT_FOUND',
  MOD_EXTRACTION_FAILED: 'MOD_EXTRACTION_FAILED',
  MOD_CATEGORY_CONFLICT: 'MOD_CATEGORY_CONFLICT',

  // File Operation Errors (FILE_*)
  FILE_IN_USE: 'FILE_IN_USE',
  FILE_NOT_FOUND: 'FILE_NOT_FOUND',
  FILE_ACCESS_DENIED: 'FILE_ACCESS_DENIED',

  // Generic Errors
  UNKNOWN_ERROR: 'UNKNOWN_ERROR',
  INVALID_OPERATION: 'INVALID_OPERATION',
} as const;

export type ErrorCode = typeof ErrorCodes[keyof typeof ErrorCodes];
