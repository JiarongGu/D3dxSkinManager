import React from 'react';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const updateGlobalSetting = vi.fn();

// Preserve the rest of the ipc barrel (settingsStore imports types from it); only stub settingsService.
vi.mock('../../services/ipc', async (importActual) => ({
  ...(await importActual<any>()),
  settingsService: { updateGlobalSetting: (...a: any[]) => updateGlobalSetting(...a) },
}));

import { ThemeProvider, useTheme } from '../ThemeContext';
import type { ThemeMode } from '../ThemeContext';
import { useSettingsStore } from '../../../modules/setting/store/settingsStore';
import type { GlobalSettings } from '../../services/ipc';

const gs = (theme: ThemeMode): GlobalSettings => ({
  theme,
  annotationLevel: 'all',
  logLevel: 'info',
  language: 'en',
  autoUpdateCheck: true,
  contentVeilEnabled: false,
  lastUpdated: '',
});

const Harness: React.FC = () => {
  const { theme, effectiveTheme, setTheme, isLoading } = useTheme();
  return (
    <div>
      <div data-testid="theme">{theme}</div>
      <div data-testid="effective">{effectiveTheme}</div>
      <div data-testid="loading">{String(isLoading)}</div>
      <button onClick={() => void setTheme('dark')}>set-dark</button>
    </div>
  );
};

const renderProvider = () =>
  render(
    <ThemeProvider>
      <Harness />
    </ThemeProvider>,
  );

describe('ThemeContext', () => {
  beforeEach(() => {
    updateGlobalSetting.mockReset();
    updateGlobalSetting.mockResolvedValue(undefined);
    act(() => {
      useSettingsStore.getState().setGlobalSettings(undefined);
      useSettingsStore.getState().setGlobalSettingsLoading(false);
    });
    document.documentElement.removeAttribute('data-theme');
  });

  it('resolves effectiveTheme from the store theme and sets data-theme', async () => {
    act(() => useSettingsStore.getState().setGlobalSettings(gs('dark')));
    renderProvider();

    await waitFor(() => expect(screen.getByTestId('effective')).toHaveTextContent('dark'));
    expect(screen.getByTestId('theme')).toHaveTextContent('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it("resolves 'auto' to the system theme (matchMedia stub → light)", async () => {
    act(() => useSettingsStore.getState().setGlobalSettings(gs('auto')));
    renderProvider();

    await waitFor(() => expect(screen.getByTestId('theme')).toHaveTextContent('auto'));
    expect(screen.getByTestId('effective')).toHaveTextContent('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('reacts to a store theme change after mount', async () => {
    act(() => useSettingsStore.getState().setGlobalSettings(gs('light')));
    renderProvider();
    await waitFor(() => expect(screen.getByTestId('effective')).toHaveTextContent('light'));

    act(() => useSettingsStore.getState().setGlobalSettings(gs('dark')));
    await waitFor(() => expect(screen.getByTestId('effective')).toHaveTextContent('dark'));
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('setTheme persists via updateGlobalSetting and syncs the store', async () => {
    act(() => useSettingsStore.getState().setGlobalSettings(gs('light')));
    renderProvider();

    await userEvent.click(screen.getByText('set-dark'));

    await waitFor(() => expect(updateGlobalSetting).toHaveBeenCalledWith('theme', 'dark'));
    expect(screen.getByTestId('theme')).toHaveTextContent('dark');
    await waitFor(() => expect(useSettingsStore.getState().globalSettings?.theme).toBe('dark'));
  });

  it('reflects globalSettingsLoading from the store', () => {
    act(() => useSettingsStore.getState().setGlobalSettingsLoading(true));
    renderProvider();
    expect(screen.getByTestId('loading')).toHaveTextContent('true');
  });

  it('useTheme throws outside a ThemeProvider', () => {
    const Bare: React.FC = () => {
      useTheme();
      return null;
    };
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
    expect(() => render(<Bare />)).toThrow('useTheme must be used within ThemeProvider');
    spy.mockRestore();
  });
});
