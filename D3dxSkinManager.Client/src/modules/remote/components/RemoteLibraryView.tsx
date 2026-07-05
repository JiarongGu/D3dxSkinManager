import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Empty, Pagination, Select, Spin } from 'antd';
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useSlideInScreenContext } from '../../../shared/context/SlideInScreenContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { CompactButton, CompactInput } from '../../../shared/components/compact';
import type { RemoteModCard, RemoteSourceInfo } from '../../../shared/types/remote.types';
import { useRemoteUiStore } from '../store/remoteUiStore';
import { RemoteModDetailScreen } from './RemoteModDetailScreen';
import './RemoteLibraryView.css';

/**
 * Remote mod library tab: browse configured remote sites (source → game list → pages / search),
 * open a mod's detail screen, download+import from there. Selection and the last result live in
 * remoteUiStore so leaving the tab and coming back restores where you were.
 */
export const RemoteLibraryView: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { openScreen } = useSlideInScreenContext();

  const [sources, setSources] = useState<RemoteSourceInfo[]>([]);
  const [loading, setLoading] = useState(false);

  const ui = useRemoteUiStore();
  const source = sources.find((s) => s.id === ui.sourceId);

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

  const browse = useCallback(
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
    if (!state.searchText.trim()) {
      void browse(1);
      return;
    }
    try {
      setLoading(true);
      const result = await api.remote.search(selectedProfileId, state.sourceId, state.searchText);
      state.setPage(1);
      state.setResult(result, true);
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setLoading(false);
    }
  }, [selectedProfileId, browse]);

  // First load (or returning to the tab with no cached result yet).
  useEffect(() => {
    if (!ui.result && ui.sourceId && ui.listId && !loading) void browse(ui.page);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ui.sourceId, ui.listId]);

  const openDetail = useCallback(
    (card: RemoteModCard) => {
      if (!ui.sourceId) return;
      openScreen({
        title: card.title || t('remote.detailTitle'),
        content: <RemoteModDetailScreen sourceId={ui.sourceId} detailUrl={card.detailUrl} fallbackTitle={card.title} />,
        width: '860px',
      });
    },
    [openScreen, t, ui.sourceId],
  );

  const cards = ui.result?.cards;

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
        {source?.hasSearch && (
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
        <CompactButton icon={<ReloadOutlined />} onClick={() => void (ui.isSearchResult ? runSearch() : browse(ui.page))}>
          {t('common.refresh')}
        </CompactButton>
        {source && (
          <span className="remote-library__origin" title={source.baseUrl}>
            {source.name}
          </span>
        )}
      </div>

      <div className="remote-library__content">
        {loading && (
          <div className="remote-library__loading">
            <Spin />
          </div>
        )}
        {!loading && cards && cards.length === 0 && (
          <Empty description={t('remote.noResults')} image={Empty.PRESENTED_IMAGE_SIMPLE} />
        )}
        {!loading && cards && cards.length > 0 && (
          <div className="remote-library__grid">
            {cards.map((card) => (
              <div key={card.detailUrl} className="remote-card" onClick={() => openDetail(card)}>
                <div className="remote-card__image-wrap">
                  <img className="remote-card__image" src={card.imageUrl} alt={card.title} loading="lazy" />
                </div>
                <div className="remote-card__title" title={card.title}>
                  {card.title || t('remote.untitled')}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {!ui.isSearchResult && ui.result && (ui.result.totalPages ?? 0) > 1 && (
        <div className="remote-library__pager">
          <Pagination
            simple
            size="small"
            current={ui.page}
            total={(ui.result.totalPages ?? 1) * 10}
            pageSize={10}
            showSizeChanger={false}
            onChange={(p) => void browse(p)}
          />
        </div>
      )}
    </div>
  );
};
