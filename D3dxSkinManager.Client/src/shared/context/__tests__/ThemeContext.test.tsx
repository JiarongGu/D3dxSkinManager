import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeProvider, useTheme, ThemeMode } from '../ThemeContext';
import { settingsService } from '../../../modules/settings/services/settingsService';

// Mock the settings service
jest.mock('../../../modules/settings/services/settingsService');

describe('ThemeContext', () => {
  // Helper component to access context
  const TestComponent = () => {
    const { theme, effectiveTheme, setTheme, isLoading } = useTheme();
    return (
      <div>
        <div data-testid="theme">{theme}</div>
        <div data-testid="effective-theme">{effectiveTheme}</div>
        <div data-testid="is-loading">{isLoading ? 'loading' : 'loaded'}</div>
        <button onClick={() => setTheme('dark')}>Set Dark</button>
        <button onClick={() => setTheme('light')}>Set Light</button>
        <button onClick={() => setTheme('auto')}>Set Auto</button>
      </div>
    );
  };

  beforeEach(() => {
    // Reset mocks before each test
    jest.clearAllMocks();

    // Mock window.matchMedia
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: jest.fn().mockImplementation(query => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: jest.fn(),
        removeListener: jest.fn(),
        addEventListener: jest.fn(),
        removeEventListener: jest.fn(),
        dispatchEvent: jest.fn(),
      })),
    });

    // Default mock implementation
    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'light',
      logLevel: 'INFO',
      annotationLevel: 'all'
    });

    (settingsService.updateGlobalSetting as jest.Mock).mockResolvedValue(undefined);
  });

  it('should render with loading state initially', () => {
    // Arrange & Act
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    // Assert - Initially loading
    expect(screen.getByTestId('is-loading')).toHaveTextContent('loading');
  });

  it('should load theme from backend on mount', async () => {
    // Arrange
    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'dark',
      logLevel: 'INFO',
      annotationLevel: 'all'
    });

    // Act
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('dark');
      expect(screen.getByTestId('is-loading')).toHaveTextContent('loaded');
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
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    }, { timeout: 5000 });

    expect(callCount).toBe(3); // Tried 3 times before success
  });

  it('should fall back to light theme after all retries fail', async () => {
    // Arrange
    (settingsService.getGlobalSettings as jest.Mock).mockRejectedValue(
      new Error('Persistent network error')
    );

    // Act
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    // Assert - Should eventually show light theme
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
      expect(screen.getByTestId('is-loading')).toHaveTextContent('loaded');
    }, { timeout: 10000 });
  });

  it('should update backend when theme changes', async () => {
    // Arrange
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    });

    // Act
    const button = screen.getByText('Set Dark');
    await userEvent.click(button);

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('dark');
    });

    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('theme', 'dark');
  });

  it('should optimistically update UI before backend confirms', async () => {
    // Arrange
    let resolveUpdate: () => void;
    (settingsService.updateGlobalSetting as jest.Mock).mockImplementation(() =>
      new Promise(resolve => { resolveUpdate = resolve as () => void; })
    );

    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    });

    // Act
    const button = screen.getByText('Set Dark');
    await userEvent.click(button);

    // Assert - Should update immediately (optimistic)
    expect(screen.getByTestId('theme')).toHaveTextContent('dark');

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
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    });

    // Act
    const button = screen.getByText('Set Dark');
    await userEvent.click(button);

    // Assert - Should rollback to 'light' after save fails
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    });

    // Should have tried to reload from backend
    expect(settingsService.getGlobalSettings).toHaveBeenCalledTimes(2);
  });

  it('should apply correct effective theme for auto mode with dark system preference', async () => {
    // Arrange - Mock dark system preference
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: jest.fn().mockImplementation(query => ({
        matches: query === '(prefers-color-scheme: dark)',
        media: query,
        onchange: null,
        addListener: jest.fn(),
        removeListener: jest.fn(),
        addEventListener: jest.fn(),
        removeEventListener: jest.fn(),
        dispatchEvent: jest.fn(),
      })),
    });

    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'auto',
      logLevel: 'INFO',
      annotationLevel: 'all'
    });

    // Act
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('auto');
      expect(screen.getByTestId('effective-theme')).toHaveTextContent('dark');
    });
  });

  it('should apply correct effective theme for auto mode with light system preference', async () => {
    // Arrange - Mock light system preference
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: jest.fn().mockImplementation(query => ({
        matches: false, // Not dark
        media: query,
        onchange: null,
        addListener: jest.fn(),
        removeListener: jest.fn(),
        addEventListener: jest.fn(),
        removeEventListener: jest.fn(),
        dispatchEvent: jest.fn(),
      })),
    });

    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'auto',
      logLevel: 'INFO',
      annotationLevel: 'all'
    });

    // Act
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    // Assert
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('auto');
      expect(screen.getByTestId('effective-theme')).toHaveTextContent('light');
    });
  });

  it('should set data-theme attribute on document root', async () => {
    // Arrange
    (settingsService.getGlobalSettings as jest.Mock).mockResolvedValue({
      theme: 'dark',
      logLevel: 'INFO',
      annotationLevel: 'all'
    });

    // Act
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    // Assert
    await waitFor(() => {
      expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    });
  });

  it('should handle multiple theme changes in succession', async () => {
    // Arrange
    render(
      <ThemeProvider>
        <TestComponent />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    });

    // Act - Multiple rapid changes
    await userEvent.click(screen.getByText('Set Dark'));
    await userEvent.click(screen.getByText('Set Auto'));
    await userEvent.click(screen.getByText('Set Light'));

    // Assert - Should end up with final value
    await waitFor(() => {
      expect(screen.getByTestId('theme')).toHaveTextContent('light');
    });

    // Should have called update for each change
    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('theme', 'dark');
    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('theme', 'auto');
    expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('theme', 'light');
  });

  it('should throw error when useTheme is used outside provider', () => {
    // Arrange
    const ComponentWithoutProvider = () => {
      useTheme(); // This should throw
      return <div>Test</div>;
    };

    // Act & Assert
    // Suppress console.error for this test
    const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

    expect(() => render(<ComponentWithoutProvider />)).toThrow(
      'useTheme must be used within ThemeProvider'
    );

    consoleSpy.mockRestore();
  });
});
