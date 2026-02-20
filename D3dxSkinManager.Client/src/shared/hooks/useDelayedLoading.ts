import { useState, useRef, useCallback } from 'react';

/**
 * Hook for delayed loading state
 * Only shows loading indicator if operation takes longer than threshold
 *
 * @param delayMs - Milliseconds to wait before showing loading state (default: 50ms)
 * @returns Object with loading state and execute function
 *
 * @example
 * ```tsx
 * const { loading, execute } = useDelayedLoading();
 *
 * const handleSave = async () => {
 *   await execute(async () => {
 *     await saveData();
 *   });
 * };
 *
 * <Button loading={loading} onClick={handleSave}>Save</Button>
 * ```
 */
export function useDelayedLoading(delayMs: number = 50) {
  const [loading, setLoading] = useState(false);
  const loadingTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const isProcessingRef = useRef(false);

  /**
   * Execute an async operation with delayed loading state
   * @param operation - Async function to execute
   * @returns Promise that resolves when operation completes
   */
  const execute = useCallback(
    async <T>(operation: () => Promise<T>): Promise<T> => {
      // Prevent multiple concurrent executions
      if (isProcessingRef.current) {
        throw new Error('Operation already in progress');
      }
      isProcessingRef.current = true;

      // Only show loading spinner if operation takes longer than delay
      loadingTimeoutRef.current = setTimeout(() => {
        setLoading(true);
      }, delayMs);

      try {
        const result = await operation();
        return result;
      } finally {
        // Clear timeout and reset state
        if (loadingTimeoutRef.current) {
          clearTimeout(loadingTimeoutRef.current);
          loadingTimeoutRef.current = null;
        }
        setLoading(false);
        isProcessingRef.current = false;
      }
    },
    [delayMs]
  );

  /**
   * Reset loading state (useful when unmounting or canceling)
   */
  const reset = useCallback(() => {
    if (loadingTimeoutRef.current) {
      clearTimeout(loadingTimeoutRef.current);
      loadingTimeoutRef.current = null;
    }
    setLoading(false);
    isProcessingRef.current = false;
  }, []);

  return {
    loading,
    execute,
    reset,
    isProcessing: isProcessingRef.current,
  };
}
