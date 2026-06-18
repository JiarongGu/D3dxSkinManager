import React from 'react';
import { render } from '@testing-library/react';
import { CompactField } from '../CompactField';

describe('CompactField', () => {
  it('renders label, description, hint and the control children', () => {
    const { container, getByText } = render(
      <CompactField label="Executable" description="Path to the game" hint="optional">
        <input data-testid="ctrl" />
      </CompactField>
    );
    expect(getByText('Executable')).toBeTruthy();
    expect(getByText('Path to the game')).toBeTruthy();
    expect(getByText('optional')).toBeTruthy();
    expect(container.querySelector('[data-testid="ctrl"]')).toBeTruthy();
  });

  it('omits the label row when no label or hint is given', () => {
    const { container } = render(
      <CompactField>
        <input />
      </CompactField>
    );
    expect(container.querySelector('.compact-field__label-row')).toBeNull();
  });

  it('shows a required marker only when required', () => {
    const { container, rerender } = render(<CompactField label="Name"><input /></CompactField>);
    expect(container.querySelector('.compact-field__required')).toBeNull();
    rerender(<CompactField label="Name" required><input /></CompactField>);
    expect(container.querySelector('.compact-field__required')).toBeTruthy();
  });
});
