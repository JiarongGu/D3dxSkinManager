import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { AnalysisProgress } from '../../../../../shared/types/analysis.types';

// Mock antd to avoid @rc-component/picker resolution issue in CRA Jest
vi.mock('antd', () => {
  const React = require('react');
  return {
    Progress: (props: any) => <div data-testid="progress" />,
    Tag: ({ children, ...rest }: any) => <span {...rest}>{children}</span>,
  };
});

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
  };
});

// Mock CategorySelect (complex select component)
vi.mock('../../../../../shared/components/CategorySelect', () => ({
  CategorySelect: ({ value, onChange }: { value?: string; onChange: (v?: string) => void }) => (
    <select data-testid="category-select" value={value || ''} onChange={e => onChange(e.target.value || undefined)}>
      <option value="">All</option>
      <option value="cat-1">Weapons</option>
    </select>
  ),
}));

// Must import after mocks
import { ScanView } from '../components/ScanView';

// Mock scrollIntoView (not available in JSDOM)
Element.prototype.scrollIntoView = vi.fn();

describe('ScanView', () => {
  const defaultProps = {
    progress: undefined as AnalysisProgress | undefined,
    scanning: false,
    cancelling: false,
    loading: false,
    initialFeed: undefined as Array<{ name: string; status: string }> | undefined,
    categories: [{ id: 'cat-1', name: 'Weapons', parentId: undefined, children: [] }],
    selectedCategoryId: undefined as string | undefined,
    onCategoryChange: vi.fn(),
    onStart: vi.fn(),
    onPause: vi.fn(),
    onResume: vi.fn(),
    onCancel: vi.fn(),
    onViewHistory: vi.fn(),
    sessionCount: 0,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render hero screen when not scanning', () => {
    render(<ScanView {...defaultProps} />);
    expect(screen.getByText('tools.modAnalyzer.title')).toBeInTheDocument();
    expect(screen.getByText('tools.modAnalyzer.startScan')).toBeInTheDocument();
  });

  it('should call onStart when scan button clicked', async () => {
    render(<ScanView {...defaultProps} />);
    const startBtn = screen.getByText('tools.modAnalyzer.startScan');
    await userEvent.click(startBtn);
    expect(defaultProps.onStart).toHaveBeenCalledTimes(1);
  });

  it('should show history button when sessions exist', () => {
    render(<ScanView {...defaultProps} sessionCount={3} />);
    expect(screen.getByText(/tools\.modAnalyzer\.viewHistory.*\(3\)/)).toBeInTheDocument();
  });

  it('should hide history button when no sessions', () => {
    render(<ScanView {...defaultProps} sessionCount={0} />);
    expect(screen.queryByText(/tools\.modAnalyzer\.viewHistory/)).not.toBeInTheDocument();
  });

  it('should show active scan view when scanning', () => {
    const progress: AnalysisProgress = {
      sessionId: 's1', stage: 'analyzing', current: 5, total: 10,
      currentModName: 'TestMod', status: 'running',
      healthyCount: 3, warningCount: 1, errorCount: 1,
    };
    render(<ScanView {...defaultProps} scanning={true} progress={progress} />);
    expect(screen.getByText('tools.modAnalyzer.scanRunning')).toBeInTheDocument();
    expect(screen.getByText('5 / 10')).toBeInTheDocument();
    expect(screen.getByText('50%')).toBeInTheDocument();
  });

  it('should show pause/cancel buttons during active scan', () => {
    const progress: AnalysisProgress = {
      sessionId: 's1', stage: 'analyzing', current: 3, total: 10,
      currentModName: '', status: 'running',
      healthyCount: 2, warningCount: 0, errorCount: 1,
    };
    render(<ScanView {...defaultProps} scanning={true} progress={progress} />);
    expect(screen.getByText('tools.modAnalyzer.pause')).toBeInTheDocument();
    expect(screen.getByText('tools.modAnalyzer.cancel')).toBeInTheDocument();
  });

  it('should show resume/cancel buttons when paused', () => {
    const progress: AnalysisProgress = {
      sessionId: 's1', stage: 'paused', current: 5, total: 10,
      currentModName: '', status: 'paused',
      healthyCount: 3, warningCount: 1, errorCount: 1,
    };
    render(<ScanView {...defaultProps} scanning={true} progress={progress} />);
    expect(screen.getByText('tools.modAnalyzer.scanPaused')).toBeInTheDocument();
    expect(screen.getByText('tools.modAnalyzer.resume')).toBeInTheDocument();
    expect(screen.getByText('tools.modAnalyzer.cancel')).toBeInTheDocument();
  });

  it('should show preparing state when scanning with no progress', () => {
    render(<ScanView {...defaultProps} scanning={true} />);
    // "preparing" appears in both the title and the feed area
    expect(screen.getAllByText('tools.modAnalyzer.preparing').length).toBeGreaterThanOrEqual(1);
  });

  it('should show stat pills with counts', () => {
    const progress: AnalysisProgress = {
      sessionId: 's1', stage: 'analyzing', current: 8, total: 10,
      currentModName: '', status: 'running',
      healthyCount: 5, warningCount: 2, errorCount: 1,
    };
    render(<ScanView {...defaultProps} scanning={true} progress={progress} />);
    // Stat pill values
    expect(screen.getByText('5')).toBeInTheDocument(); // healthy
    expect(screen.getByText('2')).toBeInTheDocument(); // warnings
    expect(screen.getByText('1')).toBeInTheDocument(); // errors
  });

  it('should render feature tags on hero screen', () => {
    render(<ScanView {...defaultProps} />);
    expect(screen.getByText('tools.modAnalyzer.tabs.health')).toBeInTheDocument();
    expect(screen.getByText('tools.modAnalyzer.staleOrMissing')).toBeInTheDocument();
    expect(screen.getByText('tools.modAnalyzer.tabs.duplicates')).toBeInTheDocument();
    expect(screen.getByText('tools.modAnalyzer.tabs.conflicts')).toBeInTheDocument();
  });
});
