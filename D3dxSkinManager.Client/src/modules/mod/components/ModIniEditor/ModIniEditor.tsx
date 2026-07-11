import React, { useState, useCallback, useEffect } from 'react';
import { Collapse, Empty, Tooltip, Spin, Tabs } from 'antd';
import {
  LockOutlined, CheckOutlined, CloseOutlined, SettingOutlined, ApartmentOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { notification } from '../../../../shared/utils/notification';
import { CompactIconButton, CompactInput, CompactSelect, CompactSwitch, CompactInputNumber } from '../../../../shared/components/compact';
import { StatusTag } from '../../../../shared/components/common/StatusTag';
import { KeyCaptureInput } from '../../../../shared/components/common/KeyCaptureInput';
import type { ModInfo } from '../../../../shared/types/mod.types';
import type { ModIniFile, ModIniSection, ModIniEntry } from '../../../../shared/types/modIni.types';
import './ModIniEditor.css';

interface ModIniEditorProps {
  visible: boolean;
  mod?: ModInfo;
  onClose: () => void;
}

// ---- Friendly-label helpers (non-tech presentation; raw key kept in a tooltip) -----------------

/** Turn a CamelCase/snake token into spaced Title Case words. */
function humanize(token: string): string {
  return token
    .replace(/[_\-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

/** A friendly label for an entry key (e.g. "global persist $swapkey0" → "Swapkey0", "key" → "Hotkey"). */
function friendlyKey(key: string, t: (k: string) => string): string {
  const lower = key.trim().toLowerCase();
  const known: Record<string, string> = {
    key: t('modIni.field.key'),
    type: t('modIni.field.type'),
    condition: t('modIni.field.condition'),
    back: t('modIni.field.back'),
    delay: t('modIni.field.delay'),
    wrap: t('modIni.field.wrap'),
    smart: t('modIni.field.smart'),
    transition: t('modIni.field.transition'),
    transition_type: t('modIni.field.transitionType'),
    release_delay: t('modIni.field.releaseDelay'),
    release_transition: t('modIni.field.releaseTransition'),
    release_transition_type: t('modIni.field.releaseTransitionType'),
  };
  if (known[lower]) return known[lower];
  // Strip 3DMigoto variable qualifiers and the $ sigil, then humanize what's left.
  const stripped = key
    .replace(/\bglobal\b/gi, '')
    .replace(/\bpersist\b/gi, '')
    .replace(/\$/g, '')
    .trim();
  return humanize(stripped) || key;
}

/** A friendly section title ([KeySwap0] → "Toggle: Swap0", [Constants] → "Variables"). */
function friendlySection(name: string, t: (k: string) => string): string {
  if (/^key/i.test(name)) return `${t('modIni.toggle')}: ${humanize(name.replace(/^key/i, '')) || name}`;
  if (/^constants$/i.test(name)) return t('modIni.variables');
  return name;
}

/**
 * General mod config editor (slide-in). Left vertical tab per .ini file; within a file the tunable
 * settings (toggles / variables) are shown with friendly labels, advanced 3DMigoto plumbing (hashes,
 * overrides, resources, command lists) is collapsed + read-only. Each save patches just that one file
 * into the archive via the fast single-file path (backend re-validates editability).
 */
export const ModIniEditor: React.FC<ModIniEditorProps> = ({ visible, mod, onClose }) => {
  const { t } = useTranslation();
  useSlideInScreen({
    visible,
    title: mod ? t('modIni.title', { name: mod.name }) : t('modIni.titleShort'),
    content: mod ? <ModIniEditorInner mod={mod} /> : null,
    width: '72%',
    onClose,
  });
  return null;
};

const ModIniEditorInner: React.FC<{ mod: ModInfo }> = ({ mod }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [files, setFiles] = useState<ModIniFile[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    if (!selectedProfileId) return;
    setLoading(true);
    try {
      setFiles(await api.mod.getModIniFiles(selectedProfileId, mod.id));
    } catch (error) {
      handleError(error);
    } finally {
      setLoading(false);
    }
  }, [selectedProfileId, mod.id]);

  useEffect(() => { void load(); }, [load]);

  // Commit one entry, then reflect the new value locally (so the row's draft resets and goes clean).
  const saveEntry = useCallback(
    async (relativePath: string, lineIndex: number, newValue: string) => {
      if (!selectedProfileId) return;
      await api.mod.updateModIniEntry(selectedProfileId, mod.id, relativePath, lineIndex, newValue);
      setFiles((prev) =>
        prev.map((f) =>
          f.relativePath !== relativePath
            ? f
            : {
                ...f,
                sections: f.sections.map((s) => ({
                  ...s,
                  entries: s.entries.map((e) => (e.lineIndex === lineIndex ? { ...e, value: newValue } : e)),
                })),
              },
        ),
      );
      notification.success(t('modIni.saved'));
    },
    [selectedProfileId, mod.id, t],
  );

  if (loading) return <div className="mod-ini-editor__center"><Spin /></div>;
  if (files.length === 0) {
    return (
      <div className="mod-ini-editor__center">
        <Empty description={t('modIni.notExtracted')} />
      </div>
    );
  }

  // Land on the first file that actually has something to tune (skip technical-only files like
  // a pure-TextureOverride .ini), so the user sees editable settings immediately.
  const hasTunable = (f: ModIniFile) => f.sections.some((s) => !s.advanced);
  const defaultTab = (files.find(hasTunable) ?? files[0]).relativePath;

  return (
    <div className="mod-ini-editor">
      <p className="mod-ini-editor__hint">{t('modIni.hint')}</p>
      <Tabs
        tabPosition="left"
        defaultActiveKey={defaultTab}
        className="mod-ini-editor__tabs"
        items={files.map((file) => {
          const tunable = hasTunable(file);
          return {
            key: file.relativePath,
            label: (
              <Tooltip title={file.relativePath} placement="right">
                <span className={`mod-ini-editor__tab${tunable ? '' : ' mod-ini-editor__tab--technical'}`}>
                  <span className="mod-ini-editor__tab-name">{file.fileName}</span>
                  {file.namespace && (
                    <span className="mod-ini-editor__tab-ns">
                      <ApartmentOutlined /> {file.namespace}
                    </span>
                  )}
                </span>
              </Tooltip>
            ),
            children: <IniFileBody file={file} onSave={saveEntry} />,
          };
        })}
      />
    </div>
  );
};

/** A [Constants] default linked to the toggle ([Key*] section) whose cycle list drives it. */
interface LinkedDefault {
  entry: ModIniEntry;
  /** The values the toggle's key cycles through (the $var's meaningful domain). */
  cycleValues: string[];
}

/** Normalize a variable reference: strip global/persist/local qualifiers, keep the $name. */
function varName(key: string): string {
  return key.toLowerCase().replace(/\b(global|persist|local)\b/g, '').trim();
}

const IniFileBody: React.FC<{
  file: ModIniFile;
  onSave: (relativePath: string, lineIndex: number, newValue: string) => Promise<void>;
}> = ({ file, onSave }) => {
  const { t } = useTranslation();
  const tunable = file.sections.filter((s) => !s.advanced);
  const advanced = file.sections.filter((s) => s.advanced);

  // PER-TOGGLE GROUPING: a [Key*] section's `$var = a,b,…` line is the cycle list of the variable
  // it drives; that variable's DEFAULT lives in [Constants]. Surface the default INSIDE the
  // toggle's card (as a select over the cycle values) and drop it from the plain Variables group,
  // so one toggle is edited in one place.
  const constants = tunable.filter((s) => /^constants$/i.test(s.name));
  const constantsByVar = new Map<string, ModIniEntry>();
  for (const c of constants)
    for (const e of c.entries)
      if (e.key.includes('$')) constantsByVar.set(varName(e.key), e);

  const linkedBySection = new Map<string, LinkedDefault[]>();
  const claimed = new Set<number>(); // lineIndexes of Constants entries shown inside a toggle
  for (const s of tunable) {
    if (!/^key/i.test(s.name)) continue;
    for (const e of s.entries) {
      if (!e.key.trim().startsWith('$')) continue;
      const def = constantsByVar.get(varName(e.key));
      if (!def || claimed.has(def.lineIndex)) continue;
      claimed.add(def.lineIndex);
      const list = linkedBySection.get(s.name) ?? [];
      list.push({ entry: def, cycleValues: e.value.split(',').map((v) => v.trim()).filter(Boolean) });
      linkedBySection.set(s.name, list);
    }
  }

  return (
    <div className="ini-file-body">
      {file.namespace && (
        <div className="ini-file-body__namespace">
          <ApartmentOutlined /> {t('modIni.namespace')}: <code>{file.namespace}</code>
          <span className="ini-file-body__namespace-hint">{t('modIni.namespaceHint')}</span>
        </div>
      )}

      {tunable.length === 0 && advanced.length > 0 && (
        <p className="ini-file-body__none">{t('modIni.noTunable')}</p>
      )}

      {tunable.map((section) => (
        <IniSection
          key={section.name}
          file={file}
          section={section}
          onSave={onSave}
          linkedDefaults={linkedBySection.get(section.name)}
          omitLineIndexes={/^constants$/i.test(section.name) ? claimed : undefined}
        />
      ))}

      {advanced.length > 0 && (
        <Collapse
          ghost
          className="ini-file-body__advanced"
          items={[
            {
              key: 'advanced',
              label: (
                <span className="ini-section__advanced-label">
                  <SettingOutlined /> {t('modIni.advanced', { count: advanced.length })}
                </span>
              ),
              children: advanced.map((section) => (
                <IniSection key={section.name} file={file} section={section} onSave={onSave} />
              )),
            },
          ]}
        />
      )}
    </div>
  );
};

const IniSection: React.FC<{
  file: ModIniFile;
  section: ModIniSection;
  onSave: (relativePath: string, lineIndex: number, newValue: string) => Promise<void>;
  /** [Constants] defaults driven by THIS toggle — shown here instead of the Variables group. */
  linkedDefaults?: LinkedDefault[];
  /** Constants entries claimed by a toggle — hidden from the plain Variables listing. */
  omitLineIndexes?: Set<number>;
}> = ({ file, section, onSave, linkedDefaults, omitLineIndexes }) => {
  const { t } = useTranslation();
  const entries = omitLineIndexes
    ? section.entries.filter((e) => !omitLineIndexes.has(e.lineIndex))
    : section.entries;

  // A Variables group whose vars ALL belong to toggles has nothing left to show.
  if (entries.length === 0 && (linkedDefaults?.length ?? 0) === 0) return null;

  return (
    <div className={`ini-section${section.advanced ? ' ini-section--advanced' : ''}`}>
      <div className="ini-section__header">
        <span className="ini-section__name">{section.advanced ? `[${section.name}]` : friendlySection(section.name, t)}</span>
        {section.advanced && <StatusTag tone="neutral" label={t('modIni.readOnly')} />}
      </div>
      {entries.map((entry) => (
        <IniRow
          key={entry.lineIndex}
          entry={entry}
          advanced={section.advanced}
          onSave={(v) => onSave(file.relativePath, entry.lineIndex, v)}
        />
      ))}
      {(linkedDefaults ?? []).map((link) => (
        <IniRow
          key={`default-${link.entry.lineIndex}`}
          entry={link.entry}
          advanced={false}
          labelOverride={`${t('modIni.field.default')} (${friendlyKey(link.entry.key, t)})`}
          selectValues={link.cycleValues}
          onSave={(v) => onSave(file.relativePath, link.entry.lineIndex, v)}
        />
      ))}
    </div>
  );
};

const TYPE_OPTIONS = ['cycle', 'hold', 'toggle'];
// [Key*] millisecond easings (delay/transition families) — number fields, not free text.
const MS_KEYS = new Set(['delay', 'transition', 'release_delay', 'release_transition']);
// Easing curves 3DMigoto accepts for transition_type / release_transition_type (docs: key page).
const TRANSITION_TYPE_OPTIONS = ['linear', 'cosine'];

const IniRow: React.FC<{
  entry: ModIniEntry;
  advanced: boolean;
  onSave: (value: string) => Promise<void>;
  /** Custom label (per-toggle default rows override the plain var name). */
  labelOverride?: string;
  /** When set, render a Select over these values (a toggle var's cycle list — its whole domain). */
  selectValues?: string[];
}> = ({ entry, advanced, onSave, labelOverride, selectValues }) => {
  const { t } = useTranslation();
  const [draft, setDraft] = useState(entry.value);
  const [saving, setSaving] = useState(false);
  useEffect(() => setDraft(entry.value), [entry.value]);
  const dirty = draft !== entry.value;
  // Advanced rows keep the raw key; tunable rows get a friendly label (raw key in tooltip).
  const label = labelOverride ?? (advanced ? entry.key : friendlyKey(entry.key, t));
  const keyLower = entry.key.trim().toLowerCase();

  // Friendly control: the toggle Mode (type=cycle/hold/toggle) → a Select. Variable defaults are NOT
  // booleans — a $var's value cycles through the values its key defines (0,1,2,3…), so it stays a
  // plain input rather than a misleading on/off switch.
  const isMode = !advanced && keyLower === 'type';
  // Genuine [Key] booleans (default true): cycle wrap-around + smart resync. Render as a Switch —
  // unlike a $var default (which cycles through its key's value list), these are truly on/off.
  const isBoolean = !advanced && (keyLower === 'wrap' || keyLower === 'smart');
  // Hotkey rows: capture a chord visually instead of typing raw VK text. (key = main, back = reverse-cycle)
  const isHotkey = !advanced && (keyLower === 'key' || keyLower === 'back');
  // Millisecond easings (delay/transition families) → a number field with an ms suffix.
  const isMs = !advanced && MS_KEYS.has(keyLower);
  // Easing curve → a Select over the values 3DMigoto accepts.
  const isTransitionType = !advanced && (keyLower === 'transition_type' || keyLower === 'release_transition_type');

  const commitValue = async (value: string) => {
    if (saving) return;
    setSaving(true);
    try {
      await onSave(value);
    } catch (error) {
      handleError(error);
      setDraft(entry.value);
    } finally {
      setSaving(false);
    }
  };

  if (!entry.editable) {
    return (
      <div className="ini-row ini-row--locked">
        <span className="ini-row__key">{label}</span>
        <span className="ini-row__value-readonly">{entry.value || '—'}</span>
        <Tooltip title={t(`modIni.lock.${entry.lockReason ?? 'advancedSection'}`)}>
          <LockOutlined className="ini-row__lock" />
        </Tooltip>
      </div>
    );
  }

  const labelEl = (
    <Tooltip title={entry.key !== label ? entry.key : undefined} placement="topLeft">
      <span className="ini-row__key">{label}</span>
    </Tooltip>
  );

  // Per-toggle default: the $var's value is one of the values its key cycles through — a Select
  // over that exact domain (never a boolean switch; see 3dmigoto-ini-interface.md).
  if (selectValues && selectValues.length > 0 && entry.editable) {
    const options = Array.from(new Set([entry.value, ...selectValues]))
      .filter((v) => v !== '')
      .map((v) => ({ value: v, label: v }));
    return (
      <div className="ini-row ini-row--linked">
        {labelEl}
        <CompactSelect
          className="ini-row__input"
          size="small"
          value={entry.value || undefined}
          disabled={saving}
          loading={saving}
          options={options}
          onChange={(v) => void commitValue(v)}
        />
        <span className="ini-row__actions" />
      </div>
    );
  }

  if (isMode) {
    const options = Array.from(new Set([entry.value, ...TYPE_OPTIONS])).map((v) => ({ value: v, label: v }));
    return (
      <div className="ini-row">
        {labelEl}
        <CompactSelect
          className="ini-row__input"
          size="small"
          value={entry.value}
          disabled={saving}
          loading={saving}
          options={options}
          onChange={(v) => void commitValue(v)}
        />
        <span className="ini-row__actions" />
      </div>
    );
  }

  if (isBoolean) {
    // Default is "true" when unset/non-false (3DMigoto default for wrap/smart).
    const on = entry.value.trim().toLowerCase() !== 'false';
    return (
      <div className="ini-row">
        {labelEl}
        <span className="ini-row__input">
          <CompactSwitch size="small" checked={on} loading={saving} disabled={saving} onChange={(v) => void commitValue(v ? 'true' : 'false')} />
        </span>
        <span className="ini-row__actions" />
      </div>
    );
  }

  if (isTransitionType) {
    const options = Array.from(new Set([entry.value, ...TRANSITION_TYPE_OPTIONS]))
      .filter(Boolean)
      .map((v) => ({ value: v, label: v }));
    return (
      <div className="ini-row">
        {labelEl}
        <CompactSelect
          className="ini-row__input"
          size="small"
          value={entry.value || undefined}
          placeholder="linear"
          disabled={saving}
          loading={saving}
          options={options}
          onChange={(v) => void commitValue(v)}
        />
        <span className="ini-row__actions" />
      </div>
    );
  }

  if (isMs) {
    return (
      <div className="ini-row">
        {labelEl}
        <CompactInputNumber
          className="ini-row__input"
          size="small"
          min={0}
          step={50}
          value={draft === '' ? undefined : Number(draft)}
          disabled={saving}
          suffix="ms"
          onChange={(v) => setDraft(v === null || v === undefined ? '' : String(v))}
          onPressEnter={() => void commitValue(draft)}
        />
        <span className="ini-row__actions">
          {dirty && (
            <>
              <CompactIconButton tone="success" icon={<CheckOutlined />} loading={saving} title={t('common.save')} onClick={() => void commitValue(draft)} />
              <CompactIconButton tone="danger" icon={<CloseOutlined />} disabled={saving} title={t('common.cancel')} onClick={() => setDraft(entry.value)} />
            </>
          )}
        </span>
      </div>
    );
  }

  if (isHotkey) {
    return (
      <div className="ini-row">
        {labelEl}
        <KeyCaptureInput className="ini-row__input" value={draft} disabled={saving} onChange={setDraft} />
        <span className="ini-row__actions">
          {dirty && (
            <>
              <CompactIconButton tone="success" icon={<CheckOutlined />} loading={saving} title={t('common.save')} onClick={() => void commitValue(draft)} />
              <CompactIconButton tone="danger" icon={<CloseOutlined />} disabled={saving} title={t('common.cancel')} onClick={() => setDraft(entry.value)} />
            </>
          )}
        </span>
      </div>
    );
  }

  return (
    <div className="ini-row">
      {labelEl}
      <CompactInput
        className="ini-row__input"
        size="small"
        value={draft}
        disabled={saving}
        onChange={(e) => setDraft(e.target.value)}
        onPressEnter={() => void commitValue(draft)}
      />
      <span className="ini-row__actions">
        {dirty && (
          <>
            <CompactIconButton
              tone="success"
              icon={<CheckOutlined />}
              loading={saving}
              title={t('common.save')}
              onClick={() => void commitValue(draft)}
            />
            <CompactIconButton
              tone="danger"
              icon={<CloseOutlined />}
              disabled={saving}
              title={t('common.cancel')}
              onClick={() => setDraft(entry.value)}
            />
          </>
        )}
      </span>
    </div>
  );
};
