import { useCallback, useRef } from 'react';

/**
 * Hook for optimistic UI updates with automatic verification
 *
 * **When to use this hook:**
 * - Operations with unpredictable delays (slow backend, network issues)
 * - Need to verify complex state changes (multiple interdependent fields)
 * - Want to detect and handle mismatches automatically
 *
 * **When to use `useDelayedLoading` instead:**
 * - Simple loading operations that just need delayed spinner
 * - Single data source refresh (just show/hide loading state)
 * - Fast operations (<100ms typically) where flicker is the main concern
 *
 * **Migration to `useDelayedLoading`:**
 * 1. Wrap your loading operation with `execute()`
 * 2. Sync the hook's `loading` state with your reducer via `useEffect`
 * 3. Remove automatic `loading: false` from your reducer actions
 * 4. Call simple refresh instead of verification
 *
 * See `useClassificationData.ts` or `useModData.ts` for examples of `useDelayedLoading`.
 */

/**
 * Deep equality check using JSON.stringify
 * Simple but effective for most cases
 */
function deepEqual(a: any, b: any): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/**
 * Find differences between two objects
 * Returns a summary of mismatched fields
 */
function findDifferences(expected: any, actual: any, path: string = ''): string[] {
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
    const allKeys = new Set([...Object.keys(expected), ...Object.keys(actual)]);

    allKeys.forEach(key => {
      const newPath = path ? `${path}.${key}` : key;

      if (!(key in expected)) {
        differences.push(`${newPath}: unexpected field in actual (value: ${JSON.stringify(actual[key])})`);
      } else if (!(key in actual)) {
        differences.push(`${newPath}: missing in actual (expected: ${JSON.stringify(expected[key])})`);
      } else if (typeof expected[key] === 'object' && typeof actual[key] === 'object') {
        differences.push(...findDifferences(expected[key], actual[key], newPath));
      } else if (expected[key] !== actual[key]) {
        differences.push(`${newPath}: expected ${JSON.stringify(expected[key])}, got ${JSON.stringify(actual[key])}`);
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

  const verificationTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  /**
   * Verify the expected result matches backend after a delay
   * @param expectedResult - What we expect the backend state to be
   * @param fetchParams - Optional parameters to pass to fetchFn
   */
  const verify = useCallback(
    (expectedResult: TData, fetchParams: TFetchParams) => {
      // Clear any pending verification
      if (verificationTimeoutRef.current) {
        clearTimeout(verificationTimeoutRef.current);
      }

      // Schedule verification after delay
      verificationTimeoutRef.current = setTimeout(async () => {
        try {
          if (enableLogging) {
            console.log('[OptimisticUpdate] Starting verification...');
          }

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
            if (enableLogging) {
              const differences = findDifferences(normalizedExpected, normalizedBackend);
              console.warn('[OptimisticUpdate] ❌ Mismatch detected, calling onMismatch');
              console.warn(`Found ${differences.length} difference(s):`);
              differences.forEach(diff => console.warn(`  - ${diff}`));
            }
            onMismatch();
          } else {
            if (enableLogging) {
              console.log('[OptimisticUpdate] ✓ Verification passed - no refresh needed');
            }
          }
        } catch (error) {
          if (enableLogging) {
            console.error('[OptimisticUpdate] Verification failed, calling onMismatch as fallback', error);
          }
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
    if (verificationTimeoutRef.current) {
      clearTimeout(verificationTimeoutRef.current);
      verificationTimeoutRef.current = null;
    }
  }, []);

  return { verify, cancel };
}
