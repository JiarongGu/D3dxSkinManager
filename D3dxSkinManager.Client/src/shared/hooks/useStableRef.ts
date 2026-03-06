import { useRef, useEffect } from 'react';

/**
 * Creates stable references to values that persist across renders.
 * Solves React closure issues where callbacks capture stale values.
 *
 * @example
 * ```tsx
 * const itemsRef = useStableRef(items);
 * const handleClick = useCallback(() => {
 *   logger.info(itemsRef.current.length); // Always current
 * }, []); // No deps needed
 * ```
 *
 * For architecture details, see docs/ai-assistant/GUIDELINES.md
 *
 * @param values - One or more values to store in refs (supports up to 12 values)
 * @returns Single ref or array of refs
 */
export function useStableRef<T1>(value1: T1): { current: T1 };
export function useStableRef<T1, T2>(
  value1: T1,
  value2: T2
): [{ current: T1 }, { current: T2 }];
export function useStableRef<T1, T2, T3>(
  value1: T1,
  value2: T2,
  value3: T3
): [{ current: T1 }, { current: T2 }, { current: T3 }];
export function useStableRef<T1, T2, T3, T4>(
  value1: T1,
  value2: T2,
  value3: T3,
  value4: T4
): [{ current: T1 }, { current: T2 }, { current: T3 }, { current: T4 }];
export function useStableRef<T1, T2, T3, T4, T5>(
  value1: T1,
  value2: T2,
  value3: T3,
  value4: T4,
  value5: T5
): [{ current: T1 }, { current: T2 }, { current: T3 }, { current: T4 }, { current: T5 }];
export function useStableRef<T1, T2, T3, T4, T5, T6>(
  value1: T1,
  value2: T2,
  value3: T3,
  value4: T4,
  value5: T5,
  value6: T6
): [{ current: T1 }, { current: T2 }, { current: T3 }, { current: T4 }, { current: T5 }, { current: T6 }];
export function useStableRef<T1, T2, T3, T4, T5, T6, T7>(
  value1: T1,
  value2: T2,
  value3: T3,
  value4: T4,
  value5: T5,
  value6: T6,
  value7: T7
): [{ current: T1 }, { current: T2 }, { current: T3 }, { current: T4 }, { current: T5 }, { current: T6 }, { current: T7 }];
export function useStableRef<T1, T2, T3, T4, T5, T6, T7, T8>(
  value1: T1,
  value2: T2,
  value3: T3,
  value4: T4,
  value5: T5,
  value6: T6,
  value7: T7,
  value8: T8
): [{ current: T1 }, { current: T2 }, { current: T3 }, { current: T4 }, { current: T5 }, { current: T6 }, { current: T7 }, { current: T8 }];
export function useStableRef<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
  value1: T1,
  value2: T2,
  value3: T3,
  value4: T4,
  value5: T5,
  value6: T6,
  value7: T7,
  value8: T8,
  value9: T9
): [{ current: T1 }, { current: T2 }, { current: T3 }, { current: T4 }, { current: T5 }, { current: T6 }, { current: T7 }, { current: T8 }, { current: T9 }];
export function useStableRef<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(
  value1: T1,
  value2: T2,
  value3: T3,
  value4: T4,
  value5: T5,
  value6: T6,
  value7: T7,
  value8: T8,
  value9: T9,
  value10: T10
): [{ current: T1 }, { current: T2 }, { current: T3 }, { current: T4 }, { current: T5 }, { current: T6 }, { current: T7 }, { current: T8 }, { current: T9 }, { current: T10 }];
export function useStableRef<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(
  value1: T1,
  value2: T2,
  value3: T3,
  value4: T4,
  value5: T5,
  value6: T6,
  value7: T7,
  value8: T8,
  value9: T9,
  value10: T10,
  value11: T11
): [{ current: T1 }, { current: T2 }, { current: T3 }, { current: T4 }, { current: T5 }, { current: T6 }, { current: T7 }, { current: T8 }, { current: T9 }, { current: T10 }, { current: T11 }];
export function useStableRef<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(
  value1: T1,
  value2: T2,
  value3: T3,
  value4: T4,
  value5: T5,
  value6: T6,
  value7: T7,
  value8: T8,
  value9: T9,
  value10: T10,
  value11: T11,
  value12: T12
): [{ current: T1 }, { current: T2 }, { current: T3 }, { current: T4 }, { current: T5 }, { current: T6 }, { current: T7 }, { current: T8 }, { current: T9 }, { current: T10 }, { current: T11 }, { current: T12 }];
export function useStableRef(...values: unknown[]): { current: unknown } | { current: unknown }[] {
  // For single value: Create a stable ref object
  const singleRef = useRef<{ current: unknown } | undefined>(undefined);

  // For multiple values: Create a stable array of ref objects
  const multiRef = useRef<{ current: unknown }[] | undefined>(undefined);

  // Initialize on first render only
  if (values.length === 1) {
    if (singleRef.current === undefined) {
      singleRef.current = { current: values[0] };
    }
  } else {
    if (multiRef.current === undefined) {
      multiRef.current = values.map((value) => ({ current: value }));
    }
  }
  // Update refs whenever values change
  useEffect(() => {
    if (values.length === 1) {
      singleRef.current!.current = values[0];
    } else {
      values.forEach((value, index) => {
        multiRef.current![index].current = value;
      });
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, values);

  // Return the SAME ref object every render (never changes)
  // The ref object itself is stable, only its .current property updates
  return values.length === 1 ? singleRef.current! : multiRef.current!;
}
