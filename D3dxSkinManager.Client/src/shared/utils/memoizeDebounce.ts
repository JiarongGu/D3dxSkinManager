import { debounce as lodashDebounce, DebounceSettings, DebouncedFunc } from 'lodash-es';

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
 * Creates a debounced function where each unique parameter gets its own timer.
 *
 * Unlike regular debounce (single timer, last call wins), this creates independent timers
 * per key, preserving all parameters. Auto-cleans cache after execution.
 *
 * @param func - Function to debounce
 * @param resolver - Extract cache key from arguments (e.g., `(id) => id`)
 * @param wait - Delay in milliseconds (default: 0)
 * @param options - Debounce options (leading, trailing, maxWait)
 *
 * @example
 * const refresh = memoizeDebounce(
 *   async (id: string) => api.refresh(id),
 *   (id) => id,
 *   20
 * );
 * refresh('mod1'); refresh('mod2'); // Both get independent 20ms timers
 * refresh.cancel('mod1');           // Cancel specific timer
 * refresh.cancel();                 // Cancel all timers
 */
export function memoizeDebounce<F extends AnyFunction>(
  func: F,
  resolver: (...args: Parameters<F>) => string | number | symbol,
  wait = 0,
  options: DebounceSettings = {},
): MemoizeDebouncedFunction<F> {
  const debouncedCache = new Map<string | number | symbol, DebouncedFunc<F>>();

  function wrappedFunction(
    this: MemoizeDebouncedFunction<F>,
    ...args: Parameters<F>
  ): ReturnType<F> | undefined {
    const key = resolver(...args);
    let debouncedFn = debouncedCache.get(key);

    if (!debouncedFn) {
      // Wrap to auto-cleanup cache after execution
      const wrappedFunc = async (...funcArgs: Parameters<F>) => {
        try {
          return await func(...funcArgs);
        } finally {
          debouncedCache.delete(key);
        }
      };

      debouncedFn = lodashDebounce(wrappedFunc as F, wait, options);
      debouncedCache.set(key, debouncedFn);
    }

    return debouncedFn(...args);
  }

  function flush(...args: Parameters<F>): ReturnType<F> | undefined {
    if (args.length === 0) {
      debouncedCache.forEach((debouncedFn) => debouncedFn.flush());
      return undefined;
    }

    const key = resolver(...args);
    return debouncedCache.get(key)?.flush();
  }

  function cancel(...args: Parameters<F>): void {
    if (args.length === 0) {
      debouncedCache.forEach((debouncedFn) => debouncedFn.cancel());
      debouncedCache.clear();
      return;
    }

    const key = resolver(...args);
    const debouncedFn = debouncedCache.get(key);
    if (debouncedFn) {
      debouncedFn.cancel();
      debouncedCache.delete(key);
    }
  }

  wrappedFunction.flush = flush as MemoizeDebouncedFunction<F>['flush'];
  wrappedFunction.cancel = cancel as MemoizeDebouncedFunction<F>['cancel'];

  return wrappedFunction;
}
