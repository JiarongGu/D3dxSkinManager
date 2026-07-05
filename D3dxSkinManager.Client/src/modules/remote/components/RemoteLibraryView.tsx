import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Empty, Pagination, Select, Spin, Tooltip } from 'antd';
import { CheckCircleFilled, ReloadOutlined, SearchOutlined, SyncOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useSlideInScreenContext } from '../../../shared/context/SlideInScreenContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { CompactButton, CompactInput } from '../../../shared/components/compact';
import type { RemoteSourceInfo } from '../../../shared/types/remote.types';
import { useRemoteUiStore } from '../store/remoteUiStore';
import { RemoteModDetailScreen } from './RemoteModDetailScreen';
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

  const ui = useRemoteUiStore();
  const source = sources.find((s) => s.id === ui.sourceId);
  const indexReady = (ui.index?.info.entryCount ?? 0) > 0;

  // Load the configured sources once per profile; restore/derive the selection.
  useEffect(() => {
    if (!selectedProfileId) return;
    ui.ensureProfile(selectedProfileId);
    void (async () => {
      try {
        const list = await api.remote.getSources(selectedProfileId);
        setSources(list);
        const state = useRemoteUiStore.getState();
        if (!state.sourceId && list.length > 0) state.setSource(list[0].id);
        const src = list.find((s) => s.id === useRemoteUiStore.getState().sourceId);
        if (src && !useRemoteUiStore.getState().listId && src.lists.length > 0) {
          useRemoteUiStore.getState().setList(src.lists[0].id);
        }
      } catch (error: unknown) {
        handleError(error);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId]);

  /** Query the synced index; when it's empty (never synced), fall back to live browsing. */
  const loadIndex = useCallback(
    async (page: number, search?: string) => {
      const state = useRemoteUiStore.getState();
      if (!selectedProfileId || !state.sourceId || !state.listId) return;
      try {
        setLoading(true);
        const index = await api.remote.indexQuery(
          selectedProfileId, state.sourceId, state.listId, search?.trim() || undefined, page, INDEX_PAGE_SIZE);
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

  return (
    <div className="remote-library">
      <div className="remote-library__toolbar">
        {sources.length > 1 && (
          <Select
            className="remote-library__source"
            size="small"
            value={ui.sourceId}
            options={sources.map((s) => ({ value: s.id, label: s.name }))}
            onChange={(v) => {
              ui.setSource(v);
              const src = sources.find((s) => s.id === v);
              if (src && src.lists.length > 0) useRemoteUiStore.getState().setList(src.lists[0].id);
            }}
          />
        )}
        {source && source.lists.length > 0 && (
          <Select
            className="remote-library__list"
            size="small"
            value={ui.listId}
            options={source.lists.map((l) => ({ value: l.id, label: l.name }))}
            onChange={(v) => ui.setList(v)}
          />
        )}
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
        <CompactButton icon={<ReloadOutlined />} onClick={refresh}>
          {t('common.refresh')}
        </CompactButton>
        <Tooltip title={t('remote.notSynced')}>
          <CompactButton icon={<SyncOutlined />} onClick={() => void startSync()}>
            {t('remote.sync')}
          </CompactButton>
        </Tooltip>
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
                  <img className="remote-card__image" src={card.imageUrl} alt={card.title} loading="lazy" />
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
            size="small"
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
            size="small"
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
