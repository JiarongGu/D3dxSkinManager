import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Empty, Pagination, Spin, Tooltip } from 'antd';
import { CheckCircleFilled, ReloadOutlined, SearchOutlined, SettingOutlined, SyncOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useSlideInScreenContext } from '../../../shared/context/SlideInScreenContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { CompactButton, CompactInput, CompactSelect } from '../../../shared/components/compact';
import type { RemoteBinding, RemoteSourceInfo } from '../../../shared/types/remote.types';
import { toAppUrl } from '../../../shared/utils/imageUrlHelper';
import { useRemoteUiStore } from '../store/remoteUiStore';
import { RemoteModDetailScreen } from './RemoteModDetailScreen';
import { RemoteSourceManagerScreen } from './RemoteSourceManagerScreen';
import './RemoteLibraryView.css';

const INDEX_PAGE_SIZE = 60;

/**
 * Remote mod library tab. Primary browse source is the SYNCED LOCAL INDEX (instant filter/search/
 * paging, offline); live page browsing is the fallback until the first sync. Selection and results
 * live in remoteUiStore so leaving the tab and coming back restores where you were.
 */
export const RemoteLibraryView: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { openScreen } = useSlideInScreenContext();

  const [sources, setSources] = useState<RemoteSourceInfo[]>([]);
  const [loading, setLoading] = useState(false);
  // undefined = still loading; null = profile hasn't picked its game yet (setup mode).
  const [binding, setBindingState] = useState<RemoteBinding | null | undefined>(undefined);
  const [setupSource, setSetupSource] = useState<string>();
  // Remote image URL -> cached local path (per-profile remote-cache; falls back to the URL).
  const [imageMap, setImageMap] = useState<Record<string, string>>({});
  const [setupList, setSetupList] = useState<string>();

  const ui = useRemoteUiStore();
  const source = sources.find((s) => s.id === ui.sourceId);
  const indexReady = (ui.index?.info.entryCount ?? 0) > 0;

  // Load sources + this profile's game binding. A profile is ONE game: bound → that source+list
  // drives everything; unbound → setup mode.
  useEffect(() => {
    if (!selectedProfileId) return;
    ui.ensureProfile(selectedProfileId);
    void (async () => {
      try {
        const [list, bound] = await Promise.all([
          api.remote.getSources(selectedProfileId),
          api.remote.getBinding(selectedProfileId),
        ]);
        setSources(list);
        setBindingState(bound ?? null);
        const state = useRemoteUiStore.getState();
        if (bound) {
          if (state.sourceId !== bound.sourceId) state.setSource(bound.sourceId);
          if (useRemoteUiStore.getState().listId !== bound.listId) {
            useRemoteUiStore.getState().setList(bound.listId);
          }
        } else {
          setSetupSource(list[0]?.id);
          setSetupList(list[0]?.lists[0]?.id);
        }
      } catch (error: unknown) {
        handleError(error);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId]);

  const bindAndSync = useCallback(async () => {
    if (!selectedProfileId || !setupSource || !setupList) return;
    try {
      const result = await api.remote.setBinding(selectedProfileId, setupSource, setupList, true);
      setBindingState(result.binding);
      const state = useRemoteUiStore.getState();
      state.setSource(result.binding.sourceId);
      useRemoteUiStore.getState().setList(result.binding.listId);
      notification.info(t('remote.syncStarted'));
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, setupSource, setupList, t]);

  const rebind = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      await api.remote.clearBinding(selectedProfileId);
      setSetupSource(binding?.sourceId ?? sources[0]?.id);
      setSetupList(binding?.listId ?? sources[0]?.lists[0]?.id);
      setBindingState(null);
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, binding, sources]);

  /** Query the synced index; when it's empty (never synced), fall back to live browsing. */
  const loadIndex = useCallback(
    async (page: number, search?: string) => {
      const state = useRemoteUiStore.getState();
      if (!selectedProfileId || !state.sourceId || !state.listId) return;
      try {
        setLoading(true);
        const index = await api.remote.indexQuery(
          selectedProfileId, state.sourceId, state.listId, search?.trim() || undefined, page, INDEX_PAGE_SIZE, state.sort);
        state.setPage(page);
        state.setIndex(index);
        if (index.info.entryCount === 0) {
          // Never synced — live-browse the first page so the tab isn't empty.
          const result = await api.remote.browse(selectedProfileId, state.sourceId, state.listId, page);
          state.setResult(result, false);
        }
      } catch (error: unknown) {
        handleError(error);
      } finally {
        setLoading(false);
      }
    },
    [selectedProfileId],
  );

  const browseLive = useCallback(
    async (page: number) => {
      const state = useRemoteUiStore.getState();
      if (!selectedProfileId || !state.sourceId || !state.listId) return;
      try {
        setLoading(true);
        const result = await api.remote.browse(selectedProfileId, state.sourceId, state.listId, page);
        state.setPage(page);
        state.setResult(result, false);
      } catch (error: unknown) {
        handleError(error);
      } finally {
        setLoading(false);
      }
    },
    [selectedProfileId],
  );

  const runSearch = useCallback(async () => {
    const state = useRemoteUiStore.getState();
    if (!selectedProfileId || !state.sourceId) return;
    const query = state.searchText.trim();
    if ((state.index?.info.entryCount ?? 0) > 0) {
      // Index mode — instant local filter.
      void loadIndex(1, query);
      return;
    }
    if (!query) {
      void browseLive(1);
      return;
    }
    try {
      setLoading(true);
      const result = await api.remote.search(selectedProfileId, state.sourceId, query);
      state.setPage(1);
      state.setResult(result, true);
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setLoading(false);
    }
  }, [selectedProfileId, loadIndex, browseLive]);

  const startSync = useCallback(async () => {
    const state = useRemoteUiStore.getState();
    if (!selectedProfileId || !state.sourceId || !state.listId) return;
    try {
      const ack = await api.remote.indexSync(selectedProfileId, state.sourceId, state.listId);
      notification.info(t(ack.started ? 'remote.syncStarted' : 'remote.syncRunningOrDone'));
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, t]);

  // First load (or returning to the tab with no cached result yet).
  useEffect(() => {
    if (!ui.index && !ui.result && ui.sourceId && ui.listId && !loading) void loadIndex(1, ui.searchText);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ui.sourceId, ui.listId]);

  // Resolve the visible cards' images through the per-profile cache (fire-and-forget; the grid
  // renders remote URLs until the local copies are ready, then swaps).
  const cacheImages = useCallback((urls: string[]) => {
    if (!selectedProfileId || urls.length === 0) return;
    void api.remote
      .resolveImages(selectedProfileId, urls)
      .then((map) => setImageMap((prev) => ({ ...prev, ...map })))
      .catch(() => undefined);
  }, [selectedProfileId]);

  useEffect(() => {
    const urls = (ui.index?.entries ?? []).map((e) => e.imageUrl).concat((ui.result?.cards ?? []).map((c) => c.imageUrl));
    cacheImages(urls.filter((u) => u && !imageMap[u]));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ui.index, ui.result]);

  const openDetail = useCallback(
    (detailUrl: string, title: string) => {
      if (!ui.sourceId) return;
      openScreen({
        title: title || t('remote.detailTitle'),
        content: <RemoteModDetailScreen sourceId={ui.sourceId} detailUrl={detailUrl} fallbackTitle={title} />,
        width: '860px',
      });
    },
    [openScreen, t, ui.sourceId],
  );

  const refresh = () => {
    if (indexReady) void loadIndex(ui.page, ui.searchText);
    else if (ui.isSearchResult) void runSearch();
    else void browseLive(ui.page);
  };

  // Unified card list: index entries when synced, live cards otherwise.
  const cards = indexReady
    ? ui.index!.entries.map((e) => ({
        key: e.id,
        title: e.title,
        detailUrl: e.detailUrl,
        imageUrl: e.imageUrl,
        dateHint: e.dateHint,
        imported: e.imported,
      }))
    : (ui.result?.cards ?? []).map((c) => ({
        key: c.detailUrl,
        title: c.title,
        detailUrl: c.detailUrl,
        imageUrl: c.imageUrl,
        dateHint: undefined as string | undefined,
        imported: false,
      }));
  const loaded = indexReady || !!ui.result;

  const syncedAt = ui.index?.info.syncedAtUtc;
  const setupSourceInfo = sources.find((s) => s.id === setupSource);
  const boundListName = source?.lists.find((l) => l.id === ui.listId)?.name ?? ui.listId;

  if (binding === undefined) {
    return (
      <div className="remote-library__loading">
        <Spin />
      </div>
    );
  }

  // SETUP MODE — the profile hasn't picked its game yet: choose source + game, bind & sync.
  if (binding === null) {
    return (
      <div className="remote-library remote-library--setup">
        <div className="remote-setup">
          <h2 className="remote-setup__title">{t('remote.setupTitle')}</h2>
          <p className="remote-setup__hint">{t('remote.setupHint')}</p>
          <div className="remote-setup__row">
            <CompactSelect
              className="remote-library__source"
                  value={setupSource}
              placeholder={t('remote.setupPickSource')}
              options={sources.map((s) => ({ value: s.id, label: s.name }))}
              onChange={(v) => {
                setSetupSource(v);
                setSetupList(sources.find((s) => s.id === v)?.lists[0]?.id);
              }}
            />
            <CompactSelect
              className="remote-library__list"
                  value={setupList}
              placeholder={t('remote.setupPickGame')}
              options={(setupSourceInfo?.lists ?? []).map((l) => ({ value: l.id, label: l.name }))}
              onChange={setSetupList}
            />
            <CompactButton type="primary" icon={<SyncOutlined />} disabled={!setupSource || !setupList}
              onClick={() => void bindAndSync()}>
              {t('remote.bindAndSync')}
            </CompactButton>
          </div>
          <CompactButton
            icon={<SettingOutlined />}
            onClick={() =>
              openScreen({
                title: t('remote.manageTitle'),
                content: <RemoteSourceManagerScreen onChanged={() => {
                  if (selectedProfileId) void api.remote.getSources(selectedProfileId).then(setSources).catch(handleError);
                }} />,
                width: '760px',
              })
            }
          >
            {t('remote.custom')}
          </CompactButton>
        </div>
      </div>
    );
  }

  return (
    <div className="remote-library">
      <div className="remote-library__toolbar">
        <span className="remote-library__bound" title={source?.baseUrl}>
          {source?.name} · {boundListName}
        </span>
        <CompactButton onClick={() => void rebind()}>{t('remote.rebind')}</CompactButton>
        {(indexReady || source?.hasSearch) && (
          <CompactInput
            className="remote-library__search"
            placeholder={t('remote.searchPlaceholder')}
            value={ui.searchText}
            onChange={(e) => ui.setSearchText(e.target.value)}
            onPressEnter={() => void runSearch()}
            suffix={<SearchOutlined onClick={() => void runSearch()} />}
            allowClear
          />
        )}
        {indexReady && (
          <CompactSelect
            className="remote-library__sort"
              value={ui.sort}
            options={[
              { value: 'site', label: t('remote.sortSite') },
              { value: 'date', label: t('remote.sortDate') },
            ]}
            onChange={(v) => {
              ui.setSort(v);
              void loadIndex(1, useRemoteUiStore.getState().searchText);
            }}
          />
        )}
        <CompactButton icon={<ReloadOutlined />} onClick={refresh}>
          {t('common.refresh')}
        </CompactButton>
        <Tooltip title={t('remote.notSynced')}>
          <CompactButton icon={<SyncOutlined />} onClick={() => void startSync()}>
            {t('remote.sync')}
          </CompactButton>
        </Tooltip>
        <CompactButton
          icon={<SettingOutlined />}
          onClick={() =>
            openScreen({
              title: t('remote.manageTitle'),
              content: (
                <RemoteSourceManagerScreen
                  onChanged={() => {
                    if (selectedProfileId) {
                      void api.remote.getSources(selectedProfileId).then(setSources).catch(handleError);
                    }
                  }}
                />
              ),
              width: '760px',
            })
          }
        >
          {t('remote.manage')}
        </CompactButton>
        <span className="remote-library__origin" title={source?.baseUrl}>
          {syncedAt
            ? t('remote.lastSynced', {
                time: new Date(syncedAt).toLocaleString(),
                count: ui.index?.info.entryCount ?? 0,
              })
            : source?.name}
        </span>
      </div>

      {!syncedAt && loaded && (
        <div className="remote-library__sync-hint">
          {t('remote.notSynced')}
          <CompactButton size="small" type="link" icon={<SyncOutlined />} onClick={() => void startSync()}>
            {t('remote.sync')}
          </CompactButton>
        </div>
      )}

      <div className="remote-library__content">
        {loading && (
          <div className="remote-library__loading">
            <Spin />
          </div>
        )}
        {!loading && loaded && cards.length === 0 && (
          <Empty description={t('remote.noResults')} image={Empty.PRESENTED_IMAGE_SIMPLE} />
        )}
        {!loading && cards.length > 0 && (
          <div className="remote-library__grid">
            {cards.map((card) => (
              <div key={card.key} className="remote-card" onClick={() => openDetail(card.detailUrl, card.title)}>
                <div className="remote-card__image-wrap">
                  <img className="remote-card__image" src={imageMap[card.imageUrl] ? toAppUrl(imageMap[card.imageUrl]) : card.imageUrl} alt={card.title} loading="lazy" />
                  {card.imported && (
                    <span className="remote-card__imported" title={t('remote.importedBadge')}>
                      <CheckCircleFilled /> {t('remote.importedBadge')}
                    </span>
                  )}
                </div>
                <div className="remote-card__meta">
                  <div className="remote-card__title" title={card.title}>
                    {card.title || t('remote.untitled')}
                  </div>
                  {card.dateHint && <div className="remote-card__date">{card.dateHint}</div>}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {indexReady && (ui.index?.total ?? 0) > INDEX_PAGE_SIZE && (
        <div className="remote-library__pager">
          <Pagination
              current={ui.page}
            total={ui.index!.total}
            pageSize={INDEX_PAGE_SIZE}
            showSizeChanger={false}
            onChange={(p) => void loadIndex(p, ui.searchText)}
          />
        </div>
      )}
      {!indexReady && !ui.isSearchResult && ui.result && (ui.result.totalPages ?? 0) > 1 && (
        <div className="remote-library__pager">
          <Pagination
            simple
              current={ui.page}
            total={(ui.result.totalPages ?? 1) * 10}
            pageSize={10}
            showSizeChanger={false}
            onChange={(p) => void browseLive(p)}
          />
        </div>
      )}
    </div>
  );
};
