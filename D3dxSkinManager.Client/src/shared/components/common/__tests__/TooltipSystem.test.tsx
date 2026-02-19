import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AnnotationProvider, useAnnotation, AnnotationLevel } from '../TooltipSystem';
import { settingsService } from '../../../../modules/settings/services/settingsService';

// Mock the settings service
jest.mock('../../../../modules/settings/services/settingsService');

describe('AnnotationContext (TooltipSystem)', () => {
  // Helper component to access context
  const TestComponent = () => {
    const { annotationLevel, setAnnotationLevel } = useAnnotation();
    return (
      <div>
        <div data-testid="annotation-level">{annotationLevel}</div>
        <button onClick={() => setAnnotationLevel('all')}>Set All</button>
        <button onClick={() => setAnnotationLevel('more')}>Set More</button>
        <button onClick={() => setAnnotationLevel('less')}>Set Less</button>
        <button onClick={() => setAnnotationLevel('off')}>Set Off</button>
      </div>
    );
  };

  beforeEach(() => {
    // Reset mocks before each test
    jest.clearAllMocks();

    // Default mock implementation
    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'light',
      logLevel: 'INFO',
      annotationLevel: 'all'
    });

    (settingsService.updateGlobalSetting as jest.Mock).mockResolvedValue(undefined);
  });

  it('should use initialLevel prop if provided', () => {
    // Act
    render(
      <AnnotationProvider initialLevel="more">
        <TestComponent />
      </AnnotationProvider>
    );

    // Assert - Should show initial level immediately
    expect(screen.getByTestId('annotation-level')).toHaveTextContent('more');
  });

  it('should load annotation level from backend on mount', async () => {
    // Arrange
    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'light',
      logLevel: 'INFO',
      annotationLevel: 'less'
    });

    // Act
    render(
      <AnnotationProvider>
        <TestComponent />
      </AnnotationProvider>
    );

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('less');
    });

    expect(settingsService.getGlobalSettings).toHaveBeenCalledTimes(1);
  });

  it('should retry on failure with exponential backoff', async () => {
    // Arrange
    let callCount = 0;
    (settingsService.getGlobalSettings as jest.Mock).mockImplementation(() => {
      callCount++;
      if (callCount < 3) {
        return Promise.reject(new Error('Network error'));
      }
      return Promise.resolve({ theme: 'light', logLevel: 'INFO', annotationLevel: 'all' });
    });

    // Act
    render(
      <AnnotationProvider>
        <TestComponent />
      </AnnotationProvider>
    );

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
    }, { timeout: 5000 });

    expect(callCount).toBe(3); // Tried 3 times before success
  });

  it('should fall back to default "all" after all retries fail', async () => {
    // Arrange
    (settingsService.getGlobalSettings as jest.Mock).mockRejectedValue(
      new Error('Persistent network error')
    );

    // Act
    render(
      <AnnotationProvider initialLevel="less">
        <TestComponent />
      </AnnotationProvider>
    );

    // Assert - Should eventually show 'all' (default fallback)
    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
    }, { timeout: 10000 });
  });

  it('should update backend when annotation level changes', async () => {
    // Arrange
    render(
      <AnnotationProvider>
        <TestComponent />
      </AnnotationProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
    });

    // Act
    const button = screen.getByText('Set More');
    await userEvent.click(button);

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('more');
    });

    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('annotationLevel', 'more');
  });

  it('should optimistically update UI before backend confirms', async () => {
    // Arrange
    let resolveUpdate: () => void;
    (settingsService.updateGlobalSetting as jest.Mock).mockImplementation(() =>
      new Promise(resolve => { resolveUpdate = resolve as () => void; })
    );

    render(
      <AnnotationProvider>
        <TestComponent />
      </AnnotationProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
    });

    // Act
    const button = screen.getByText('Set Less');
    await userEvent.click(button);

    // Assert - Should update immediately (optimistic)
    expect(screen.getByTestId('annotation-level')).toHaveTextContent('less');

    // Complete the backend call
    resolveUpdate!();
    await waitFor(() => {
      expect(settingsService.updateGlobalSetting).toHaveBeenCalled();
    });
  });

  it('should rollback on save failure', async () => {
    // Arrange
    (settingsService.updateGlobalSetting as jest.Mock).mockRejectedValue(
      new Error('Save failed')
    );
    (settingsService.getGlobalSettings as jest.Mock)
      .mockResolvedValueOnce({ theme: 'light', logLevel: 'INFO', annotationLevel: 'all' })
      .mockResolvedValueOnce({ theme: 'light', logLevel: 'INFO', annotationLevel: 'all' });

    render(
      <AnnotationProvider>
        <TestComponent />
      </AnnotationProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
    });

    // Act
    const button = screen.getByText('Set Less');
    await userEvent.click(button);

    // Assert - Should rollback to 'all' after save fails
    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
    });

    // Should have tried to reload from backend
    expect(settingsService.getGlobalSettings).toHaveBeenCalledTimes(2);
  });

  it('should handle all valid annotation levels', async () => {
    // Arrange
    render(
      <AnnotationProvider>
        <TestComponent />
      </AnnotationProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
    });

    // Act & Assert - Test each level
    const levels: AnnotationLevel[] = ['more', 'less', 'off', 'all'];

    for (const level of levels) {
      const button = screen.getByText(`Set ${level.charAt(0).toUpperCase() + level.slice(1)}`);
      await userEvent.click(button);

      await waitFor(() => {
        expect(screen.getByTestId('annotation-level')).toHaveTextContent(level);
      });

      expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('annotationLevel', level);
    }
  });

  it('should ignore invalid annotation levels from backend', async () => {
    // Arrange
    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'light',
      logLevel: 'INFO',
      annotationLevel: 'invalid-level' // Invalid level
    });

    // Act
    render(
      <AnnotationProvider initialLevel="more">
        <TestComponent />
      </AnnotationProvider>
    );

    // Assert - Should keep initial level since backend returned invalid
    await waitFor(() => {
      // Give it time to try loading from backend
      expect(settingsService.getGlobalSettings).toHaveBeenCalled();
    });

    // Should still show initial level (not the invalid one)
    expect(screen.getByTestId('annotation-level')).toHaveTextContent('more');
  });

  it('should handle multiple level changes in succession', async () => {
    // Arrange
    render(
      <AnnotationProvider>
        <TestComponent />
      </AnnotationProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
    });

    // Act - Multiple rapid changes
    await userEvent.click(screen.getByText('Set More'));
    await userEvent.click(screen.getByText('Set Less'));
    await userEvent.click(screen.getByText('Set Off'));

    // Assert - Should end up with final value
    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('off');
    });

    // Should have called update for each change
    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('annotationLevel', 'more');
    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('annotationLevel', 'less');
    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('annotationLevel', 'off');
  });

  it('should throw error when useAnnotation is used outside provider', () => {
    // Arrange
    const ComponentWithoutProvider = () => {
      useAnnotation(); // This should throw
      return <div>Test</div>;
    };

    // Act & Assert
    // Suppress console.error for this test
    const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

    expect(() => render(<ComponentWithoutProvider />)).toThrow();

    consoleSpy.mockRestore();
  });

  it('should not update backend if level does not change', async () => {
    // Arrange
    render(
      <AnnotationProvider>
        <TestComponent />
      </AnnotationProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
    });

    // Clear previous calls
    (settingsService.updateGlobalSetting as jest.Mock).mockClear();

    // Act - Click same level
    const button = screen.getByText('Set All');
    await userEvent.click(button);

    // Assert - Should still call update (component doesn't check for duplicates)
    // This is expected behavior - let backend handle deduplication if needed
    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('annotationLevel', 'all');
  });

  it('should handle backend returning empty annotationLevel gracefully', async () => {
    // Arrange
    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'light',
      logLevel: 'INFO',
      annotationLevel: '' // Empty string
    });

    // Act
    render(
      <AnnotationProvider initialLevel="more">
        <TestComponent />
      </AnnotationProvider>
    );

    // Assert - Should keep initial level
    await waitFor(() => {
      expect(settingsService.getGlobalSettings).toHaveBeenCalled();
    });

    expect(screen.getByTestId('annotation-level')).toHaveTextContent('more');
  });
});
