import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DeleteOutlined, DiffOutlined, ExperimentOutlined, PlusOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { canonicalJson } from '../../../shared/utils/canonicalJson';
import {
  CompactButton,
  CompactField,
  CompactIconButton,
  CompactInput,
  CompactSection,
  CompactSelect,
  CompactSwitch,
  CompactTextArea,
} from '../../../shared/components/compact';
import type { RemoteSourceConfig, RemoteSourceInfo } from '../../../shared/types/remote.types';
import { RemoteSourceTestDialog } from './RemoteSourceTestDialog';
import { RemoteSourceCompareDialog } from './RemoteSourceCompareDialog';
import './RemoteSourceEditor.css';

interface RemoteSourceEditorProps {
  /** The config to edit; undefined = a fresh (blank http) adapter. */
  initial?: RemoteSourceConfig;
  /** Origin of the edited source — enables "compare with default" only for a customized (res+overlay) one. */
  origin?: RemoteSourceInfo['origin'];
  onCancel: () => void;
  onSaved: (saved: RemoteSourceConfig) => void;
}

const tryParse = (text: string): RemoteSourceConfig | undefined => {
  try {
    return JSON.parse(text) as RemoteSourceConfig;
  } catch {
    return undefined;
  }
};

const BLANK: RemoteSourceConfig = {
  id: '',
  name: '',
  baseUrl: '',
  engine: 'http',
  fetcher: 'http',
  lists: [{ id: '', name: '' }],
  listUrlFirstPage: '',
  listUrlTemplate: '',
  searchUrlTemplate: '',
  cardPattern: '',
  cardScopePattern: '',
  totalPagesPattern: '',
  detailTitlePattern: '',
  detailImagePattern: '',
  downloadLinkPattern: '',
  entryIdPattern: '',
  imageDatePattern: '',
  titleTagPattern: '',
  resolvers: [],
};

/**
 * Form-based editor for a remote-library site adapter (replaces raw-JSON editing). Fields adapt to the
 * `engine`: the "http" engine shows the HTML regex/URL-template fields; the "gamebanana" JSON engine
 * hides them (it only needs base URL + game-id lists). A "Test" button runs the candidate config against
 * the live site; an "Advanced" toggle drops to raw JSON for anything the form doesn't cover.
 */
export const RemoteSourceEditor: React.FC<RemoteSourceEditorProps> = ({ initial, origin, onCancel, onSaved }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [cfg, setCfg] = useState<RemoteSourceConfig>(() => ({ ...BLANK, ...initial }));
  const [advanced, setAdvanced] = useState(false);
  const [rawText, setRawText] = useState('');
  const [saving, setSaving] = useState(false);
  // Test-connection runs in a MODAL (obvious spinner → pass/fail). testConfig = the snapshot to test
  // (undefined = closed); the dialog picks the game/params and runs.
  const [testConfig, setTestConfig] = useState<RemoteSourceConfig>();
  // "Compare with default" (per-field re-sync) — only for a customized (res + overlay) source.
  const [comparing, setComparing] = useState(false);
  const [defaultConfig, setDefaultConfig] = useState<RemoteSourceConfig>();

  const isGameBanana = (cfg.engine ?? 'http') === 'gamebanana';
  const isNew = !initial;

  // Baseline = the config as opened; dirty-tracks so Save is disabled when nothing changed (user ask).
  const baseline = useMemo(() => ({ ...BLANK, ...initial }), [initial]);
  const currentConfig = useMemo(() => (advanced ? tryParse(rawText) : cfg), [advanced, rawText, cfg]);
  const dirty = useMemo(
    () => (currentConfig ? canonicalJson(currentConfig) !== canonicalJson(baseline) : true),
    [currentConfig, baseline],
  );

  const set = <K extends keyof RemoteSourceConfig>(key: K, value: RemoteSourceConfig[K]) =>
    setCfg((c) => ({ ...c, [key]: value }));

  // --- lists (games) ---
  const setList = (i: number, field: 'id' | 'name', value: string) =>
    setCfg((c) => ({ ...c, lists: c.lists.map((l, idx) => (idx === i ? { ...l, [field]: value } : l)) }));
  const addList = () => setCfg((c) => ({ ...c, lists: [...c.lists, { id: '', name: '' }] }));
  const removeList = (i: number) => setCfg((c) => ({ ...c, lists: c.lists.filter((_, idx) => idx !== i) }));

  // --- resolvers (download hosts) ---
  const setResolver = (i: number, field: 'match' | 'type' | 'name', value: string) =>
    setCfg((c) => ({ ...c, resolvers: c.resolvers.map((r, idx) => (idx === i ? { ...r, [field]: value } : r)) }));
  const addResolver = () =>
    setCfg((c) => ({ ...c, resolvers: [...c.resolvers, { match: '', type: 'direct', name: '' }] }));
  const removeResolver = (i: number) =>
    setCfg((c) => ({ ...c, resolvers: c.resolvers.filter((_, idx) => idx !== i) }));

  /** The config to send — parsed from the raw editor when in advanced mode. */
  const resolveConfig = (): RemoteSourceConfig | undefined => {
    if (!advanced) return cfg;
    try {
      return JSON.parse(rawText) as RemoteSourceConfig;
    } catch (e) {
      notification.error(String(e));
      return undefined;
    }
  };

  const toggleAdvanced = (on: boolean) => {
    if (on) {
      setRawText(JSON.stringify(cfg, null, 2));
    } else {
      try {
        setCfg(JSON.parse(rawText) as RemoteSourceConfig);
      } catch {
        /* keep the form state if the raw JSON is invalid */
      }
    }
    setAdvanced(on);
  };

  /** Open the test-connection modal on a snapshot of the current config (raw JSON parsed if advanced). */
  const handleTest = () => {
    const config = resolveConfig();
    if (config) setTestConfig(config);
  };

  const handleSave = async () => {
    if (!selectedProfileId) return;
    const config = resolveConfig();
    if (!config) return;
    try {
      setSaving(true);
      const saved = await api.remote.saveSource(selectedProfileId, config);
      notification.success(t('remote.sourceSaved', { name: saved.name }));
      onSaved(saved);
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setSaving(false);
    }
  };

  /** Open the per-field "compare with default" dialog — fetch the shipped res default lazily. */
  const handleCompare = async () => {
    if (!selectedProfileId) return;
    try {
      const def = await api.remote.getSourceDefault(selectedProfileId, cfg.id);
      if (!def) {
        notification.info(t('remote.compareNoDefault'));
        return;
      }
      setDefaultConfig(def);
      setComparing(true);
    } catch (error: unknown) {
      handleError(error);
    }
  };

  /** Apply the reverted config (selected fields set to their default values) back into the editor. */
  const handleRevert = (reverted: RemoteSourceConfig) => {
    setCfg(reverted);
    if (advanced) setRawText(JSON.stringify(reverted, null, 2));
    setComparing(false);
  };

  const engineOptions = useMemo(
    () => [
      { value: 'http', label: t('remote.engineHttp') },
      { value: 'gamebanana', label: t('remote.engineGamebanana') },
    ],
    [t],
  );
  const fetcherOptions = useMemo(
    () => [
      { value: 'http', label: t('remote.fetcherHttp') },
      { value: 'webview', label: t('remote.fetcherWebview') },
    ],
    [t],
  );
  const resolverTypeOptions = useMemo(
    () => [
      { value: 'direct', label: t('remote.resolverDirect') },
      { value: 'cloudreve', label: t('remote.resolverCloudreve') },
      { value: 'external', label: t('remote.resolverExternal') },
    ],
    [t],
  );

  return (
    <div className="remote-source-editor">
      <div className="remote-source-editor__body">
      <CompactSection title={t('remote.editorBasics')}>
        <CompactField label={t('remote.fieldName')} required>
          <CompactInput value={cfg.name} onChange={(e) => set('name', e.target.value)} placeholder="My Site" />
        </CompactField>
        <CompactField label={t('remote.fieldId')} required description={t('remote.fieldIdHint')}>
          <CompactInput
            value={cfg.id}
            disabled={!isNew}
            onChange={(e) => set('id', e.target.value.replace(/[^a-zA-Z0-9_-]/g, ''))}
            placeholder="mysite"
          />
        </CompactField>
        <CompactField label={t('remote.fieldBaseUrl')} required>
          <CompactInput value={cfg.baseUrl} onChange={(e) => set('baseUrl', e.target.value)} placeholder="https://example.com" />
        </CompactField>
        <CompactField label={t('remote.fieldEngine')} description={t('remote.fieldEngineHint')}>
          <CompactSelect value={cfg.engine ?? 'http'} options={engineOptions} onChange={(v) => set('engine', v)} style={{ width: '100%' }} />
        </CompactField>
        <CompactField label={t('remote.fieldFetcher')} description={t('remote.fieldFetcherHint')}>
          <CompactSelect value={cfg.fetcher ?? 'http'} options={fetcherOptions} onChange={(v) => set('fetcher', v)} style={{ width: '100%' }} />
        </CompactField>
      </CompactSection>

      <CompactSection title={isGameBanana ? t('remote.editorGamesGb') : t('remote.editorGames')}>
        {cfg.lists.map((l, i) => (
          <div key={i} className="remote-source-editor__row">
            <CompactInput
              className="remote-source-editor__row-id"
              value={l.id}
              onChange={(e) => setList(i, 'id', e.target.value)}
              placeholder={isGameBanana ? t('remote.fieldGameId') : 'id'}
            />
            <CompactInput value={l.name} onChange={(e) => setList(i, 'name', e.target.value)} placeholder={t('remote.fieldGameName')} />
            <CompactIconButton tone="danger" icon={<DeleteOutlined />} title={t('common.remove')} onClick={() => removeList(i)} />
          </div>
        ))}
        <CompactButton size="small" icon={<PlusOutlined />} onClick={addList}>
          {t('remote.addGame')}
        </CompactButton>
      </CompactSection>

      {!isGameBanana && !advanced && (
        <>
          <CompactSection title={t('remote.editorBrowsing')}>
            <CompactField label={t('remote.fieldListUrl')} required description="{list}">
              <CompactInput value={cfg.listUrlFirstPage} onChange={(e) => set('listUrlFirstPage', e.target.value)} placeholder="/?list_{list}/" />
            </CompactField>
            <CompactField label={t('remote.fieldListUrlPaged')} description="{list} · {page}">
              <CompactInput value={cfg.listUrlTemplate ?? ''} onChange={(e) => set('listUrlTemplate', e.target.value)} placeholder="/?list_{list}_{page}/" />
            </CompactField>
            <CompactField label={t('remote.fieldSearchUrl')} hint={t('common.optional')} description="{query}">
              <CompactInput value={cfg.searchUrlTemplate ?? ''} onChange={(e) => set('searchUrlTemplate', e.target.value)} placeholder="/?keyword={query}" />
            </CompactField>
            <CompactField label={t('remote.fieldCardPattern')} required>
              <CompactTextArea value={cfg.cardPattern} onChange={(e) => set('cardPattern', e.target.value)} autoSize={{ minRows: 2, maxRows: 5 }} spellCheck={false} />
            </CompactField>
            <CompactField label={t('remote.fieldCardScope')} hint={t('common.optional')}>
              <CompactTextArea value={cfg.cardScopePattern ?? ''} onChange={(e) => set('cardScopePattern', e.target.value)} autoSize={{ minRows: 1, maxRows: 3 }} spellCheck={false} />
            </CompactField>
            <CompactField label={t('remote.fieldTotalPages')} hint={t('common.optional')}>
              <CompactInput value={cfg.totalPagesPattern ?? ''} onChange={(e) => set('totalPagesPattern', e.target.value)} />
            </CompactField>
          </CompactSection>

          <CompactSection title={t('remote.editorDetail')}>
            <CompactField label={t('remote.fieldDetailTitle')} required>
              <CompactInput value={cfg.detailTitlePattern} onChange={(e) => set('detailTitlePattern', e.target.value)} />
            </CompactField>
            <CompactField label={t('remote.fieldDetailImage')} hint={t('common.optional')}>
              <CompactInput value={cfg.detailImagePattern ?? ''} onChange={(e) => set('detailImagePattern', e.target.value)} />
            </CompactField>
            <CompactField label={t('remote.fieldDownloadLink')} required>
              <CompactInput value={cfg.downloadLinkPattern} onChange={(e) => set('downloadLinkPattern', e.target.value)} />
            </CompactField>
          </CompactSection>
        </>
      )}

      {!advanced && (
        <CompactSection title={t('remote.editorIdentity')}>
          <CompactField label={t('remote.fieldEntryId')} hint={t('common.optional')} description={t('remote.fieldEntryIdHint')}>
            <CompactInput value={cfg.entryIdPattern ?? ''} onChange={(e) => set('entryIdPattern', e.target.value)} placeholder="/mods/(?<id>\\d+)" />
          </CompactField>
          {!isGameBanana && (
            <CompactField label={t('remote.fieldImageDate')} hint={t('common.optional')}>
              <CompactInput value={cfg.imageDatePattern ?? ''} onChange={(e) => set('imageDatePattern', e.target.value)} />
            </CompactField>
          )}
          {!isGameBanana && (
            <CompactField label={t('remote.fieldTitleTag')} hint={t('common.optional')} description={t('remote.fieldTitleTagHint')}>
              <CompactInput
                value={cfg.titleTagPattern ?? ''}
                onChange={(e) => set('titleTagPattern', e.target.value)}
                placeholder={'^(?<tag>\\S+)\\s'}
                spellCheck={false}
              />
            </CompactField>
          )}
        </CompactSection>
      )}

      {!isGameBanana && !advanced && (
        <CompactSection title={t('remote.editorDownloads')}>
          {cfg.resolvers.map((r, i) => (
            <div key={i} className="remote-source-editor__row">
              <CompactInput className="remote-source-editor__row-match" value={r.match} onChange={(e) => setResolver(i, 'match', e.target.value)} placeholder={t('remote.fieldResolverMatch')} spellCheck={false} />
              <CompactSelect className="remote-source-editor__row-type" value={r.type} options={resolverTypeOptions} onChange={(v) => setResolver(i, 'type', v)} />
              <CompactInput value={r.name} onChange={(e) => setResolver(i, 'name', e.target.value)} placeholder={t('remote.fieldResolverName')} />
              <CompactIconButton tone="danger" icon={<DeleteOutlined />} title={t('common.remove')} onClick={() => removeResolver(i)} />
            </div>
          ))}
          <CompactButton size="small" icon={<PlusOutlined />} onClick={addResolver}>
            {t('remote.addResolver')}
          </CompactButton>
        </CompactSection>
      )}

      {advanced && (
        <CompactTextArea
          className="remote-source-editor__raw"
          value={rawText}
          onChange={(e) => setRawText(e.target.value)}
          autoSize={{ minRows: 14, maxRows: 26 }}
          spellCheck={false}
        />
      )}

      </div>

      {/* Pinned action bar — always reachable no matter how long the form is. */}
      <div className="remote-source-editor__footer">
        <label className="remote-source-editor__advanced">
          <CompactSwitch size="small" checked={advanced} onChange={toggleAdvanced} />
          {t('remote.editorAdvanced')}
        </label>
        <div className="remote-source-editor__actions">
          {/* Compare with default = per-field re-sync; only meaningful for a customized (res+overlay) source. */}
          {origin === 'customized' && (
            <CompactButton icon={<DiffOutlined />} onClick={() => void handleCompare()}>
              {t('remote.compareWithDefault')}
            </CompactButton>
          )}
          <CompactButton onClick={onCancel}>{t('common.cancel')}</CompactButton>
          <CompactButton icon={<ExperimentOutlined />} onClick={handleTest}>
            {t('remote.testSource')}
          </CompactButton>
          <CompactButton type="primary" loading={saving} disabled={!dirty} onClick={() => void handleSave()}>
            {t('remote.saveSource')}
          </CompactButton>
        </div>
      </div>

      {defaultConfig && (
        <RemoteSourceCompareDialog
          visible={comparing}
          current={currentConfig ?? cfg}
          def={defaultConfig}
          onRevert={handleRevert}
          onCancel={() => setComparing(false)}
        />
      )}

      {testConfig && (
        <RemoteSourceTestDialog visible config={testConfig} onClose={() => setTestConfig(undefined)} />
      )}
    </div>
  );
};
