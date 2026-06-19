import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { FullAnalysisReport } from '../../../../../shared/types/analysis.types';

// Mock antd to avoid @rc-component/picker resolution issue in CRA Jest
vi.mock('antd', () => {
  const React = require('react');
  return {
    Tag: ({ children, onClick, className, color, ...rest }: any) => (
      <span onClick={onClick} className={className} data-color={color} {...rest}>{children}</span>
    ),
    Tooltip: ({ children, title }: any) => <span title={title}>{children}</span>,
    Collapse: ({ items }: any) => (
      <div>{items?.map((item: any, i: number) => <div key={i}>{item.label}{item.children}</div>)}</div>
    ),
    Empty: ({ description }: any) => <div>{description}</div>,
    Input: ({ value, onChange, placeholder }: any) => (
      <input value={value} onChange={onChange} placeholder={placeholder} />
    ),
  };
});

// Stub the edit dialog so it doesn't pull real antd Modal + the compact tree into the test render.
vi.mock('../../../../../shared/components/dialogs/FormDialog', () => ({
  FormDialog: ({ visible, title, children }: any) => (visible ? <div>{title}{children}</div> : null),
}));

// Mock i18n
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

// Mock compact components
vi.mock('../../../../../shared/components/compact', () => {
  const React = require('react');
  return {
    CompactButton: ({ children, onClick, disabled, ...rest }: any) => (
      <button onClick={onClick} disabled={disabled} {...rest}>{children}</button>
    ),
    CompactInput: ({ prefix, placeholder, value, onChange, allowClear, style }: any) => (
      <input placeholder={placeholder} value={value} onChange={onChange} style={style} />
    ),
  };
});

// Mock notification
vi.mock('../../../../../shared/utils/notification', () => ({
  notification: { success: vi.fn() },
}));

// Must import after mocks
import { FindingsView } from '../components/FindingsView';

const makeReport = (overrides: Partial<FullAnalysisReport> = {}): FullAnalysisReport => ({
  sessionId: 'session-1',
  status: 'completed',
  totalMods: 5,
  analyzedCount: 5,
  skippedCount: 0,
  healthyCount: 2,
  warningCount: 1,
  errorCount: 1,
  results: [
    {
      modId: 'mod-1', modName: 'BrokenMod', categoryName: 'Weapons', isLoaded: false,
      hasCache: true, isAvailable: true, healthStatus: 'error',
      issues: [{ type: 'noIniFile', severity: 'error', message: 'No .ini files found' }],
      iniFileCount: 0, resourceFileCount: 0, textureOverrideCount: 0,
      targetHashes: [], bufferHash: '', textureHash: '',
      bufferSizeBytes: 0, textureSizeBytes: 0, pluginDependencies: [],
    },
    {
      modId: 'mod-2', modName: 'StaleMod', categoryName: 'Characters', isLoaded: true,
      hasCache: true, isAvailable: true, healthStatus: 'warning',
      issues: [{ type: 'staleHash', severity: 'info', message: 'All target hashes are unique' }],
      iniFileCount: 1, resourceFileCount: 3, textureOverrideCount: 2,
      targetHashes: ['abc123'], bufferHash: 'buf1', textureHash: 'tex1',
      bufferSizeBytes: 1024, textureSizeBytes: 2048, pluginDependencies: [],
    },
    {
      modId: 'mod-3', modName: 'HealthyMod', categoryName: 'Weapons', isLoaded: false,
      hasCache: true, isAvailable: true, healthStatus: 'healthy',
      issues: [],
      iniFileCount: 1, resourceFileCount: 5, textureOverrideCount: 3,
      targetHashes: ['def456'], bufferHash: 'buf2', textureHash: 'tex2',
      bufferSizeBytes: 4096, textureSizeBytes: 8192, pluginDependencies: [],
    },
  ],
  duplicateGroups: [],
  identicalCount: 0,
  textureVariantCount: 0,
  conflicts: [],
  conflictCount: 0,
  affectedModCount: 0,
  suspiciousHashes: [],
  ...overrides,
});

describe('FindingsView', () => {
  const defaultProps = {
    report: makeReport(),
    scanning: false,
    onNewScan: vi.fn(),
    onRescan: vi.fn(),
    onViewHistory: vi.fn(),
    onDeleteMod: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render findings summary with count', () => {
    render(<FindingsView {...defaultProps} />);
    expect(screen.getByText(/5\/5/)).toBeInTheDocument();
  });

  it('should render broken mods section', () => {
    render(<FindingsView {...defaultProps} />);
    expect(screen.getByText('BrokenMod')).toBeInTheDocument();
  });

  it('should render filter chips', () => {
    render(<FindingsView {...defaultProps} />);
    // All filter chip should exist (some labels appear in both chips and section headers)
    expect(screen.getByText(/tools\.modAnalyzer\.filterAll/)).toBeInTheDocument();
    expect(screen.getAllByText(/tools\.modAnalyzer\.broken/).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/tools\.modAnalyzer\.healthy/).length).toBeGreaterThanOrEqual(1);
  });

  it('should filter results when search text entered', async () => {
    render(<FindingsView {...defaultProps} />);
    const searchInput = screen.getByPlaceholderText('tools.modAnalyzer.searchFindings');
    await userEvent.type(searchInput, 'Broken');
    expect(screen.getByText('BrokenMod')).toBeInTheDocument();
    // HealthyMod should not appear in the broken section after search
  });

  it('should disable scan buttons while scanning', () => {
    render(<FindingsView {...defaultProps} scanning={true} />);
    const newScanBtn = screen.getByText('tools.modAnalyzer.newScan').closest('button');
    expect(newScanBtn).toBeDisabled();
  });

  it('should call onRescan when rescan button clicked', async () => {
    render(<FindingsView {...defaultProps} />);
    const rescanBtn = screen.getByText('tools.modAnalyzer.rescan');
    await userEvent.click(rescanBtn);
    expect(defaultProps.onRescan).toHaveBeenCalledTimes(1);
  });

  it('should call onViewHistory when history button clicked', async () => {
    render(<FindingsView {...defaultProps} />);
    const historyBtn = screen.getByText('tools.modAnalyzer.history');
    await userEvent.click(historyBtn);
    expect(defaultProps.onViewHistory).toHaveBeenCalledTimes(1);
  });

  it('should show empty state when no findings match filter', async () => {
    const emptyReport = makeReport({ results: [], duplicateGroups: [], conflicts: [] });
    render(<FindingsView {...defaultProps} report={emptyReport} />);
    expect(screen.getByText('tools.modAnalyzer.noFindings')).toBeInTheDocument();
  });

  it('should show healthy mods only in healthy filter', async () => {
    render(<FindingsView {...defaultProps} />);
    // Click healthy filter chip (1 healthy mod in test data)
    const healthyChips = screen.getAllByText(/tools\.modAnalyzer\.healthy/);
    // Find the chip (in filters section) — it has the count
    const filterChip = healthyChips.find(el => el.textContent?.includes('(1)'));
    expect(filterChip).toBeTruthy();
    await userEvent.click(filterChip!);
    expect(screen.getByText('HealthyMod')).toBeInTheDocument();
  });
});
