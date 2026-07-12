import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CompactButton, CompactCheckbox, CompactSpace } from '../../../shared/components/compact';
import { FormDialog } from '../../../shared/components/dialogs/FormDialog';
import type { RemoteSourceConfig } from '../../../shared/types/remote.types';
import './RemoteSourceCompareDialog.css';

interface RemoteSourceCompareDialogProps {
  visible: boolean;
  /** The current (edited) config. */
  current: RemoteSourceConfig;
  /** The shipped res default to compare against. */
  def: RemoteSourceConfig;
  /** Apply the selected fields' DEFAULT values into a copy of `current` and hand it back. */
  onRevert: (reverted: RemoteSourceConfig) => void;
  onCancel: () => void;
}

/** A leaf value → its string form, with ALL empty-ish values (undefined / null / '' / [] / {})
 *  collapsed to '' so "empty" reads the same however it was serialized. */
const asText = (v: unknown): string => {
  if (v === undefined || v === null || v === '') return '';
  if (Array.isArray(v)) return v.length === 0 ? '' : JSON.stringify(v);
  if (typeof v === 'object') return Object.keys(v as object).length === 0 ? '' : JSON.stringify(v);
  return typeof v === 'string' ? v : JSON.stringify(v);
};

/** Two values are "the same" when their normalized text matches — so '' vs undefined vs [] is NOT a
 *  change (the res default omits nulls while the current config carries empty strings). */
const eq = (a: unknown, b: unknown): boolean => asText(a) === asText(b);

/** Index a typed config by dynamic key (the diff walks arbitrary fields). */
const rec = (c: RemoteSourceConfig): Record<string, unknown> => c as unknown as Record<string, unknown>;

/** Split two strings into [common prefix][changed middle][common suffix] so the changed span can be
 *  highlighted on each side (cheap prefix/suffix diff — reads well for URLs / regex tweaks). */
const splitDiff = (a: string, b: string) => {
  const min = Math.min(a.length, b.length);
  let pre = 0;
  while (pre < min && a[pre] === b[pre]) pre++;
  let suf = 0;
  while (suf < min - pre && a[a.length - 1 - suf] === b[b.length - 1 - suf]) suf++;
  return {
    pre: a.slice(0, pre),
    aMid: a.slice(pre, a.length - suf),
    bMid: b.slice(pre, b.length - suf),
    suf: a.slice(a.length - suf),
  };
};

/** One value cell with its changed span highlighted (`mid`). Empty → an em dash. */
const DiffValue: React.FC<{ pre: string; mid: string; suf: string; empty: boolean }> = ({ pre, mid, suf, empty }) => (
  <code className="remote-compare__value-text">
    {empty ? '—' : (
      <>
        {pre}
        {mid && <mark className="remote-compare__hl">{mid}</mark>}
        {suf}
      </>
    )}
  </code>
);

/**
 * L2 — "Compare with default": lists every field where the customized source differs from its shipped
 * res default, and lets the user tick which to REVERT to default. Reverting = copy the default value in;
 * saving then drops it from the sparse overlay (so it inherits res again). Pure props + local selection
 * state; the parent owns the config + the save. (remote-library-redesign.md — per-field re-sync.)
 */
export const RemoteSourceCompareDialog: React.FC<RemoteSourceCompareDialogProps> = ({ visible, current, def, onRevert, onCancel }) => {
  const { t } = useTranslation();
  const [selected, setSelected] = useState<Record<string, boolean>>({});

  // Fields that differ from default (id is identity, never revertable).
  const changedKeys = useMemo(() => {
    const keys = new Set<string>([...Object.keys(current), ...Object.keys(def)]);
    keys.delete('id');
    return [...keys].filter((k) => !eq(rec(current)[k], rec(def)[k]));
  }, [current, def]);

  const toggle = (key: string) => setSelected((s) => ({ ...s, [key]: !s[key] }));

  const allSelected = changedKeys.length > 0 && changedKeys.every((k) => selected[k]);
  const someSelected = changedKeys.some((k) => selected[k]);
  const toggleAll = () =>
    setSelected(allSelected ? {} : Object.fromEntries(changedKeys.map((k) => [k, true])));

  /** Copy the DEFAULT value of `keys` into a copy of current, hand it back (saving drops them from the overlay). */
  const revert = (keys: string[]) => {
    const reverted: Record<string, unknown> = { ...rec(current) };
    for (const key of keys) reverted[key] = rec(def)[key];
    onRevert(reverted as unknown as RemoteSourceConfig);
  };

  return (
    <FormDialog
      visible={visible}
      title={t('remote.compareTitle')}
      width={720}
      onCancel={onCancel}
      footer={
        <CompactSpace>
          <CompactButton onClick={onCancel}>{t('common.cancel')}</CompactButton>
          {/* Take all = sync every differing field to default; or revert only the checked ones. */}
          <CompactButton disabled={changedKeys.length === 0} onClick={() => revert(changedKeys)}>
            {t('remote.compareTakeAll')}
          </CompactButton>
          <CompactButton type="primary" disabled={!someSelected} onClick={() => revert(changedKeys.filter((k) => selected[k]))}>
            {t('remote.compareRevert')}
          </CompactButton>
        </CompactSpace>
      }
    >
      {changedKeys.length === 0 ? (
        <div className="remote-compare__empty">{t('remote.compareNoChanges')}</div>
      ) : (
        <div className="remote-compare">
          <div className="remote-compare__toolbar">
            <CompactCheckbox
              checked={allSelected}
              indeterminate={someSelected && !allSelected}
              onChange={toggleAll}
            >
              {t('remote.compareSelectAll')}
            </CompactCheckbox>
            <span className="remote-compare__hint">{t('remote.compareHint')}</span>
          </div>
          {/* Column legend — Yours (what you'll replace) vs Default (what gets applied). */}
          <div className="remote-compare__legend">
            <span className="remote-compare__legend-item remote-compare__legend-item--yours">{t('remote.compareYours')}</span>
            <span className="remote-compare__legend-item remote-compare__legend-item--default">{t('remote.compareDefault')}</span>
          </div>
          {/* Scroll only the rows — toolbar + legend stay put no matter how many fields differ. */}
          <div className="remote-compare__list">
            {changedKeys.map((key) => {
              const curText = asText(rec(current)[key]);
              const defText = asText(rec(def)[key]);
              const d = splitDiff(curText, defText);
              return (
                <div key={key} className="remote-compare__row" data-testid={`compare-row-${key}`}>
                  <CompactCheckbox checked={!!selected[key]} onChange={() => toggle(key)}>
                    <span className="remote-compare__field">{key}</span>
                  </CompactCheckbox>
                  <div className="remote-compare__cols">
                    <div className="remote-compare__col remote-compare__col--yours">
                      <DiffValue pre={d.pre} mid={d.aMid} suf={d.suf} empty={curText === ''} />
                    </div>
                    <div className="remote-compare__col remote-compare__col--default">
                      <DiffValue pre={d.pre} mid={d.bMid} suf={d.suf} empty={defText === ''} />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </FormDialog>
  );
};
