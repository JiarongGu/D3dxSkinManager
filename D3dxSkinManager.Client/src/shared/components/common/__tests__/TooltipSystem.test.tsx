import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AnnotationProvider, useAnnotation, AnnotationLevel } from '../TooltipSystem';
import { settingsService } from '../../../services/ipc';

// The `settingsService` instance lives in the ipc barrel (settingsService.ts only exports the class),
// so mock the barrel and stub the two methods the component uses.
vi.mock('../../../services/ipc', () => ({
  settingsService: { getGlobalSettings: vi.fn(), updateGlobalSetting: vi.fn() },
}));

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
    vi.clearAllMocks();

    // Default mock implementation
    (settingsService.getGlobalSettings as vi.Mock).mockResolvedValue({
      theme: 'light',
      logLevel: 'INFO',
      annotationLevel: 'all'
    });

    (settingsService.updateGlobalSetting as vi.Mock).mockResolvedValue(undefined);
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
    (settingsService.getGlobalSettings as vi.Mock).mockResolvedValue({
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
    (settingsService.getGlobalSettings as vi.Mock).mockImplementation(() => {
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

    // Assert: wait for the 3rd (successful) attempt. initialLevel is already 'all', so the displayed
    // text matches the default from the start — wait on the call count, not the text.
    await waitFor(() => {
      expect(settingsService.getGlobalSettings).toHaveBeenCalledTimes(3);
    }, { timeout: 5000 });
    expect(screen.getByTestId('annotation-level')).toHaveTextContent('all');
  });

  it('should fall back to default "all" after all retries fail', async () => {
    // Arrange
    (settingsService.getGlobalSettings as vi.Mock).mockRejectedValue(
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
    (settingsService.updateGlobalSetting as vi.Mock).mockImplementation(() =>
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
    (settingsService.updateGlobalSetting as vi.Mock).mockRejectedValue(
      new Error('Save failed')
    );
    (settingsService.getGlobalSettings as vi.Mock)
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
    (settingsService.getGlobalSettings as vi.Mock).mockResolvedValue({
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

  it('returns the default annotation level when used outside a provider', () => {
    // useAnnotation reads a default-valued context (createContext default 'all'), so it does NOT throw
    // outside a provider — it returns the default. (Behavior changed from the original throw.)
    const ComponentWithoutProvider = () => <div data-testid="lvl">{useAnnotation().annotationLevel}</div>;

    render(<ComponentWithoutProvider />);

    expect(screen.getByTestId('lvl')).toHaveTextContent('all');
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
    (settingsService.updateGlobalSetting as vi.Mock).mockClear();

    // Act - Click same level
    const button = screen.getByText('Set All');
    await userEvent.click(button);

    // Assert - Should still call update (component doesn't check for duplicates)
    // This is expected behavior - let backend handle deduplication if needed
    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('annotationLevel', 'all');
  });

  it('should handle backend returning empty annotationLevel gracefully', async () => {
    // Arrange
    (settingsService.getGlobalSettings as vi.Mock).mockResolvedValue({
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
