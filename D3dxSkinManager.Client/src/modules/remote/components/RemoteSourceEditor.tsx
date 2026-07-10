import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DeleteOutlined, ExperimentOutlined, PlusOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
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
import type { RemoteSourceConfig, RemoteSourceTestResult } from '../../../shared/types/remote.types';
import './RemoteSourceEditor.css';

interface RemoteSourceEditorProps {
  /** The config to edit; undefined = a fresh (blank http) adapter. */
  initial?: RemoteSourceConfig;
  onCancel: () => void;
  onSaved: (saved: RemoteSourceConfig) => void;
}

const BLANK: RemoteSourceConfig = {
  id: '',
  name: '',
  baseUrl: '',
  engine: 'http',
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
export const RemoteSourceEditor: React.FC<RemoteSourceEditorProps> = ({ initial, onCancel, onSaved }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [cfg, setCfg] = useState<RemoteSourceConfig>(() => ({ ...BLANK, ...initial }));
  const [advanced, setAdvanced] = useState(false);
  const [rawText, setRawText] = useState('');
  const [testing, setTesting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [testResult, setTestResult] = useState<RemoteSourceTestResult>();
  const [testError, setTestError] = useState<string>();

  const isGameBanana = (cfg.engine ?? 'http') === 'gamebanana';
  const isNew = !initial;

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
      setTestError(String(e));
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

  const handleTest = async () => {
    if (!selectedProfileId) return;
    const config = resolveConfig();
    if (!config) return;
    try {
      setTesting(true);
      setTestError(undefined);
      setTestResult(await api.remote.testSource(selectedProfileId, config));
    } catch (error: unknown) {
      setTestResult(undefined);
      handleError(error);
    } finally {
      setTesting(false);
    }
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

  const engineOptions = useMemo(
    () => [
      { value: 'http', label: t('remote.engineHttp') },
      { value: 'gamebanana', label: t('remote.engineGamebanana') },
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

      {testError && <div className="remote-source-editor__test remote-source-editor__test--error">{testError}</div>}
      {testResult && (
        <div className="remote-source-editor__test">
          {testResult.cardCount === 0
            ? t('remote.testNoCards')
            : t('remote.testResult', {
                cards: testResult.cardCount,
                pages: testResult.totalPages ?? '?',
                title: testResult.detailTitle ?? '—',
                downloads: testResult.detailDownloads.length,
                images: testResult.detailImageCount,
              })}
          {testResult.sampleTitles.length > 0 && (
            <div className="remote-source-editor__samples">{testResult.sampleTitles.join(' · ')}</div>
          )}
        </div>
      )}
      </div>

      {/* Pinned action bar — always reachable no matter how long the form is. */}
      <div className="remote-source-editor__footer">
        <label className="remote-source-editor__advanced">
          <CompactSwitch size="small" checked={advanced} onChange={toggleAdvanced} />
          {t('remote.editorAdvanced')}
        </label>
        <div className="remote-source-editor__actions">
          <CompactButton onClick={onCancel}>{t('common.cancel')}</CompactButton>
          <CompactButton icon={<ExperimentOutlined />} loading={testing} onClick={() => void handleTest()}>
            {t('remote.testSource')}
          </CompactButton>
          <CompactButton type="primary" loading={saving} onClick={() => void handleSave()}>
            {t('remote.saveSource')}
          </CompactButton>
        </div>
      </div>
    </div>
  );
};
