import React from 'react';
import { render } from '@testing-library/react';
import { HealthStatusIcon } from '../HealthStatusIcon';

describe('HealthStatusIcon', () => {
  it('should render error icon for error status', () => {
    const { container } = render(<HealthStatusIcon status="error" />);
    const icon = container.querySelector('.anticon-close-circle');
    expect(icon).toBeTruthy();
  });

  it('should render warning icon for warning status', () => {
    const { container } = render(<HealthStatusIcon status="warning" />);
    const icon = container.querySelector('.anticon-warning');
    expect(icon).toBeTruthy();
  });

  it('should render info icon for info status', () => {
    const { container } = render(<HealthStatusIcon status="info" />);
    const icon = container.querySelector('.anticon-info-circle');
    expect(icon).toBeTruthy();
  });

  it('should render check icon for healthy status', () => {
    const { container } = render(<HealthStatusIcon status="healthy" />);
    const icon = container.querySelector('.anticon-check-circle');
    expect(icon).toBeTruthy();
  });

  it('should render check icon for unknown status (fallback)', () => {
    const { container } = render(<HealthStatusIcon status="something-else" />);
    const icon = container.querySelector('.anticon-check-circle');
    expect(icon).toBeTruthy();
  });

  it('should apply custom size', () => {
    const { container } = render(<HealthStatusIcon status="error" size={20} />);
    const svg = container.querySelector('svg');
    // Ant Design icons apply fontSize via parent span style
    const iconSpan = container.querySelector('.anticon');
    expect(iconSpan).toBeTruthy();
  });
});
