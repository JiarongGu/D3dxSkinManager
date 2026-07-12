import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ExperimentOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { CompactButton, CompactField, CompactInput, CompactSelect, CompactSpace } from '../../../shared/components/compact';
import { FormDialog } from '../../../shared/components/dialogs/FormDialog';
import type { RemoteSourceConfig, RemoteSourceTestResult } from '../../../shared/types/remote.types';
import { RemoteSourceTestResultView } from './RemoteSourceTestResultView';
import './RemoteSourceTestDialog.css';

interface RemoteSourceTestDialogProps {
  visible: boolean;
  /** The candidate config to test (already resolved from the editor's form/raw state). */
  config: RemoteSourceConfig;
  onClose: () => void;
}

/**
 * Modal test-connection runner — makes it OBVIOUS what's happening (spinner → pass/fail) instead of an
 * inline line. Lets the user pick WHICH game/list + supply the source's params, so a parameterized
 * source is tested exactly as a specific library would run it. Auto-runs once on open with the defaults.
 */
export const RemoteSourceTestDialog: React.FC<RemoteSourceTestDialogProps> = ({ visible, config, onClose }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [listId, setListId] = useState<string>();
  const [paramValues, setParamValues] = useState<Record<string, string>>({});
  const [testing, setTesting] = useState(false);
  const [result, setResult] = useState<RemoteSourceTestResult>();

  const params = config.params ?? [];

  const run = async (list: string | undefined, values: Record<string, string>) => {
    if (!selectedProfileId) return;
    setTesting(true);
    setResult(undefined);
    try {
      setResult(await api.remote.testSource(selectedProfileId, config, list, values));
    } catch {
      setResult({ success: false, error: t('remote.testFailed'), cardCount: 0, sampleTitles: [], detailFetched: false, detailDownloads: [], detailImageCount: 0 });
    } finally {
      setTesting(false);
    }
  };

  // On open: seed the game + param defaults, then auto-run once so the modal shows a result immediately.
  useEffect(() => {
    if (!visible) return;
    const firstList = config.lists[0]?.id;
    const seeded = Object.fromEntries(
      (config.params ?? []).filter((p) => p.default != null).map((p) => [p.key, p.default as string]),
    );
    setListId(firstList);
    setParamValues(seeded);
    void run(firstList, seeded);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible]);

  return (
    <FormDialog
      visible={visible}
      title={t('remote.testDialogTitle')}
      width={560}
      onCancel={onClose}
      footer={
        <CompactSpace>
          <CompactButton onClick={onClose}>{t('common.close')}</CompactButton>
          <CompactButton type="primary" icon={<ExperimentOutlined />} loading={testing} onClick={() => void run(listId, paramValues)}>
            {t('remote.testRun')}
          </CompactButton>
        </CompactSpace>
      }
    >
      <div className="remote-test-dialog">
        {/* What's being tested — makes the modal self-explanatory. */}
        <div className="remote-test-dialog__source">
          <span className="remote-test-dialog__source-name">{config.name || config.id}</span>
          <span className="remote-test-dialog__source-url">{config.baseUrl}</span>
        </div>

        {(config.lists.length > 1 || params.length > 0) && (
          <div className="remote-test-dialog__config">
            {config.lists.length > 1 && (
              <CompactField label={t('remote.fieldGame')}>
                <CompactSelect
                  value={listId}
                  options={config.lists.map((l) => ({ value: l.id, label: l.name || l.id }))}
                  onChange={(v) => setListId(v as string)}
                  style={{ width: '100%' }}
                />
              </CompactField>
            )}
            {params.map((p) => (
              <CompactField key={p.key} label={p.label || p.key}>
                {p.type === 'select' ? (
                  <CompactSelect
                    value={paramValues[p.key] || undefined}
                    options={p.options.map((o) => ({ value: o.value, label: o.label || o.value }))}
                    onChange={(v) => setParamValues((pv) => ({ ...pv, [p.key]: (v as string) ?? '' }))}
                    style={{ width: '100%' }}
                  />
                ) : (
                  <CompactInput
                    value={paramValues[p.key] ?? ''}
                    placeholder={p.default ?? ''}
                    onChange={(e) => setParamValues((pv) => ({ ...pv, [p.key]: e.target.value }))}
                  />
                )}
              </CompactField>
            ))}
          </div>
        )}

        {/* Fixed min-height so the modal doesn't jump between the spinner and the result. */}
        <div className="remote-test-dialog__result">
          <RemoteSourceTestResultView testing={testing} result={result} />
        </div>
      </div>
    </FormDialog>
  );
};
