import { debounce as lodashDebounce, memoize, DebounceSettings, DebouncedFunc } from 'lodash-es';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AnyFunction = (...args: any[]) => any;

export interface MemoizeDebouncedFunction<F extends AnyFunction> {
  (...args: Parameters<F>): ReturnType<F> | undefined;
  flush(): void;
  flush(...args: Parameters<F>): ReturnType<F> | undefined;
  cancel(): void;
  cancel(...args: Parameters<F>): void;
}

/**
 * Combines lodash debounce with memoize to allow for debouncing based on parameters.
 *
 * Unlike regular debounce which shares a single timer for all calls, this creates separate timers
 * for each unique parameter value. This is useful when you want to debounce operations on different
 * entities independently.
 *
 * @param func - The function to debounce
 * @param wait - The number of milliseconds to delay
 * @param options - Lodash debounce options object
 * @param resolver - The function to resolve the cache key (defaults to first argument)
 * @returns Memoized debounced function
 *
 * @example
 * // Each mod gets its own debounce timer
 * const refreshMod = memoizeDebounce(
 *   async (sha: string) => {
 *     await api.refreshMod(sha);
 *   },
 *   20,
 *   {},
 *   (sha) => sha // Use sha as cache key
 * );
 *
 * refreshMod('mod1'); // Starts 20ms timer for mod1
 * refreshMod('mod2'); // Starts SEPARATE 20ms timer for mod2
 * refreshMod('mod1'); // Resets timer for mod1 (typical debounce behavior)
 * // After 20ms: mod1 and mod2 both refresh independently
 *
 * // Can cancel specific timer or all timers
 * refreshMod.cancel('mod1'); // Cancel only mod1
 * refreshMod.cancel(); // Cancel all timers
 */
export function memoizeDebounce<F extends AnyFunction>(
  func: F,
  wait = 0,
  options: DebounceSettings = {},
  resolver?: (...args: Parameters<F>) => unknown
): MemoizeDebouncedFunction<F> {
  const debounceMemo = memoize<(...args: Parameters<F>) => DebouncedFunc<F>>(
    (...args: Parameters<F>) => {
      // Wrap the function to cleanup cache after execution
      const wrappedFunc = async (...funcArgs: Parameters<F>) => {
        try {
          return await func(...funcArgs);
        } finally {
          // Clean up this specific cached entry after execution completes
          const key = resolver ? resolver(...args) : args[0];
          const cache = debounceMemo.cache as { delete: (key: unknown) => boolean };
          cache.delete(key);
        }
      };
      return lodashDebounce(wrappedFunc as F, wait, options);
    },
    resolver
  );

  function wrappedFunction(
    this: MemoizeDebouncedFunction<F>,
    ...args: Parameters<F>
  ): ReturnType<F> | undefined {
    return debounceMemo(...args)(...args);
  }

  function flush(...args: Parameters<F>): ReturnType<F> | undefined {
    // If no args provided, flush all memoized debounced functions
    if (args.length === 0) {
      // Lodash MapCache structure: { __data__: { map: Map(...) } }
      const cache = debounceMemo.cache as { __data__?: { map?: Map<unknown, DebouncedFunc<F>> } };
      const map = cache.__data__?.map;
      if (map instanceof Map) {
        map.forEach((debouncedFn) => debouncedFn.flush());
      }
      return undefined;
    }
    return debounceMemo(...args).flush();
  }

  function cancel(...args: Parameters<F>): void {
    // If no args provided, cancel all memoized debounced functions
    if (args.length === 0) {
      // Lodash MapCache structure: { __data__: { map: Map(...) } }
      const cache = debounceMemo.cache as { __data__?: { map?: Map<unknown, DebouncedFunc<F>> }; clear: () => void };
      const map = cache.__data__?.map;
      if (map instanceof Map) {
        map.forEach((debouncedFn) => debouncedFn.cancel());
      }
      cache.clear();
      return;
    }
    return debounceMemo(...args).cancel();
  }

  wrappedFunction.flush = flush as MemoizeDebouncedFunction<F>['flush'];
  wrappedFunction.cancel = cancel as MemoizeDebouncedFunction<F>['cancel'];

  return wrappedFunction;
}
