/**
 * Utility for delayed loading state in operations
 * Only shows loading indicator if operation takes longer than threshold
 *
 * This works with any setter function (Zustand store, React setState, etc.)
 *
 * @param operation - Async operation to execute
 * @param setLoading - Function to set loading state (e.g., store.setModsLoading)
 * @param delayMs - Delay before showing loading (default: 100ms)
 * @returns Promise that resolves with operation result
 *
 * @example
 * ```typescript
 * const { setModsLoading } = useModsStore.getState();
 *
 * await executeWithDelayedLoading(
 *   async () => {
 *     await modService.updateMod(id, data);
 *   },
 *   setModsLoading,
 *   100
 * );
 * ```
 */
export async function executeWithDelayedLoading<T>(
  operation: () => Promise<T>,
  setLoading: (loading: boolean) => void,
  delayMs: number = 100
): Promise<T> {
  let loadingTimeout: NodeJS.Timeout | undefined;
  let loadingWasSet = false;

  try {
    // Set a timeout to show loading only if operation takes longer than delay
    loadingTimeout = setTimeout(() => {
      setLoading(true);
      loadingWasSet = true;
    }, delayMs);

    // Execute the operation
    const result = await operation();

    // Clear the timeout if operation finished before delay
    if (loadingTimeout !== undefined) {
      clearTimeout(loadingTimeout);
    }

    // Reset loading if it was set
    if (loadingWasSet) {
      setLoading(false);
    }

    return result;
  } catch (error) {
    // Clear timeout and reset loading on error
    if (loadingTimeout !== undefined) {
      clearTimeout(loadingTimeout);
    }
    if (loadingWasSet) {
      setLoading(false);
    }
    throw error;
  }
}
