import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Empty, Pagination, Spin, Tooltip } from 'antd';
import { AppstoreOutlined, CheckCircleFilled, ReloadOutlined, SearchOutlined, SyncOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useSlideInScreenContext } from '../../../shared/context/SlideInScreenContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { CompactButton, CompactInput, CompactSelect } from '../../../shared/components/compact';
import type { RemoteLibrariesState, RemoteSourceInfo, RemoteTagCount } from '../../../shared/types/remote.types';
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
  // Distinct SITE tags in the synced index — the grid tag filter.
  const [tagCounts, setTagCounts] = useState<RemoteTagCount[]>([]);
  // The in-flight background sync process id — watched so the grid auto-refreshes from the index
  // when the crawl finishes (otherwise you'd stare at the pre-sync live fallback until a manual refresh).
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

  /** Query the synced index; when it's empty (never synced), fall back to live browsing. */
  const loadIndex = useCallback(
    async (page: number, search?: string) => {
      const state = useRemoteUiStore.getState();
      if (!selectedProfileId || !state.sourceId || !state.listId) return;
      try {
        setLoading(true);
        const index = await api.remote.indexQuery(
          selectedProfileId, state.sourceId, state.listId, search?.trim() || undefined, page, INDEX_PAGE_SIZE,
          state.sort, state.tagFilter);
        state.setPage(page);
        state.setIndex(index);
        if (index.info.entryCount === 0) {
          // Never synced — live-browse the first page so the tab isn't empty, and AUTO-START the
          // sync (once per library per session; backend is idempotent). Standardized flow: every
          // engine gets its index built the same way, so search/sort/tags/pagination just work.
          const result = await api.remote.browse(selectedProfileId, state.sourceId, state.listId, page);
          state.setResult(result, false);
          const key = `${state.sourceId}|${state.listId}`;
          if (!autoSynced.current.has(key)) {
            autoSynced.current.add(key);
            const ack = await api.remote.indexSync(selectedProfileId, state.sourceId, state.listId, false);
            if (ack.started && ack.processId) {
              setSyncProcId(ack.processId);
              notification.info(t('remote.syncStarted'));
            }
          }
        } else {
          // Refresh the tag-filter options from the (possibly just-synced) index.
          void api.remote
            .indexTags(selectedProfileId, state.sourceId, state.listId)
            .then(setTagCounts)
            .catch(() => setTagCounts([]));
        }
      } catch (error: unknown) {
        handleError(error);
      } finally {
        setLoading(false);
      }
    },
    [selectedProfileId, t],
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

  // When the tracked background sync finishes, re-query the index so the grid shows the freshly
  // crawled results instead of the pre-sync live fallback (no manual refresh needed).
  const syncProcess = useProcessStore((s) => (syncProcId ? s.processes.find((p) => p.id === syncProcId) : undefined));
  useEffect(() => {
    if (syncProcId && syncProcess && syncProcess.status === 'completed') {
      setSyncProcId(undefined);
      void loadIndex(1, useRemoteUiStore.getState().searchText);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [syncProcess?.status]);

  // First load (or returning to the tab with no cached result yet).
  useEffect(() => {
    if (!ui.index && !ui.result && ui.sourceId && ui.listId && !loading) void loadIndex(1, ui.searchText);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ui.sourceId, ui.listId]);

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
        <CompactButton icon={<AppstoreOutlined />} onClick={openManagement}>
          {t('remote.manage')}
        </CompactButton>
        {/* One CONSISTENT toolbar regardless of sync state — sort/tag act on the synced index and
            are disabled (not hidden) until the first sync, so the layout never jumps. */}
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
        <CompactSelect
          className="remote-library__tag-filter"
          value={ui.tagFilter ?? ''}
          disabled={!indexReady || tagCounts.length === 0}
          options={[
            { value: '', label: t('remote.allTags') },
            ...tagCounts.map((c) => ({ value: c.name, label: `${c.name} (${c.count})` })),
          ]}
          onChange={(v) => {
            ui.setTagFilter(v || undefined);
            void loadIndex(1, useRemoteUiStore.getState().searchText);
          }}
        />
        <CompactButton icon={<ReloadOutlined />} onClick={refresh}>
          {t('common.refresh')}
        </CompactButton>
        <Tooltip title={t('remote.updateHint')}>
          <CompactButton icon={<SyncOutlined />} onClick={() => void startSync(false)}>
            {t('remote.update')}
          </CompactButton>
        </Tooltip>
        <Tooltip title={t('remote.fullReindexHint')}>
          <CompactButton icon={<ReloadOutlined />} onClick={() => void startSync(true)}>
            {t('remote.fullReindex')}
          </CompactButton>
        </Tooltip>
        <span className="remote-library__origin" title={source?.baseUrl}>
          {syncedAt
            ? t('remote.lastSynced', {
                time: new Date(syncedAt).toLocaleString(),
                count: ui.index?.info.entryCount ?? 0,
              })
            : activeLibrary?.name}
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

      {/* ONE pager, driven by OUR synced list's count (the library is our own list built by the
          sync strategy — never mirror the site's pagination). Pre-sync, the live preview shows
          page 1 only with the sync hint above pushing to sync. */}
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
    </div>
  );
};
