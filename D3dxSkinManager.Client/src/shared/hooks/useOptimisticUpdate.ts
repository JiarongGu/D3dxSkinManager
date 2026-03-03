import { useCallback, useRef } from 'react';

/**
 * Hook for optimistic UI updates with automatic verification.
 * Use for operations with unpredictable delays requiring state verification.
 * For simple loading operations, use `useDelayedLoading` instead.
 *
 * For migration guide and examples, see docs/ai-assistant/GUIDELINES.md
 */

/**
 * Deep equality check using JSON.stringify
 */
function deepEqual(a: unknown, b: unknown): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/**
 * Find differences between two objects for debugging
 */
function findDifferences(expected: unknown, actual: unknown, path: string = ''): string[] {
  const differences: string[] = [];

  // Handle arrays
  if (Array.isArray(expected) && Array.isArray(actual)) {
    if (expected.length !== actual.length) {
      differences.push(`${path}.length: expected ${expected.length}, got ${actual.length}`);
    }

    const minLength = Math.min(expected.length, actual.length);
    for (let i = 0; i < minLength; i++) {
      const itemPath = path ? `${path}[${i}]` : `[${i}]`;
      differences.push(...findDifferences(expected[i], actual[i], itemPath));
    }
  }
  // Handle objects
  else if (expected !== null && actual !== null && typeof expected === 'object' && typeof actual === 'object') {
    const expectedObj = expected as Record<string, unknown>;
    const actualObj = actual as Record<string, unknown>;
    const allKeys = new Set([...Object.keys(expectedObj), ...Object.keys(actualObj)]);

    allKeys.forEach(key => {
      const newPath = path ? `${path}.${key}` : key;

      if (!(key in expectedObj)) {
        differences.push(`${newPath}: unexpected field in actual (value: ${JSON.stringify(actualObj[key])})`);
      } else if (!(key in actualObj)) {
        differences.push(`${newPath}: missing in actual (expected: ${JSON.stringify(expectedObj[key])})`);
      } else if (typeof expectedObj[key] === 'object' && typeof actualObj[key] === 'object') {
        differences.push(...findDifferences(expectedObj[key], actualObj[key], newPath));
      } else if (expectedObj[key] !== actualObj[key]) {
        differences.push(`${newPath}: expected ${JSON.stringify(expectedObj[key])}, got ${JSON.stringify(actualObj[key])}`);
      }
    });
  }
  // Handle primitives
  else if (expected !== actual) {
    differences.push(`${path}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }

  return differences;
}

/**
 * Configuration for optimistic updates
 */
export interface OptimisticUpdateConfig<TData = any> {
  /** Delay in ms before verifying backend state (default: 50ms) */
  verificationDelay?: number;
  /** Whether to log verification results to console (default: true in dev) */
  enableLogging?: boolean;
  /** Optional function to normalize data before comparison (e.g., sort by ID) */
  normalizeForComparison?: (data: TData) => TData;
}

/**
 * Custom hook for optimistic UI updates with automatic verification
 *
 * @example
 * ```tsx
 * const { verify } = useOptimisticUpdate({
 *   fetchFn: async (profileId) => await modService.getAllMods(profileId),
 *   onMismatch: () => refreshMods(),
 * });
 *
 * // After optimistic update
 * verify(expectedMods, profileId);
 * ```
 */
export function useOptimisticUpdate<TData = any, TFetchParams = void>(
  config: OptimisticUpdateConfig<TData> & {
    /** Function to fetch backend state for verification */
    fetchFn: (params: TFetchParams) => Promise<TData>;
    /** Callback when verification detects a mismatch */
    onMismatch: () => void;
  }
) {
  const {
    fetchFn,
    onMismatch,
    verificationDelay = 50,
    enableLogging = process.env.NODE_ENV === 'development',
    normalizeForComparison,
  } = config;

  const verificationTimeoutRef = useRef<NodeJS.Timeout | undefined>(undefined);

  /**
   * Verify the expected result matches backend after a delay
   * @param expectedResult - What we expect the backend state to be
   * @param fetchParams - Optional parameters to pass to fetchFn
   */
  const verify = useCallback(
    (expectedResult: TData, fetchParams: TFetchParams) => {
      // Clear any pending verification
      if (verificationTimeoutRef.current !== undefined) {
        clearTimeout(verificationTimeoutRef.current);
      }

      // Schedule verification after delay
      verificationTimeoutRef.current = setTimeout(async () => {
        try {
          // Fetch backend state
          const backendResult = await fetchFn(fetchParams);

          // Apply normalization if provided (e.g., sort by ID for tree comparisons)
          const normalizedExpected = normalizeForComparison
            ? normalizeForComparison(expectedResult)
            : expectedResult;
          const normalizedBackend = normalizeForComparison
            ? normalizeForComparison(backendResult as TData)
            : backendResult;

          // Deep compare
          const isMatch = deepEqual(normalizedExpected, normalizedBackend);

          if (!isMatch) {
            onMismatch();
          }
        } catch (error) {
          // Error handled by error handler
          onMismatch();
        }
      }, verificationDelay);
    },
    [fetchFn, onMismatch, verificationDelay, enableLogging]
  );

  /**
   * Cancel any pending verification
   */
  const cancel = useCallback(() => {
    if (verificationTimeoutRef.current !== undefined) {
      clearTimeout(verificationTimeoutRef.current);
      verificationTimeoutRef.current = undefined;
    }
  }, []);

  return { verify, cancel };
}
