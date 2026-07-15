import React from 'react';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ModPackageProvider, useModPackage } from '../ModPackageContext';
import { eventBus, Module, ToolsEventType } from '../../../../../../shared/services/eventBus';
import { api } from '../../../../../../shared/services/ipc';
import type { ExportResult, ImportResult } from '../../../../../../shared/types/modPackage.types';

// Fire-and-forget contract (Batch 1 — code-review H+M): export/import no longer return their result
// from the awaited IPC call — the IPC acks immediately (`{ started: true }`) and the real result
// arrives via TOOL/MOD_PACKAGE_EXPORT_COMPLETE / MOD_PACKAGE_IMPORT_COMPLETE. These tests lock that
// wiring: start* leaves status 'running' after the IPC resolves, and only the completion EVENT flips
// it to 'done' with the payload. Referencing the two new ToolsEventType members also compile-guards
// the enum addition (tsc fails until they exist).

vi.mock('../../../../../../shared/services/ipc', () => ({
  api: {
    tool: {
      exportModPackage: vi.fn(),
      importModPackage: vi.fn(),
    },
    system: {
      openFolderDialog: vi.fn(),
    },
    mod: { getAllMods: vi.fn() },
    category: { getCategoryTree: vi.fn() },
  },
}));

vi.mock('../../../../../../shared/context/ProfileContext', () => ({
  useProfile: () => ({ selectedProfileId: 'p1' }),
}));

const TestConsumer: React.FC = () => {
  const {
    startExport, startImport,
    exportStatus, exportResult,
    importStatus, importResult,
    setExportOpts, setPackagePath, setSelectedImportIds,
  } = useModPackage();

  return (
    <div>
      <div data-testid="export-status">{exportStatus}</div>
      <div data-testid="export-count">{exportResult?.exportedCount ?? ''}</div>
      <div data-testid="import-status">{importStatus}</div>
      <div data-testid="import-count">{importResult?.importedCount ?? ''}</div>
      <button onClick={() => setExportOpts(prev => ({ ...prev, packageName: 'MyPkg' }))}>prep-export</button>
      <button onClick={() => void startExport()}>do-export</button>
      <button onClick={() => { setPackagePath('C:/pkg'); setSelectedImportIds(new Set(['m1'])); }}>prep-import</button>
      <button onClick={() => void startImport({ updateExisting: true, importPreviews: true, createCategories: true })}>do-import</button>
    </div>
  );
};

const renderCtx = () =>
  render(
    <ModPackageProvider>
      <TestConsumer />
    </ModPackageProvider>,
  );

describe('ModPackageContext — fire-and-forget export/import (Batch 1)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    eventBus.clear();
    (api.system.openFolderDialog as ReturnType<typeof vi.fn>).mockResolvedValue({ success: true, filePath: 'C:/out' });
    (api.tool.exportModPackage as ReturnType<typeof vi.fn>).mockResolvedValue({ started: true });
    (api.tool.importModPackage as ReturnType<typeof vi.fn>).mockResolvedValue({ started: true });
  });

  it('startExport fires the IPC and stays "running" — no result from the return value', async () => {
    renderCtx();

    await userEvent.click(screen.getByText('prep-export'));
    await userEvent.click(screen.getByText('do-export'));

    // IPC was invoked, but status must NOT flip to done from the awaited return (fire-and-forget).
    await waitFor(() => expect(api.tool.exportModPackage).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByTestId('export-status')).toHaveTextContent('running'));
    expect(screen.getByTestId('export-count')).toHaveTextContent('');
  });

  it('MOD_PACKAGE_EXPORT_COMPLETE event flips status to done and sets the result', async () => {
    renderCtx();

    await userEvent.click(screen.getByText('prep-export'));
    await userEvent.click(screen.getByText('do-export'));
    await waitFor(() => expect(screen.getByTestId('export-status')).toHaveTextContent('running'));

    const result: ExportResult = {
      success: true, exportedCount: 3, outputPath: 'C:/out/MyPkg', totalSizeBytes: 100, errors: [],
    };
    act(() => {
      eventBus.emit({ module: Module.TOOL, type: ToolsEventType.MOD_PACKAGE_EXPORT_COMPLETE, payload: result });
    });

    await waitFor(() => expect(screen.getByTestId('export-status')).toHaveTextContent('done'));
    expect(screen.getByTestId('export-count')).toHaveTextContent('3');
  });

  it('startImport fires the IPC and stays "running" until MOD_PACKAGE_IMPORT_COMPLETE', async () => {
    renderCtx();

    await userEvent.click(screen.getByText('prep-import'));
    await userEvent.click(screen.getByText('do-import'));

    await waitFor(() => expect(api.tool.importModPackage).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByTestId('import-status')).toHaveTextContent('running'));
    expect(screen.getByTestId('import-count')).toHaveTextContent('');

    const result: ImportResult = {
      importedCount: 2, updatedCount: 0, skippedCount: 0, failedCount: 0,
      errors: [], importedModNames: ['a', 'b'], updatedModNames: [],
    };
    act(() => {
      eventBus.emit({ module: Module.TOOL, type: ToolsEventType.MOD_PACKAGE_IMPORT_COMPLETE, payload: result });
    });

    await waitFor(() => expect(screen.getByTestId('import-status')).toHaveTextContent('done'));
    expect(screen.getByTestId('import-count')).toHaveTextContent('2');
  });
});
