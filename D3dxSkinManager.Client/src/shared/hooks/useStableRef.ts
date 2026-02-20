import { useRef, useEffect } from 'react';

/**
 * Creates stable references to one or more values using useRef with automatic updates.
 *
 * This hook solves the common React closure issue where callbacks capture stale values
 * from their creation time, even when dependencies change.
 *
 * **Problem it solves:**
 * When using `useCallback`, if the callback is passed to event handlers or refs that
 * don't update frequently, those handlers will continue to call the old callback with
 * stale closure values, even though the callback function itself is recreated with new
 * dependencies.
 *
 * **How it works:**
 * - Stores the latest value(s) in ref(s) that persist across renders
 * - Updates the ref(s) whenever the value(s) change
 * - Callbacks can access `ref.current` to always get the latest value
 *
 * **Use cases:**
 * - Accessing props/state in callbacks without triggering re-creation
 * - Accessing array/object data that changes frequently
 * - Avoiding closure issues in event handlers
 * - Callbacks passed to third-party libraries or stored in refs
 *
 * @example Single value
 * ```tsx
 * function MyComponent({ items }) {
 *   const itemsRef = useStableRef(items);
 *
 *   const handleClick = useCallback(() => {
 *     // Always accesses current items, no need to add to deps
 *     console.log(itemsRef.current.length);
 *   }, []); // Empty deps array - callback never recreated
 *
 *   return <button onClick={handleClick}>Click</button>;
 * }
 * ```
 *
 * @example Multiple values
 * ```tsx
 * function MyComponent({ items, filters, selectedId }) {
 *   const [itemsRef, filtersRef, selectedIdRef] = useStableRef(items, filters, selectedId);
 *
 *   const handleSearch = useCallback(() => {
 *     const filtered = itemsRef.current.filter(item =>
 *       filtersRef.current.includes(item.type) &&
 *       item.id !== selectedIdRef.current
 *     );
 *     return filtered;
 *   }, []); // No dependencies needed!
 *
 *   return <SearchButton onSearch={handleSearch} />;
 * }
 * ```
 *
 * @param values One or more values to store in refs (supports up to 12 values)
 * @returns Single ref (if one value) or array of refs (if multiple values)
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
export function useStableRef(...values: any[]): { current: any } | { current: any }[] {
  // For single value: Create a stable ref object
  const singleRef = useRef<{ current: any } | null>(null);

  // For multiple values: Create a stable array of ref objects
  const multiRef = useRef<{ current: any }[] | null>(null);

  // Initialize on first render only
  if (values.length === 1) {
    if (singleRef.current === null) {
      singleRef.current = { current: values[0] };
    }
  } else {
    if (multiRef.current === null) {
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
