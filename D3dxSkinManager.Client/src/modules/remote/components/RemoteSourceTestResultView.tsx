import React from 'react';
import { useTranslation } from 'react-i18next';
import { StatusTag } from '../../../shared/components/common/StatusTag';
import type { RemoteSourceTestResult } from '../../../shared/types/remote.types';
import './RemoteSourceTestResultView.css';

interface RemoteSourceTestResultViewProps {
  /** Backend test result (success or a structured failure with `error`). */
  result?: RemoteSourceTestResult;
  /** Client-side error (e.g. invalid raw JSON before the request) — shown as a failure. */
  error?: string;
  /** True while the test request is in flight. */
  testing?: boolean;
}

/**
 * L2 presentational — a pass/fail indicator for the remote-source "Test connection" run. Turns the
 * backend {@link RemoteSourceTestResult} into an overall status + per-check StatusTags (cards / detail /
 * downloads / images) so the user sees WHAT worked, not a bare number or a toast. Pure props, no IPC.
 */
export const RemoteSourceTestResultView: React.FC<RemoteSourceTestResultViewProps> = ({ result, error, testing }) => {
  const { t } = useTranslation();

  if (testing) {
    return (
      <div className="remote-test-result" data-testid="remote-test-result">
        <StatusTag tone="processing" label={t('remote.testTesting')} />
      </div>
    );
  }

  // A client-side error, or a backend failure → red overall status + the message.
  const failed = !!error || (result && !result.success);
  if (failed) {
    const message = error ?? result?.error;
    return (
      <div className="remote-test-result remote-test-result--error" data-testid="remote-test-result">
        <StatusTag tone="error" label={t('remote.testFailed')} />
        {message && <div className="remote-test-result__message">{message}</div>}
      </div>
    );
  }

  if (!result) return null;

  const hasCards = result.cardCount > 0;
  return (
    <div className="remote-test-result remote-test-result--ok" data-testid="remote-test-result">
      <div className="remote-test-result__checks">
        <StatusTag tone="success" label={t('remote.testConnected')} />
        <StatusTag
          tone={hasCards ? 'success' : 'warning'}
          label={hasCards ? t('remote.testCards', { count: result.cardCount }) : t('remote.testNoCards')}
        />
        {result.totalPages != null && (
          <StatusTag tone="info" icon={null} label={t('remote.testPages', { count: result.totalPages })} />
        )}
        {hasCards && (
          <StatusTag
            tone={result.detailTitle ? 'success' : 'warning'}
            label={result.detailTitle ? t('remote.testDetailOk') : t('remote.testDetailNoTitle')}
          />
        )}
        {hasCards && (
          <StatusTag
            tone={result.detailDownloads.length > 0 ? 'success' : 'warning'}
            label={
              result.detailDownloads.length > 0
                ? t('remote.testDownloads', { count: result.detailDownloads.length })
                : t('remote.testNoDownloads')
            }
          />
        )}
        {hasCards && result.detailImageCount > 0 && (
          <StatusTag tone="info" icon={null} label={t('remote.testImages', { count: result.detailImageCount })} />
        )}
      </div>
      {result.sampleTitles.length > 0 && (
        <div className="remote-test-result__samples">{result.sampleTitles.join(' · ')}</div>
      )}
    </div>
  );
};
