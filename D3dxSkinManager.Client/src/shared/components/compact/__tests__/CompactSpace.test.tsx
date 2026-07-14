import React from 'react';
import { render } from '@testing-library/react';
import { CompactSpace } from '../CompactSpace';

// Locks the fix for the `vertical` prop having no effect: antd Space uses `direction` (there is no
// `orientation` prop), and the old `{...rest}` spread re-injected `direction={undefined}`.
describe('CompactSpace', () => {
  it('applies a vertical layout for the `vertical` shorthand', () => {
    const { container } = render(
      <CompactSpace vertical>
        <div>one</div>
        <div>two</div>
      </CompactSpace>
    );
    expect(container.querySelector('.ant-space-vertical')).toBeTruthy();
    expect(container.querySelector('.ant-space-horizontal')).toBeNull();
  });

  it('defaults to a horizontal layout', () => {
    const { container } = render(
      <CompactSpace>
        <div>one</div>
        <div>two</div>
      </CompactSpace>
    );
    expect(container.querySelector('.ant-space-horizontal')).toBeTruthy();
    expect(container.querySelector('.ant-space-vertical')).toBeNull();
  });

  it('honours an explicit direction prop', () => {
    const { container } = render(
      <CompactSpace direction="vertical">
        <div>one</div>
      </CompactSpace>
    );
    expect(container.querySelector('.ant-space-vertical')).toBeTruthy();
  });
});
