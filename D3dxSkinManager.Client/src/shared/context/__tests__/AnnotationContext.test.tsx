import React from 'react';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AnnotationProvider, useAnnotation, AnnotationLevel } from '../AnnotationContext';
import { useSettingsStore } from '../../../modules/setting/store/settingsStore';
import { settingsService } from '../../services/ipc';

// AnnotationProvider is now store-reactive (mirrors ThemeProvider): it reads the level from
// settingsStore and persists via settingsService — no on-mount GET_GLOBAL. Mock only the persist;
// drive the level through the REAL store (seed via setGlobalSettings).
vi.mock('../../services/ipc', () => ({
  settingsService: { updateGlobalSetting: vi.fn() },
}));

const seedStore = (annotationLevel?: string) =>
  act(() => {
    useSettingsStore.getState().setGlobalSettings(
      annotationLevel === undefined ? undefined : ({ annotationLevel } as never),
    );
  });

const TestComponent = () => {
  const { annotationLevel, setAnnotationLevel } = useAnnotation();
  return (
    <div>
      <div data-testid="level">{annotationLevel}</div>
      <button onClick={() => setAnnotationLevel('more')}>more</button>
    </div>
  );
};

describe('AnnotationContext (store-reactive)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useSettingsStore.getState().reset();
    (settingsService.updateGlobalSetting as ReturnType<typeof vi.fn>).mockResolvedValue(undefined);
  });

  it('reads the annotation level from the settings store', () => {
    seedStore('less');
    render(<AnnotationProvider><TestComponent /></AnnotationProvider>);
    expect(screen.getByTestId('level')).toHaveTextContent('less');
  });

  it('reacts to the store being populated after mount', async () => {
    render(<AnnotationProvider initialLevel="all"><TestComponent /></AnnotationProvider>);
    expect(screen.getByTestId('level')).toHaveTextContent('all');
    seedStore('off');
    await waitFor(() => expect(screen.getByTestId('level')).toHaveTextContent('off'));
  });

  it('falls back to initialLevel when the store has no global settings', () => {
    render(<AnnotationProvider initialLevel="more"><TestComponent /></AnnotationProvider>);
    expect(screen.getByTestId('level')).toHaveTextContent('more');
  });

  it('ignores an invalid level from the store', () => {
    seedStore('bogus');
    render(<AnnotationProvider initialLevel="more"><TestComponent /></AnnotationProvider>);
    expect(screen.getByTestId('level')).toHaveTextContent('more');
  });

  it('setAnnotationLevel optimistically updates, persists, and syncs the store', async () => {
    seedStore('all');
    render(<AnnotationProvider><TestComponent /></AnnotationProvider>);

    await userEvent.click(screen.getByText('more'));

    expect(screen.getByTestId('level')).toHaveTextContent('more'); // optimistic
    await waitFor(() =>
      expect(settingsService.updateGlobalSetting).toHaveBeenCalledWith('annotationLevel', 'more'),
    );
    await waitFor(() =>
      expect(useSettingsStore.getState().globalSettings?.annotationLevel).toBe('more'),
    );
  });

  it('returns the default level outside a provider', () => {
    const Bare = () => <div data-testid="lvl">{useAnnotation().annotationLevel}</div>;
    render(<Bare />);
    expect(screen.getByTestId('lvl')).toHaveTextContent('all');
  });

  it('rolls back to the stored level when persistence fails', async () => {
    seedStore('all');
    (settingsService.updateGlobalSetting as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('nope'));
    render(<AnnotationProvider><TestComponent /></AnnotationProvider>);

    await userEvent.click(screen.getByText('more'));

    // Reverts to the store's 'all' after the failed save.
    await waitFor(() => expect(screen.getByTestId('level')).toHaveTextContent('all'));
  });

  it('accepts every valid level', () => {
    const levels: AnnotationLevel[] = ['all', 'more', 'less', 'off'];
    for (const lvl of levels) {
      useSettingsStore.getState().reset();
      seedStore(lvl);
      const { unmount } = render(<AnnotationProvider><TestComponent /></AnnotationProvider>);
      expect(screen.getByTestId('level')).toHaveTextContent(lvl);
      unmount();
    }
  });
});
