import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Empty, Pagination, Spin, Tooltip } from 'antd';
import { AppstoreOutlined, CheckCircleFilled, CloudSyncOutlined, ReloadOutlined, SearchOutlined, SyncOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useSlideInScreenContext } from '../../../shared/context/SlideInScreenContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { CompactButton, CompactIconButton, CompactInput, CompactSelect } from '../../../shared/components/compact';
import type { RemoteLibrariesState, RemoteSourceInfo } from '../../../shared/types/remote.types';
import { remoteImageUrl } from '../../../shared/utils/imageUrlHelper';
import { useProcessStore } from '../../../shared/store/processStore';
import { useRemoteUiStore } from '../store/remoteUiStore';
import { RemoteModDetailScreen } from './RemoteModDetailScreen';
import { RemoteLibraryManagementScreen } from './RemoteLibraryManagementScreen';
import './RemoteLibraryView.css';

const INDEX_PAGE_SIZE = 60;

/**
 * Remote mod library tab (remote-library-redesign.md). A profile owns MANY configured libraries
 * (site + game + import tag-rules); the toolbar switches between them; library management
 * adds/edits/removes them. Primary browse source is the SYNCED LOCAL INDEX (instant filter/search/
 * paging, offline); live page browsing is the fallback until the first sync. Selection and results
 * live in remoteUiStore so leaving the tab and coming back restores where you were.
 */
export const RemoteLibraryView: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { openScreen } = useSlideInScreenContext();

  const [sources, setSources] = useState<RemoteSourceInfo[]>([]);
  const [loading, setLoading] = useState(false);
  // undefined = still loading; the profile's configured libraries + which is active.
  const [libState, setLibState] = useState<RemoteLibrariesState>();
  // The in-flight background sync process id — watched for PROGRESSIVE refresh (the grid fills while
  // the crawl runs; pagination/sort work as soon as the first pages land) + the final refresh.
  const [syncProcId, setSyncProcId] = useState<string>();
  // Libraries whose empty index already auto-kicked a sync this session (avoid re-kicking on paging).
  const autoSynced = React.useRef(new Set<string>());

  const ui = useRemoteUiStore();
  const activeLibrary = libState?.libraries.find((l) => l.id === libState.activeLibraryId);
  const source = sources.find((s) => s.id === ui.sourceId);
  const indexReady = (ui.index?.info.entryCount ?? 0) > 0;

  /** Point the browse state at a library (source+list drive every query below). */
  const applyLibrary = useCallback((sourceId?: string, listId?: string) => {
    const state = useRemoteUiStore.getState();
    if (state.sourceId !== sourceId) state.setSource(sourceId);
    if (useRemoteUiStore.getState().listId !== listId) useRemoteUiStore.getState().setList(listId);
  }, []);

  const reloadLibraries = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      const [list, state] = await Promise.all([
        api.remote.getSources(selectedProfileId),
        api.remote.libraryGetState(selectedProfileId),
      ]);
      setSources(list);
      setLibState(state);
      const active = state.libraries.find((l) => l.id === state.activeLibraryId);
      applyLibrary(active?.sourceId, active?.listId);
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, applyLibrary]);

  useEffect(() => {
    if (!selectedProfileId) return;
    ui.ensureProfile(selectedProfileId);
    void reloadLibraries();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId]);

  const switchLibrary = useCallback(async (libraryId: string) => {
    if (!selectedProfileId) return;
    try {
      const state = await api.remote.librarySetActive(selectedProfileId, libraryId);
      setLibState(state);
      const active = state.libraries.find((l) => l.id === state.activeLibraryId);
      applyLibrary(active?.sourceId, active?.listId);
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, applyLibrary]);

  /** Query the synced index; when it's empty (never synced), fall back to live browsing.
   * `silent` skips the spinner — used by the progressive refresh while a sync crawls. */
  const loadIndex = useCallback(
    async (page: number, search?: string, silent = false) => {
      const state = useRemoteUiStore.getState();
      if (!selectedProfileId || !state.sourceId || !state.listId) return;
      try {
        if (!silent) setLoading(true);
        const index = await api.remote.indexQuery(
          selectedProfileId, state.sourceId, state.listId, search?.trim() || undefined, page, INDEX_PAGE_SIZE,
          state.sort);
        state.setPage(page);
        state.setIndex(index);
        if (index.info.entryCount === 0) {
          // Never synced — live-browse the first page so the tab isn't empty, and AUTO-START the
          // sync (once per library per session; backend is idempotent). Standardized flow: every
          // engine gets its index built the same way, so search/sort/pagination just work.
          const result = await api.remote.browse(selectedProfileId, state.sourceId, state.listId, page);
          state.setResult(result, false);
          const key = `${state.sourceId}|${state.listId}`;
          if (!autoSynced.current.has(key)) {
            autoSynced.current.add(key);
            const ack = await api.remote.indexSync(selectedProfileId, state.sourceId, state.listId, false);
            if (ack.started && ack.processId) setSyncProcId(ack.processId);
          }
        }
      } catch (error: unknown) {
        if (!silent) handleError(error);
      } finally {
        if (!silent) setLoading(false);
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
      const result = await api.remote.search(selectedProfileId, state.sourceId, query, state.listId);
      state.setPage(1);
      state.setResult(result, true);
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setLoading(false);
    }
  }, [selectedProfileId, loadIndex, browseLive]);

  const startSync = useCallback(async (full = false) => {
    const state = useRemoteUiStore.getState();
    if (!selectedProfileId || !state.sourceId || !state.listId) return;
    try {
      const ack = await api.remote.indexSync(selectedProfileId, state.sourceId, state.listId, full);
      if (ack.started && ack.processId) setSyncProcId(ack.processId);
      notification.info(t(ack.started ? 'remote.syncStarted' : 'remote.syncRunningOrDone'));
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId, t]);

  // PROGRESSIVE refresh while the tracked sync crawls: silently re-query the index every few
  // seconds so the grid/pagination/sort fill up DURING the sync (the interface stays usable), then
  // a final refresh when it finishes.
  const syncProcess = useProcessStore((s) => (syncProcId ? s.processes.find((p) => p.id === syncProcId) : undefined));
  useEffect(() => {
    if (!syncProcId || !syncProcess) return;
    if (syncProcess.status === 'running' || syncProcess.status === 'queued') {
      const timer = setTimeout(() => {
        const state = useRemoteUiStore.getState();
        void loadIndex(state.page, state.searchText, true);
      }, 2500);
      return () => clearTimeout(timer);
    }
    // Terminal state — final refresh (spinnerless keeps the grid stable) + stop tracking.
    setSyncProcId(undefined);
    void loadIndex(useRemoteUiStore.getState().page, useRemoteUiStore.getState().searchText, true);
    return undefined;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [syncProcId, syncProcess?.status, syncProcess?.progress]);

  // First load (or returning to the tab with no cached result yet).
  useEffect(() => {
    if (!ui.index && !ui.result && ui.sourceId && ui.listId && !loading) void loadIndex(1, ui.searchText);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ui.sourceId, ui.listId]);

  // BUILT-IN AUTO-SYNC: when the active library's index is STALE (older than 30 min), kick a silent
  // incremental update — on opening the library and again on a timer while the tab stays mounted.
  // The backend is idempotent (an already-running sync is a no-op ack).
  useEffect(() => {
    if (!selectedProfileId || !ui.sourceId || !ui.listId) return;
    const STALE_MS = 30 * 60 * 1000;
    const maybeSync = () => {
      const at = useRemoteUiStore.getState().index?.info.syncedAtUtc;
      if (at && Date.now() - new Date(at).getTime() < STALE_MS) return;
      void api.remote.indexSync(selectedProfileId, ui.sourceId!, ui.listId!, false)
        .then((ack) => { if (ack.started && ack.processId) setSyncProcId(ack.processId); })
        .catch(() => undefined); // silent — background freshness, not a user action
    };
    const timer = setInterval(maybeSync, STALE_MS);
    maybeSync();
    return () => clearInterval(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId, ui.sourceId, ui.listId]);

  const openDetail = useCallback(
    (card: { detailUrl: string; title: string; key: string; tags: string[] }) => {
      const state = useRemoteUiStore.getState();
      if (!state.sourceId) return;
      openScreen({
        title: card.title || t('remote.detailTitle'),
        content: (
          <RemoteModDetailScreen
            sourceId={state.sourceId}
            listId={state.listId}
            entryId={card.key}
            entryTags={card.tags}
            detailUrl={card.detailUrl}
            fallbackTitle={card.title}
          />
        ),
        width: '980px',
      });
    },
    [openScreen, t],
  );

  const openManagement = useCallback(() => {
    openScreen({
      title: t('remote.manageTitle'),
      content: (
        <RemoteLibraryManagementScreen
          onChanged={() => void reloadLibraries()}
        />
      ),
      width: '860px',
    });
  }, [openScreen, t, reloadLibraries]);

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
        tags: e.tags ?? [],
        dateHint: e.dateHint,
        imported: e.imported,
      }))
    : (ui.result?.cards ?? []).map((c) => ({
        key: c.detailUrl,
        title: c.title,
        detailUrl: c.detailUrl,
        imageUrl: c.imageUrl,
        tags: c.tags ?? [],
        dateHint: c.dateHint,
        imported: false,
      }));
  const loaded = indexReady || !!ui.result;
  const syncedAt = ui.index?.info.syncedAtUtc;

  if (libState === undefined) {
    return (
      <div className="remote-library__loading">
        <Spin />
      </div>
    );
  }

  // NO LIBRARIES YET — point the user at library management to add the first one.
  if (libState.libraries.length === 0) {
    return (
      <div className="remote-library remote-library--setup">
        <div className="remote-setup">
          <h2 className="remote-setup__title">{t('remote.setupTitle')}</h2>
          <p className="remote-setup__hint">{t('remote.setupHint')}</p>
          <CompactButton type="primary" icon={<AppstoreOutlined />} onClick={openManagement}>
            {t('remote.addLibrary')}
          </CompactButton>
        </div>
      </div>
    );
  }

  return (
    <div className="remote-library">
      <div className="remote-library__toolbar">
        <CompactSelect
          className="remote-library__switcher"
          value={libState.activeLibraryId}
          options={libState.libraries.map((l) => ({ value: l.id, label: l.name }))}
          onChange={(id) => void switchLibrary(id)}
        />
        {/* One CONSISTENT compact toolbar for every engine — search covers titles AND tags; sort acts
            on the synced index (disabled until data lands); icon actions with distinct icons.
            Full-reindex lives in library management, not here. */}
        <CompactInput
          className="remote-library__search"
          placeholder={t('remote.searchPlaceholder')}
          value={ui.searchText}
          onChange={(e) => ui.setSearchText(e.target.value)}
          onPressEnter={() => void runSearch()}
          suffix={<SearchOutlined onClick={() => void runSearch()} />}
          allowClear
        />
        <CompactSelect
          className="remote-library__sort"
          value={ui.sort}
          disabled={!indexReady}
          options={[
            { value: 'site', label: t('remote.sortSite') },
            { value: 'date', label: t('remote.sortDate') },
          ]}
          onChange={(v) => {
            ui.setSort(v);
            void loadIndex(1, useRemoteUiStore.getState().searchText);
          }}
        />
        <CompactIconButton icon={<ReloadOutlined />} title={t('common.refresh')} onClick={refresh} />
        <CompactIconButton
          icon={<CloudSyncOutlined />}
          title={t('remote.updateHint')}
          onClick={() => void startSync(false)}
        />
        {/* Pager lives in the toolbar (was easy to miss below the fold). Our index count only. */}
        {indexReady && (ui.index?.total ?? 0) > INDEX_PAGE_SIZE && (
          <Pagination
            className="remote-library__pager-inline"
            simple
            size="small"
            current={ui.page}
            total={ui.index!.total}
            pageSize={INDEX_PAGE_SIZE}
            showSizeChanger={false}
            onChange={(p) => void loadIndex(p, ui.searchText)}
          />
        )}
        {/* Right side: sync freshness only (the switcher already names the library). */}
        <span className="remote-library__origin" title={source?.baseUrl}>
          {syncedAt
            ? t('remote.lastSynced', {
                time: new Date(syncedAt).toLocaleString(),
                count: ui.index?.info.entryCount ?? 0,
              })
            : null}
        </span>
        <CompactIconButton icon={<AppstoreOutlined />} title={t('remote.manage')} onClick={openManagement} />
      </div>

      {/* Sync status bar: live progress while a crawl runs (interface stays usable — the grid fills
          progressively); a sync CTA only when never synced and nothing is running. */}
      {syncProcess && (syncProcess.status === 'running' || syncProcess.status === 'queued') && (
        <div className="remote-library__sync-hint remote-library__sync-hint--active">
          <SyncOutlined spin />
          {t('remote.syncing', { percent: syncProcess.progress ?? 0 })}
        </div>
      )}
      {!syncProcess && !syncedAt && loaded && (
        <div className="remote-library__sync-hint">
          {t('remote.notSynced')}
          <CompactButton size="small" type="link" icon={<CloudSyncOutlined />} onClick={() => void startSync()}>
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
              <div key={card.key} className="remote-card" onClick={() => openDetail(card)}>
                <div className="remote-card__image-wrap">
                  <img className="remote-card__image" src={remoteImageUrl(card.imageUrl)} alt={card.title} loading="lazy" />
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
                  <div className="remote-card__footer">
                    {card.tags.length > 0 && (
                      <span className="remote-card__tags">
                        {card.tags.map((tag) => (
                          <span key={tag} className="remote-card__tag">{tag}</span>
                        ))}
                      </span>
                    )}
                    {card.dateHint && <span className="remote-card__date">{card.dateHint}</span>}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

    </div>
  );
};
