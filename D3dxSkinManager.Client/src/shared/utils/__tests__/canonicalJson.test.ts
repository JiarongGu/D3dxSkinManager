import { describe, it, expect } from 'vitest';
import { canonicalJson } from '../canonicalJson';

describe('canonicalJson', () => {
  it('is order-independent for object keys (the dirty-tracking invariant)', () => {
    expect(canonicalJson({ a: 1, b: 2 })).toBe(canonicalJson({ b: 2, a: 1 }));
  });

  it('sorts nested object keys recursively', () => {
    expect(canonicalJson({ outer: { z: 1, a: 2 } })).toBe(canonicalJson({ outer: { a: 2, z: 1 } }));
  });

  it('preserves array order (ordered lists like input rules must stay distinct)', () => {
    expect(canonicalJson([1, 2])).not.toBe(canonicalJson([2, 1]));
  });

  it('treats objects inside arrays canonically but keeps element order', () => {
    expect(canonicalJson([{ a: 1, b: 2 }])).toBe(canonicalJson([{ b: 2, a: 1 }]));
    expect(canonicalJson([{ a: 1 }, { b: 2 }])).not.toBe(canonicalJson([{ b: 2 }, { a: 1 }]));
  });

  it('detects a real value change (not a false-equal)', () => {
    expect(canonicalJson({ a: 1 })).not.toBe(canonicalJson({ a: 2 }));
  });

  it('round-trips to the original value regardless of input key order', () => {
    // Doubles as a compile-guard: canonicalJson must accept an arbitrary object and return a string.
    const s: string = canonicalJson({ name: 'x', params: { b: '2', a: '1' } });
    expect(JSON.parse(s)).toEqual({ name: 'x', params: { a: '1', b: '2' } });
  });
});
