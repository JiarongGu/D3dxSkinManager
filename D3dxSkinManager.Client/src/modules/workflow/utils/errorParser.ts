/**
 * Workflow Error Parser
 * Parses structured error messages from the backend for i18n translation
 */

/**
 * Structured error format from backend
 * Backend uses camelCase naming policy, so properties are lowercase
 */
interface WorkflowError {
  code: string;
  parameters?: Record<string, string>;
}

/**
 * Parsed error with fallback for legacy errors
 */
export interface ParsedWorkflowError {
  code: string;
  parameters: Record<string, string>;
  fallbackMessage: string;
}

/**
 * Parse workflow error message
 *
 * Backend sends errors in two formats:
 * 1. New format (JSON): { "Code": "WORKFLOW_DUPLICATE_MOD", "Parameters": { "name": "MyMod" } }
 * 2. Legacy format (plain text): "This mod already exists..."
 *
 * This parser handles both formats gracefully
 *
 * @param errorMessage Error message from workflow.errorMessage
 * @returns Parsed error with code, parameters, and fallback message
 */
export function parseWorkflowError(errorMessage: string | undefined | null): ParsedWorkflowError {
  // Handle empty/null error messages
  if (!errorMessage) {
    return {
      code: 'WORKFLOW_UNKNOWN_ERROR',
      parameters: { message: 'Unknown error' },
      fallbackMessage: 'Unknown error'
    };
  }

  try {
    // Try to parse as JSON (new structured format)
    const parsed = JSON.parse(errorMessage) as WorkflowError;

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
  // Wrap it in UNKNOWN_ERROR code so frontend can still display it
  return {
    code: 'WORKFLOW_UNKNOWN_ERROR',
    parameters: { message: errorMessage },
    fallbackMessage: errorMessage
  };
}

/**
 * Get i18n key for workflow error code
 * @param code Error code (e.g., "WORKFLOW_DUPLICATE_MOD")
 * @returns i18n key (e.g., "workflow.errors.WORKFLOW_DUPLICATE_MOD")
 */
export function getErrorI18nKey(code: string): string {
  return `workflow.errors.${code}`;
}
