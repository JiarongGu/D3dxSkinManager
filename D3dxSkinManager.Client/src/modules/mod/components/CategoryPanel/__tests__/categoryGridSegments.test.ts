import { describe, it, expect } from 'vitest';
import { resolveGroupContextTargetId } from '../categoryGridSegments';

/**
 * Guards the right-click-on-group-box fix: a right-click on a category group's BOX area (its padding /
 * the gaps between its cards, which carries data-group-id but no data-node-id) must resolve to THAT
 * group's id so it shows the group's context menu — not '' ("nowhere" / the whole-grid menu).
 */
describe('resolveGroupContextTargetId', () => {
  const el = (attrs: Record<string, string> = {}, parent?: HTMLElement): HTMLElement => {
    const d = document.createElement('div');
    for (const [k, v] of Object.entries(attrs)) d.setAttribute(k, v);
    (parent ?? document.body).appendChild(d);
    return d;
  };

  it('returns the enclosing group id for the group box area', () => {
    const group = el({ 'data-group-id': 'cat1' });
    const box = el({}, group); // padding/gap inside the group, no data-node-id
    expect(resolveGroupContextTargetId(box)).toBe('cat1');
    expect(resolveGroupContextTargetId(group)).toBe('cat1'); // the group div itself
  });

  it('returns the NEAREST group id when groups nest', () => {
    const outer = el({ 'data-group-id': 'outer' });
    const inner = el({ 'data-group-id': 'inner' }, outer);
    expect(resolveGroupContextTargetId(el({}, inner))).toBe('inner');
  });

  it("returns '' for truly-empty space (no enclosing group)", () => {
    expect(resolveGroupContextTargetId(el())).toBe('');
  });
});
