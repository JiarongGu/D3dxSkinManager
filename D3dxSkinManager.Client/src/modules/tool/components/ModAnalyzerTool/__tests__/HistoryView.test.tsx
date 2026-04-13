import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { AnalysisSessionSummary } from '../../../../../shared/types/analysis.types';

// Mock antd to avoid @rc-component/picker resolution issue in CRA Jest
jest.mock('antd', () => {
  const React = require('react');
  return {
    Empty: ({ description }: any) => <div>{description}</div>,
    Tag: ({ children, ...rest }: any) => <span {...rest}>{children}</span>,
  };
});

// Mock i18n
jest.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

// Mock compact components
jest.mock('../../../../../shared/components/compact', () => {
  const React = require('react');
  return {
    CompactButton: ({ children, onClick, danger, disabled, ...rest }: any) => (
      <button onClick={onClick} disabled={disabled} {...rest}>{children}</button>
    ),
    CompactCard: ({ children, onClick, ...rest }: any) => (
      <div onClick={onClick} {...rest}>{children}</div>
    ),
  };
});

// Mock ConfirmDialog
jest.mock('../../../../../shared/components/dialogs/ConfirmDialog', () => ({
  ConfirmDialog: ({ visible, onOk, onCancel }: { visible: boolean; onOk: () => void; onCancel: () => void }) =>
    visible ? (
      <div data-testid="confirm-dialog">
        <button onClick={onOk}>Confirm</button>
        <button onClick={onCancel}>Cancel</button>
      </div>
    ) : null,
}));

// Must import after mocks
import { HistoryView } from '../components/HistoryView';

const makeSessions = (): AnalysisSessionSummary[] => [
  {
    id: 'session-1', status: 'completed', totalMods: 10, analyzedCount: 10,
    healthyCount: 7, warningCount: 2, errorCount: 1,
    identicalCount: 1, textureVariantCount: 0, conflictCount: 0,
    startedAt: '2026-04-13T10:00:00Z', completedAt: '2026-04-13T10:05:00Z',
  },
  {
    id: 'session-2', status: 'running', totalMods: 20, analyzedCount: 5,
    healthyCount: 3, warningCount: 1, errorCount: 1,
    identicalCount: 0, textureVariantCount: 0, conflictCount: 0,
    startedAt: '2026-04-13T11:00:00Z',
  },
];

describe('HistoryView', () => {
  const defaultProps = {
    sessions: makeSessions(),
    onViewSession: jest.fn(),
    onDeleteSession: jest.fn(),
    onClearAll: jest.fn(),
    onBack: jest.fn(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render session count', () => {
    render(<HistoryView {...defaultProps} />);
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('should render back button', () => {
    render(<HistoryView {...defaultProps} />);
    expect(screen.getByText('tools.modAnalyzer.back')).toBeInTheDocument();
  });

  it('should call onBack when back button clicked', async () => {
    render(<HistoryView {...defaultProps} />);
    await userEvent.click(screen.getByText('tools.modAnalyzer.back'));
    expect(defaultProps.onBack).toHaveBeenCalledTimes(1);
  });

  it('should show empty state when no sessions', () => {
    render(<HistoryView {...defaultProps} sessions={[]} />);
    expect(screen.getByText('tools.modAnalyzer.noHistory')).toBeInTheDocument();
  });

  it('should show clear all button when sessions exist', () => {
    render(<HistoryView {...defaultProps} />);
    expect(screen.getByText('tools.modAnalyzer.clearAll')).toBeInTheDocument();
  });

  it('should hide clear all button when no sessions', () => {
    render(<HistoryView {...defaultProps} sessions={[]} />);
    expect(screen.queryByText('tools.modAnalyzer.clearAll')).not.toBeInTheDocument();
  });

  it('should show confirmation dialog on clear all click', async () => {
    render(<HistoryView {...defaultProps} />);
    await userEvent.click(screen.getByText('tools.modAnalyzer.clearAll'));
    expect(screen.getByTestId('confirm-dialog')).toBeInTheDocument();
  });

  it('should call onClearAll when confirmed', async () => {
    render(<HistoryView {...defaultProps} />);
    await userEvent.click(screen.getByText('tools.modAnalyzer.clearAll'));
    await userEvent.click(screen.getByText('Confirm'));
    expect(defaultProps.onClearAll).toHaveBeenCalledTimes(1);
  });

  it('should render session cards with stats', () => {
    render(<HistoryView {...defaultProps} />);
    // Both sessions should show their mod counts
    expect(screen.getByText(/10\/10/)).toBeInTheDocument();
    expect(screen.getByText(/5\/20/)).toBeInTheDocument();
  });
});
