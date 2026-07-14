import React from 'react';
import { render, fireEvent } from '@testing-library/react';
import { CountChip } from '../CountChip';

describe('CountChip', () => {
  it('renders the label and count in separate spans (so the atom can equalize their line boxes)', () => {
    const { container } = render(<CountChip label="失败" count={3} />);
    const label = container.querySelector('.count-chip__label');
    const count = container.querySelector('.count-chip__count');
    expect(label?.textContent).toBe('失败');
    expect(count?.textContent).toBe('3');
  });

  it('applies the tone modifier class', () => {
    const { container } = render(<CountChip label="Failed" count={1} tone="failed" />);
    expect(container.querySelector('.count-chip--failed')).toBeTruthy();
  });

  it('defaults to the neutral tone', () => {
    const { container } = render(<CountChip label="Total" count={0} />);
    expect(container.querySelector('.count-chip--default')).toBeTruthy();
  });

  it('marks the active state only when active', () => {
    const { container, rerender } = render(<CountChip label="Total" count={0} />);
    expect(container.querySelector('.count-chip--active')).toBeNull();
    rerender(<CountChip label="Total" count={0} active />);
    expect(container.querySelector('.count-chip--active')).toBeTruthy();
  });

  it('renders the optional leading icon only when provided', () => {
    const { container, rerender } = render(<CountChip label="Active" count={2} />);
    expect(container.querySelector('.count-chip__icon')).toBeNull();
    rerender(<CountChip label="Active" count={2} icon={<span data-testid="spin" />} />);
    expect(container.querySelector('.count-chip__icon [data-testid="spin"]')).toBeTruthy();
  });

  it('fires onClick', () => {
    const onClick = vi.fn();
    const { container } = render(<CountChip label="Total" count={0} onClick={onClick} />);
    fireEvent.click(container.querySelector('.count-chip')!);
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});
