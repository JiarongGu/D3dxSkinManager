import React, { useState, useCallback, useEffect } from 'react';
import { Collapse, Empty, Tooltip, Input, Spin, Tabs, Select } from 'antd';
import {
  LockOutlined, CheckOutlined, CloseOutlined, SettingOutlined, ApartmentOutlined,
} from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { handleError } from '../../../../shared/utils/errorHandler';
import { notification } from '../../../../shared/utils/notification';
import { CompactIconButton } from '../../../../shared/components/compact';
import { StatusTag } from '../../../../shared/components/common/StatusTag';
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

const IniFileBody: React.FC<{
  file: ModIniFile;
  onSave: (relativePath: string, lineIndex: number, newValue: string) => Promise<void>;
}> = ({ file, onSave }) => {
  const { t } = useTranslation();
  const tunable = file.sections.filter((s) => !s.advanced);
  const advanced = file.sections.filter((s) => s.advanced);

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
        <IniSection key={section.name} file={file} section={section} onSave={onSave} />
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
}> = ({ file, section, onSave }) => {
  const { t } = useTranslation();
  return (
    <div className={`ini-section${section.advanced ? ' ini-section--advanced' : ''}`}>
      <div className="ini-section__header">
        <span className="ini-section__name">{section.advanced ? `[${section.name}]` : friendlySection(section.name, t)}</span>
        {section.advanced && <StatusTag tone="neutral" label={t('modIni.readOnly')} />}
      </div>
      {section.entries.map((entry) => (
        <IniRow
          key={entry.lineIndex}
          entry={entry}
          advanced={section.advanced}
          onSave={(v) => onSave(file.relativePath, entry.lineIndex, v)}
        />
      ))}
    </div>
  );
};

const TYPE_OPTIONS = ['cycle', 'hold', 'toggle'];

const IniRow: React.FC<{
  entry: ModIniEntry;
  advanced: boolean;
  onSave: (value: string) => Promise<void>;
}> = ({ entry, advanced, onSave }) => {
  const { t } = useTranslation();
  const [draft, setDraft] = useState(entry.value);
  const [saving, setSaving] = useState(false);
  useEffect(() => setDraft(entry.value), [entry.value]);
  const dirty = draft !== entry.value;
  // Advanced rows keep the raw key; tunable rows get a friendly label (raw key in tooltip).
  const label = advanced ? entry.key : friendlyKey(entry.key, t);
  const keyLower = entry.key.trim().toLowerCase();

  // Friendly control: the toggle Mode (type=cycle/hold/toggle) → a Select. Variable defaults are NOT
  // booleans — a $var's value cycles through the values its key defines (0,1,2,3…), so it stays a
  // plain input rather than a misleading on/off switch.
  const isMode = !advanced && keyLower === 'type';

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

  if (isMode) {
    const options = Array.from(new Set([entry.value, ...TYPE_OPTIONS])).map((v) => ({ value: v, label: v }));
    return (
      <div className="ini-row">
        {labelEl}
        <Select
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

  return (
    <div className="ini-row">
      {labelEl}
      <Input
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
