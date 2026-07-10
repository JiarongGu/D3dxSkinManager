import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Empty, Pagination, Spin, Tag, Tooltip } from 'antd';
import { AppstoreOutlined, CheckCircleFilled, CloudSyncOutlined, ReloadOutlined, SearchOutlined, SyncOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useSlideInScreenContext } from '../../../shared/context/SlideInScreenContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { formatDateTime } from '../../../shared/utils/formatDate';
import { CompactButton, CompactIconButton, CompactInput, CompactSelect } from '../../../shared/components/compact';
import type { RemoteLibrariesState, RemoteSourceInfo, RemoteTagCount } from '../../../shared/types/remote.types';
import { remoteImageUrl } from '../../../shared/utils/imageUrlHelper';
import { navigateToModSearch } from '../../../shared/hooks/useAppNavigation';
import { orderTagsForDisplay, remoteTagLabel } from '../../../shared/utils/remoteTagLabel';
import { useProcessStore } from '../../../shared/store/processStore';
import { ContentVeil } from '../../../shared/components/common/ContentVeil';
import { useContentVeilEnabled, useContentVeilVerdicts, isVeiled } from '../../../shared/hooks/useContentVeil';
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
  const { t, i18n } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { openScreen, closeScreen } = useSlideInScreenContext();

  const [sources, setSources] = useState<RemoteSourceInfo[]>([]);
  const [loading, setLoading] = useState(false);
  // undefined = still loading; the profile's configured libraries + which is active.
  const [libState, setLibState] = useState<RemoteLibrariesState>();
  // The in-flight background sync process id — watched for PROGRESSIVE refresh (the grid fills while
  // the crawl runs; pagination/sort work as soon as the first pages land) + the final refresh.
  const [syncProcId, setSyncProcId] = useState<string>();
  // Libraries whose empty index already auto-kicked a sync this session (avoid re-kicking on paging).
  const autoSynced = React.useRef(new Set<string>());
  // Debounce handle for the reactive (offline) index search.
  const searchDebounce = React.useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  // Distinct site tags present in the synced index → the tag-filter chip strip below the toolbar.
  const [tagCounts, setTagCounts] = useState<RemoteTagCount[]>([]);
  // The horizontal chip strip — scroll the ACTIVE chip into view when the filter changes.
  const tagBarRef = React.useRef<HTMLDivElement>(null);

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
          state.sort, state.tagFilter, state.downloadedOnly);
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

  // PROGRESSIVE refresh while the ACTIVE LIBRARY's sync crawls: silently re-query the index every
  // few seconds so the grid/pagination/sort fill up DURING the sync (the interface stays usable),
  // then a final refresh when it finishes. The process is discovered from the registry by its
  // localized-title identity (titleKey + arg) — NOT just our own kick — so a running sync stays
  // tracked across tab switches/remounts (it used to vanish with the component state).
  const activeListName = source?.lists.find((l) => l.id === ui.listId)?.name ?? ui.listId;
  const expectedSyncArg = source ? `${source.name} · ${activeListName}` : undefined;
  const syncProcess = useProcessStore((s) =>
    s.processes.find(
      (p) =>
        p.id === syncProcId ||
        (p.titleKey === 'process.remoteSync' &&
          p.titleArg === expectedSyncArg &&
          (p.status === 'running' || p.status === 'queued')),
    ),
  );
  const lastSyncSeen = React.useRef<string>(undefined);
  useEffect(() => {
    if (syncProcess && (syncProcess.status === 'running' || syncProcess.status === 'queued')) {
      lastSyncSeen.current = syncProcess.id;
      const timer = setTimeout(() => {
        const state = useRemoteUiStore.getState();
        void loadIndex(state.page, state.searchText, true);
      }, 2500);
      return () => clearTimeout(timer);
    }
    // Terminal (id-tracked) OR the discovered process vanished from the running set — final
    // spinnerless refresh + stop tracking.
    if (syncProcess || lastSyncSeen.current) {
      lastSyncSeen.current = undefined;
      setSyncProcId(undefined);
      void loadIndex(useRemoteUiStore.getState().page, useRemoteUiStore.getState().searchText, true);
    }
    return undefined;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [syncProcess?.id, syncProcess?.status, syncProcess?.progress]);

  // First load (or returning to the tab with no cached result yet).
  useEffect(() => {
    if (!ui.index && !ui.result && ui.sourceId && ui.listId && !loading) void loadIndex(1, ui.searchText);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ui.sourceId, ui.listId]);

  // After a remote IMPORT finishes, the imported flags + localModId change (metadata written +
  // lookup invalidated on Complete) → silently re-query the current page so the 已导入 chip appears
  // without a manual refresh. Watch the COMPLETED count (not MOD_LIST_UPDATED, which fires before the
  // metadata is written).
  const importDoneCount = useProcessStore(
    (s) => s.processes.filter((p) => p.titleKey === 'process.remoteImport' && p.status === 'completed').length,
  );
  const prevImportDone = React.useRef(importDoneCount);
  useEffect(() => {
    if (importDoneCount > prevImportDone.current) {
      const state = useRemoteUiStore.getState();
      void loadIndex(state.page, state.searchText, true);
    }
    prevImportDone.current = importDoneCount;
  }, [importDoneCount, loadIndex]);

  // BUILT-IN AUTO-SYNC: silently kick an incremental update when the loaded index is STALE
  // (>30 min since its last completed sync) — opening the page within that window does NOTHING;
  // only staying on (or returning after) 30+ min re-syncs. Guards against the "syncs on every
  // open" bug class: (1) NEVER act before the index query returned (info undefined ≠ stale);
  // (2) at most one stale-kick per library between interval resets; (3) syncedAtUtc must parse
  // as UTC — the backend re-marks DateTimeKind after SQLite so the Z suffix survives (east of
  // UTC, a Z-less timestamp parsed as LOCAL always looked hours stale → synced on EVERY page
  // open; fixed 2026-07-06). Never-synced libraries are handled by loadIndex's empty-index path.
  const staleKicked = React.useRef(new Set<string>());
  const syncedAtUtc = ui.index?.info.syncedAtUtc;
  useEffect(() => {
    if (!selectedProfileId || !ui.sourceId || !ui.listId) return;
    const STALE_MS = 30 * 60 * 1000;
    const maybeSync = () => {
      const state = useRemoteUiStore.getState();
      const info = state.index?.info;
      if (!info || info.entryCount === 0) return; // not loaded / never synced — not ours to kick
      const at = info.syncedAtUtc;
      if (at && Date.now() - new Date(at).getTime() < STALE_MS) return;
      const key = `${state.sourceId}|${state.listId}`;
      if (staleKicked.current.has(key)) return;
      staleKicked.current.add(key);
      void api.remote.indexSync(selectedProfileId, state.sourceId!, state.listId!, false)
        .then((ack) => { if (ack.started && ack.processId) setSyncProcId(ack.processId); })
        .catch(() => undefined); // silent — background freshness, not a user action
    };
    const timer = setInterval(() => {
      staleKicked.current.clear(); // periodic re-check against real freshness
      maybeSync();
    }, STALE_MS);
    maybeSync();
    return () => clearInterval(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId, ui.sourceId, ui.listId, syncedAtUtc]);

  // Load the distinct tags for the chip strip once the index has data (and reload after a sync,
  // when new tags may have appeared). Never-synced libraries have no tags → empty strip (hidden).
  useEffect(() => {
    if (!selectedProfileId || !ui.sourceId || !ui.listId || !indexReady) {
      setTagCounts([]);
      return undefined;
    }
    let cancelled = false;
    void api.remote
      .indexTags(selectedProfileId, ui.sourceId, ui.listId)
      .then((tags) => { if (!cancelled) setTagCounts(tags); })
      .catch(() => { if (!cancelled) setTagCounts([]); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId, ui.sourceId, ui.listId, indexReady, ui.index?.info.syncedAtUtc]);

  // (1) Scroll the ACTIVE tag chip into view within the (horizontally scrollable) strip whenever the
  // filter changes or the tags (re)load. Address the chip by child index (child 0 = the "All" chip)
  // so it doesn't depend on antd's internal class; scroll the STRIP only so ancestors don't jump.
  useEffect(() => {
    const bar = tagBarRef.current;
    if (!bar) return;
    const idx = ui.tagFilter ? tagCounts.findIndex((tc) => tc.name === ui.tagFilter) + 1 : 0;
    const chip = bar.children[idx] as HTMLElement | undefined;
    if (!chip) return;
    const target = chip.offsetLeft - bar.clientWidth / 2 + chip.clientWidth / 2;
    bar.scrollTo({ left: Math.max(0, target), behavior: 'smooth' });
  }, [ui.tagFilter, tagCounts]);

  // Mouse-wheel → horizontal scroll for the single-row tag bar. Without this a vertical wheel scrolls
  // the grid behind it, not the strip. Non-passive native listener so preventDefault actually works
  // (React's synthetic onWheel is passive and can't preventDefault).
  useEffect(() => {
    const bar = tagBarRef.current;
    if (!bar) return undefined;
    const onWheel = (e: WheelEvent) => {
      if (bar.scrollWidth <= bar.clientWidth) return; // nothing to scroll
      const delta = Math.abs(e.deltaY) > Math.abs(e.deltaX) ? e.deltaY : e.deltaX;
      if (!delta) return;
      bar.scrollLeft += delta;
      e.preventDefault();
    };
    bar.addEventListener('wheel', onWheel, { passive: false });
    return () => bar.removeEventListener('wheel', onWheel);
  }, [tagCounts.length]);

  /** Set (or clear) the tag filter, then re-query the index from page 1. */
  const selectTag = useCallback((tag: string | undefined) => {
    useRemoteUiStore.getState().setTagFilter(tag);
    void loadIndex(1, useRemoteUiStore.getState().searchText);
  }, [loadIndex]);

  const openDetail = useCallback(
    (card: { detailUrl: string; title: string; key: string; tags: string[]; imported?: boolean; localModIds?: string[] }) => {
      const state = useRemoteUiStore.getState();
      if (!state.sourceId) return;
      let screenId = '';
      screenId = openScreen({
        title: card.title || t('remote.detailTitle'),
        content: (
          <RemoteModDetailScreen
            sourceId={state.sourceId}
            listId={state.listId}
            entryId={card.key}
            entryTags={card.tags}
            tagLabels={sources.find((s) => s.id === state.sourceId)?.tagLabels}
            detailUrl={card.detailUrl}
            fallbackTitle={card.title}
            imported={card.imported}
            localModIds={card.localModIds}
            onLocate={(modIds) => {
              closeScreen(screenId);
              if (selectedProfileId && modIds?.length) void navigateToModSearch(selectedProfileId, modIds);
            }}
          />
        ),
        // Responsive: use the window (huge dead side margins at a fixed 980px on wide screens).
        width: 'min(1180px, 92vw)',
        // The info card carries the mod name — the slide-in header would duplicate it and eat a row.
        headless: true,
      });
    },
    [openScreen, closeScreen, t, sources, selectedProfileId],
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
        sensitive: e.sensitive,
        imported: e.imported,
        localModIds: e.localModIds,
      }))
    : (ui.result?.cards ?? []).map((c) => ({
        key: c.detailUrl,
        title: c.title,
        detailUrl: c.detailUrl,
        imageUrl: c.imageUrl,
        tags: c.tags ?? [],
        dateHint: c.dateHint,
        sensitive: c.sensitive,
        imported: false,
        localModIds: undefined as string[] | undefined,
      }));
  const loaded = indexReady || !!ui.result;
  const syncedAt = ui.index?.info.syncedAtUtc;
  const syncActive = !!syncProcess && (syncProcess.status === 'running' || syncProcess.status === 'queued');

  // Content veil (global toggle): the IMAGE decides — the card shows only the thumbnail, so only
  // the thumbnail's own content matters. The site's content rating deliberately does NOT force a
  // veil (GB rates whole MODS; a rated mod often has a tame thumbnail — user decision 2026-07-10).
  // Veil-until-verdict, hover reveals, the detail screen is never veiled.
  const veilEnabled = useContentVeilEnabled();
  const veilVerdicts = useContentVeilVerdicts(cards.map((c) => remoteImageUrl(c.imageUrl)));
  const cardVeiled = (card: { imageUrl: string }): boolean =>
    isVeiled(veilEnabled, veilVerdicts[remoteImageUrl(card.imageUrl) ?? '']);

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
          onChange={(e) => {
            const value = e.target.value;
            ui.setSearchText(value);
            // The index search is OFFLINE (local SQLite) → fully reactive: search-as-you-type with a
            // short debounce; clearing resets instantly. Live (unsynced) mode still searches on Enter
            // to avoid spamming the site, but clearing resets the preview.
            if (searchDebounce.current) clearTimeout(searchDebounce.current);
            if (indexReady) {
              searchDebounce.current = setTimeout(() => void loadIndex(1, value), 250);
            } else if (value === '') {
              void browseLive(1);
            }
          }}
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
        {/* "Downloaded only" filter — show just the entries already imported into this profile. */}
        <CompactButton
          type={ui.downloadedOnly ? 'primary' : 'default'}
          icon={<CheckCircleFilled />}
          disabled={!indexReady}
          title={t('remote.downloadedOnly')}
          onClick={() => {
            ui.setDownloadedOnly(!ui.downloadedOnly);
            void loadIndex(1, useRemoteUiStore.getState().searchText);
          }}
        >
          {t('remote.downloaded')}
        </CompactButton>
        {/* Action icons: all grouped at the FAR RIGHT with separators (sync info + totals live in
            the bottom bar corners instead). */}
        <span className="remote-library__toolbar-divider remote-library__toolbar-divider--push" />
        <CompactIconButton icon={<ReloadOutlined />} title={t('common.refresh')} onClick={refresh} />
        <CompactIconButton
          icon={<CloudSyncOutlined />}
          title={t('remote.updateHint')}
          onClick={() => void startSync(false)}
        />
        <span className="remote-library__toolbar-divider" />
        <CompactIconButton icon={<AppstoreOutlined />} title={t('remote.manage')} onClick={openManagement} />
      </div>

      {/* Tag-filter chip strip (only once the index has tags). Horizontally scrollable; the active chip
          scrolls into view (effect above) and the strip has trailing end-padding so the last chip
          isn't flush against the edge. Filter drives the same index query as sort/search. */}
      {indexReady && tagCounts.length > 0 && (
        <div className="remote-library__tag-filter-bar" ref={tagBarRef}>
          <Tag.CheckableTag
            className="remote-library__tag-chip"
            checked={!ui.tagFilter}
            onChange={() => selectTag(undefined)}
          >
            {t('remote.tagAll')}
          </Tag.CheckableTag>
          {tagCounts.map((tc) => (
            <Tag.CheckableTag
              key={tc.name}
              className="remote-library__tag-chip"
              checked={ui.tagFilter === tc.name}
              onChange={(checked) => selectTag(checked ? tc.name : undefined)}
            >
              {remoteTagLabel(source?.tagLabels, i18n.language, tc.name)}
              <span className="remote-library__tag-chip-count">{tc.count}</span>
            </Tag.CheckableTag>
          ))}
        </div>
      )}

      {/* Sync PROGRESS moved to the bottom-left status slot (same place as the synced date). Only the
          never-synced CTA stays here; the grid fills progressively while a crawl runs. */}
      {!syncActive && !syncedAt && loaded && (
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
          <div className="remote-library__state">
            <Empty
              description={ui.downloadedOnly ? t('remote.noDownloaded') : t('remote.noResults')}
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          </div>
        )}
        {!loading && cards.length > 0 && (
          <div className="remote-library__grid">
            {cards.map((card) => (
              <div key={card.key} className="remote-card" onClick={() => openDetail(card)}>
                <div className="remote-card__image-wrap">
                  <ContentVeil veiled={cardVeiled(card)} badge={t('contentVeil.badge')}>
                    <img
                      className="remote-card__image"
                      src={remoteImageUrl(card.imageUrl)}
                      alt={card.title}
                      loading="lazy"
                      style={source?.thumbnail?.position ? { objectPosition: source.thumbnail.position } : undefined}
                    />
                  </ContentVeil>
                  {card.imported && (
                    <span className="remote-card__imported" title={t('remote.importedBadge')}>
                      <CheckCircleFilled />
                    </span>
                  )}
                  {/* Image overlays (corner badges, glass style): ONE tag (most specific, mapped)
                      + a +N counter TOP-LEFT; date BOTTOM-RIGHT — the meta row stays title-only. */}
                  {card.tags.length > 0 && (
                    <span className="remote-card__badge remote-card__badge--tag" title={card.tags.join(' · ')}>
                      {remoteTagLabel(source?.tagLabels, i18n.language, orderTagsForDisplay(card.tags)[0])}
                      {card.tags.length > 1 && (
                        <span className="remote-card__badge-more">+{card.tags.length - 1}</span>
                      )}
                    </span>
                  )}
                  {card.dateHint && (
                    <span className="remote-card__badge remote-card__badge--date">{card.dateHint}</span>
                  )}
                </div>
                <div className="remote-card__meta">
                  <div className="remote-card__title" title={card.title}>
                    {card.title || t('remote.untitled')}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Bottom bar, 3 zones: sync status (left — LIVE progress while syncing, else the synced date) ·
          button pager (center) · total count (right corner). Driven by OUR index count only. Shown
          while syncing too (so the status has a home even on the very first, index-empty crawl). */}
      {(indexReady || syncActive) && (
        <div className="remote-library__pager">
          <span className="remote-library__pager-side remote-library__pager-side--left" title={source?.baseUrl}>
            {syncActive ? (
              <span className="remote-library__syncing">
                <SyncOutlined spin /> {t('remote.syncing', { percent: syncProcess!.progress ?? 0 })}
              </span>
            ) : syncedAt ? (
              t('remote.lastSyncedTime', { time: formatDateTime(syncedAt) })
            ) : null}
          </span>
          {(ui.index?.total ?? 0) > INDEX_PAGE_SIZE ? (
            <Pagination
              current={ui.page}
              total={ui.index!.total}
              pageSize={INDEX_PAGE_SIZE}
              showSizeChanger={false}
              onChange={(p) => void loadIndex(p, ui.searchText)}
            />
          ) : (
            <span />
          )}
          <span className="remote-library__pager-side remote-library__pager-side--right">
            {t('remote.totalCount', { count: ui.index?.total ?? 0 })}
          </span>
        </div>
      )}
    </div>
  );
};
