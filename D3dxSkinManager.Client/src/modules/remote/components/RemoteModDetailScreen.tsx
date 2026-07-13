import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Skeleton } from 'antd';
import { useDelayedLoading } from '../../../shared/hooks/useDelayedLoading';
import { CheckCircleFilled, CloudDownloadOutlined, GlobalOutlined, LinkOutlined, ReloadOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { formatBytes } from '../../../shared/utils/formatBytes';
import { remoteImageUrl } from '../../../shared/utils/imageUrlHelper';
import { CompactButton, CompactPassword } from '../../../shared/components/compact';
import { ConfirmDialog } from '../../../shared/components/dialogs/ConfirmDialog';
import { KeyValueRows } from '../../../shared/components/common/KeyValueRows';
import { ImageGallery } from '../../../shared/components/common/ImageGallery';
import { orderTagsForDisplay, remoteTagLabelsDeduped } from '../../../shared/utils/remoteTagLabel';
import { useProcessStore } from '../../../shared/store/processStore';
import { CategorySelect } from '../../../shared/components/CategorySelect';
import type { CategoryInfo } from '../../../shared/types/category.types';
import type {
  RemoteDownloadOption,
  RemoteModDetail,
  RemoteResolveResult,
} from '../../../shared/types/remote.types';
import './RemoteModDetailScreen.css';

interface RemoteModDetailScreenProps {
  sourceId: string;
  /** Index context (when opened from a synced entry) — records the durable import identity. */
  listId?: string;
  entryId?: string;
  /** Tags known from the index entry/card (merged with the detail page's own tags). */
  entryTags?: string[];
  /** The source's per-language tag label table (display-only mapping). */
  tagLabels?: Record<string, Record<string, string>>;
  detailUrl: string;
  fallbackTitle?: string;
  /** True when this entry was already imported into the current profile. */
  imported?: boolean;
  /** Local mod id(s) imported from this entry (when imported) — enables the "locate" jump. Multiple
   * when the entry was downloaded more than once. */
  localModIds?: string[];
  /** Jump to the imported local mod(s) in the mod list (closes this screen). */
  onLocate?: (localModIds?: string[]) => void;
  /** The library's cache-first option — used for the initial detail fetch. The Refresh button always
   *  forces a live pull regardless. */
  preferCache?: boolean;
}

/**
 * Slide-in detail for one remote mod — LEFT review (ImageGallery + tags) / RIGHT actions (open page +
 * download options). Importable options (cloudreve/direct) confirm with the real file name/size AND a
 * download-time category picker (overrides the library's tag rules), then start the background
 * download+import; external hosts open in the system browser.
 */
export const RemoteModDetailScreen: React.FC<RemoteModDetailScreenProps> = ({
  sourceId,
  listId,
  entryId,
  entryTags,
  tagLabels,
  detailUrl,
  fallbackTitle,
  imported,
  localModIds,
  onLocate,
  preferCache,
}) => {
  const { t, i18n } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [detail, setDetail] = useState<RemoteModDetail>();
  // Delayed loading: only surfaces the skeleton if the fetch takes >150ms (no flash for fast loads).
  const { loading, execute } = useDelayedLoading(150);
  const [resolving, setResolving] = useState<string>();
  const [categories, setCategories] = useState<CategoryInfo[]>([]);
  // Download-time category choice (undefined = follow the library's tag rules).
  const [importCategory, setImportCategory] = useState<string>();
  // User unzip password (empty = the resolver's site default; imports always extract + repack).
  const [importPassword, setImportPassword] = useState('');
  const [confirmState, setConfirmState] = useState<{
    option: RemoteDownloadOption;
    resolved: RemoteResolveResult;
  }>();
  // Live imported state — seeded from the open-time props, refreshed when a background remote
  // import COMPLETES (metadata written + lookup invalidated before Complete), so the banner
  // appears without reopening the screen. Same completed-count trigger as the browse grid.
  const [importedState, setImportedState] = useState<{ imported: boolean; localModIds?: string[] }>({
    imported: !!imported,
    localModIds,
  });
  const importDoneCount = useProcessStore(
    (s) => s.processes.filter((p) => p.titleKey === 'process.remoteImport' && p.status === 'completed').length,
  );
  const prevImportDone = useRef(importDoneCount);
  useEffect(() => {
    const increased = importDoneCount > prevImportDone.current;
    prevImportDone.current = importDoneCount;
    if (!increased || !selectedProfileId) return;
    void api.remote
      .getImportedState(selectedProfileId, sourceId, listId, entryId, detailUrl)
      .then((s) => setImportedState({ imported: s.imported, localModIds: s.localModIds }))
      .catch(() => { /* banner refresh is best-effort */ });
  }, [importDoneCount, selectedProfileId, sourceId, listId, entryId, detailUrl]);

  useEffect(() => {
    if (!selectedProfileId) return;
    void (async () => {
      try {
        await execute(async () => {
          const [loaded, cats] = await Promise.all([
            api.remote.getDetail(selectedProfileId, sourceId, detailUrl, listId, preferCache),
            api.category.getCategoryTree(selectedProfileId).catch(() => [] as CategoryInfo[]),
          ]);
          setDetail(loaded);
          setCategories(cats);
          // Images render through the app://remote-image proxy — fetched+cached on demand, no preload.
          // Preselect the category the library's tag rules would assign (same logic the import uses),
          // so the download-confirm popup shows it up-front. Best-effort — no match leaves it unset.
          try {
            const tags = Array.from(new Set([...(entryTags ?? []), ...(loaded.tags ?? [])]));
            const ruleCategory = await api.remote.resolveImportCategory(
              selectedProfileId, sourceId, listId, tags, loaded.title || fallbackTitle);
            if (ruleCategory) setImportCategory(ruleCategory);
          } catch {
            /* preselect is best-effort — a resolve failure must not break the detail view */
          }
        });
      } catch (error: unknown) {
        // A concurrent invocation (React StrictMode double-invokes this effect on the same instance
        // while the first load is still in-flight) makes execute() throw — that's benign, the first
        // load handles it. Only surface real failures. Mirrors ConfirmDialog/FormDialog.
        if (error instanceof Error && error.message === 'Operation already in progress') return;
        handleError(error);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId, sourceId, detailUrl]);

  /** Re-fetch the detail page LIVE (bypasses the library's cache-first option) + persist the fresh copy. */
  const refreshDetail = async () => {
    if (!selectedProfileId) return;
    try {
      await execute(async () => {
        const loaded = await api.remote.getDetail(selectedProfileId, sourceId, detailUrl, listId, false);
        setDetail(loaded);
      });
      notification.info(t('remote.detailRefreshed'));
    } catch (error: unknown) {
      if (error instanceof Error && error.message === 'Operation already in progress') return;
      handleError(error);
    }
  };

  // Cloudreve (share API) AND direct (URL is the file, e.g. GameBanana /dl/{id}) both download+import
  // in-app; everything else (Quark, unknown hosts) opens in the browser.
  // quark imports in-app when a Quark account is logged in (Settings → Online Storage); if not, the
  // resolve surfaces QUARK_NOT_LOGGED_IN which tells the user to add one.
  const isImportable = (type: string) => type === 'cloudreve' || type === 'direct' || type === 'quark';

  // Entry tags (index) ∪ detail-page tags — shown as chips + fed to the import's tag rules.
  const allTags = Array.from(new Set([...(entryTags ?? []), ...(detail?.tags ?? [])]));

  const handleDownload = async (option: RemoteDownloadOption) => {
    if (!selectedProfileId) return;
    if (!isImportable(option.type)) {
      // Unsupported host — hand off to the system browser.
      void api.system.openUrl(option.url);
      return;
    }
    try {
      setResolving(option.url);
      const resolved = await api.remote.resolveDownload(selectedProfileId, option);
      setConfirmState({ option, resolved });
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setResolving(undefined);
    }
  };

  const handleConfirmImport = async () => {
    if (!selectedProfileId || !detail || !confirmState) return;
    try {
      await api.remote.downloadImport(selectedProfileId, sourceId, detail, confirmState.option, {
        listId,
        entryId,
        tags: allTags,
        categoryId: importCategory,
        password: importPassword.trim() || undefined,
      });
      notification.info(t('remote.importStarted', { name: detail.title || fallbackTitle }));
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setConfirmState(undefined);
    }
  };

  // Layout-shaped skeleton (matches the real LEFT gallery / RIGHT actions split) — only shown once the
  // delayed-loading hook flips (fast loads never flash it).
  if (loading) {
    return (
      <div className="remote-detail remote-detail--page">
        <div className="remote-detail__body">
          <div className="remote-detail__main">
            <Skeleton.Node active className="remote-detail__skeleton-hero"><span /></Skeleton.Node>
            <div className="remote-detail__panel remote-detail__info">
              <Skeleton active title={{ width: '40%' }} paragraph={{ rows: 3 }} />
            </div>
          </div>
          <div className="remote-detail__panel remote-detail__actions">
            <Skeleton active title={{ width: '50%' }} paragraph={{ rows: 5 }} />
          </div>
        </div>
      </div>
    );
  }

  if (!detail) return null;

  // ONE layout for every site: LEFT = hero gallery (blurred-cover backdrop, title/tags on the
  // scrim) that GROWS to fill the column + description below; RIGHT = downloads, always full
  // height. No images → title/tags move into the info card (the slide-in is headless, so the
  // title must live in the content).
  const hasImages = detail.images.length > 0;

  const downloadsCard = (
    <div className="remote-detail__panel remote-detail__actions">
      <div className="remote-detail__actions-title">
        {t('remote.actionsTitle')}
        {detail.downloads.length > 0 && (
          <span className="remote-detail__actions-count">{detail.downloads.length}</span>
        )}
      </div>
      {detail.downloads.length === 0 && (
        <span className="remote-detail__no-download">{t('remote.noDownloads')}</span>
      )}
      {/* One clean row per file: the NAME gets its own ellipsized line, the action is a short
          constant label — no filename-inside-parentheses button soup. */}
      {detail.downloads.map((option) => (
        <div key={option.url} className="remote-detail__dl-row">
          <span className="remote-detail__dl-name" title={option.name}>
            {isImportable(option.type) ? <CloudDownloadOutlined /> : <LinkOutlined />}
            <span className="remote-detail__dl-text">{option.name}</span>
            {option.sizeBytes ? (
              <span className="remote-detail__dl-size">{formatBytes(option.sizeBytes)}</span>
            ) : null}
          </span>
          <CompactButton
            size="small"
            type={isImportable(option.type) ? 'primary' : 'default'}
            loading={resolving === option.url}
            onClick={() => void handleDownload(option)}
          >
            {isImportable(option.type) ? t('remote.importAction') : t('remote.openAction')}
          </CompactButton>
        </div>
      ))}
      <div className="remote-detail__divider" />
      <CompactButton icon={<GlobalOutlined />} onClick={() => void api.system.openUrl(detail.detailUrl)}>
        {t('remote.openPage')}
      </CompactButton>
      {/* Force a LIVE re-fetch of the detail page (bypasses the library's cache-first option). */}
      <CompactButton icon={<ReloadOutlined />} loading={loading} onClick={() => void refreshDetail()}>
        {t('remote.refreshDetail')}
      </CompactButton>

      {/* Already-imported banner at the BOTTOM (clear of the headless slide-in's floating close button) —
          "locate" jumps to the imported local mod(s) in the mod list (multiple if downloaded > once). */}
      {importedState.imported && (
        <div className="remote-detail__imported">
          <span className="remote-detail__imported-label">
            <CheckCircleFilled /> {t('remote.importedBadge')}
          </span>
          {importedState.localModIds?.length && onLocate ? (
            <CompactButton size="small" onClick={() => onLocate(importedState.localModIds)}>
              {importedState.localModIds.length > 1
                ? t('remote.locateModN', { count: importedState.localModIds.length })
                : t('remote.locateMod')}
            </CompactButton>
          ) : null}
        </div>
      )}
    </div>
  );

  const titleAndTags = (
    <>
      <div className="remote-detail__name" title={detail.title || fallbackTitle}>
        {detail.title || fallbackTitle}
      </div>
      {allTags.length > 0 && (
        <div className="remote-detail__tags">
          {remoteTagLabelsDeduped(tagLabels, i18n.language, orderTagsForDisplay(allTags)).map(({ key, label }) => (
            <span key={key} className="remote-detail__hero-tag remote-detail__hero-tag--flat" title={label}>
              {label}
            </span>
          ))}
        </div>
      )}
    </>
  );

  const description = detail.description
    ? <div className="remote-detail__description">{detail.description}</div>
    : <span className="remote-detail__no-download">{t('remote.noDescription')}</span>;

  return (
    <div className="remote-detail remote-detail--page">
      <div className="remote-detail__body">
        <div className="remote-detail__main">
          {hasImages ? (
            <>
              {/* Hero gallery fills the column (width bounded by the downloads column). */}
              <ImageGallery
                className="remote-detail__hero"
                images={detail.images}
                resolveSrc={(u) => remoteImageUrl(u) ?? u}
                alt={detail.title}
                backdropBlur
                stageHeight="auto"
                overlay={
                  <div className="remote-detail__hero-meta">
                    <div className="remote-detail__hero-title" title={detail.title || fallbackTitle}>
                      {detail.title || fallbackTitle}
                    </div>
                    {allTags.length > 0 && (
                      <div className="remote-detail__hero-tags">
                        {remoteTagLabelsDeduped(tagLabels, i18n.language, orderTagsForDisplay(allTags)).map(({ key, label }) => (
                          <span key={key} className="remote-detail__hero-tag" title={label}>
                            {label}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                }
              />
              <div className="remote-detail__panel remote-detail__info">{description}</div>
            </>
          ) : (
            <div className="remote-detail__panel remote-detail__info remote-detail__info--fill">
              {titleAndTags}
              <div className="remote-detail__divider" />
              {description}
            </div>
          )}
        </div>
        {downloadsCard}
      </div>

      <ConfirmDialog
        visible={!!confirmState}
        title={t('remote.confirmImportTitle')}
        onOk={handleConfirmImport}
        onCancel={() => setConfirmState(undefined)}
        content={
          confirmState && (
            <div className="remote-detail__confirm">
              <KeyValueRows
                boxed
                rows={[
                  { label: t('remote.confirmMod'), value: detail.title || fallbackTitle || '' },
                  { label: t('remote.confirmFile'), value: confirmState.resolved.fileName },
                  { label: t('remote.confirmSize'), value: formatBytes(confirmState.resolved.size) },
                  { label: t('remote.confirmHost'), value: confirmState.option.name },
                ]}
                hint={t('remote.confirmHint')}
              />
              <div className="remote-detail__confirm-category">
                <span className="remote-detail__confirm-category-label">{t('remote.confirmCategory')}</span>
                <CategorySelect
                  categories={categories}
                  value={importCategory}
                  placeholder={t('remote.confirmCategoryByRules')}
                  onChange={setImportCategory}
                  size="small"
                />
              </div>
              {/* Imports always extract + repack into our storage format — the password feeds that
                  extraction. Empty = the site's known default (shown as the placeholder). */}
              <div className="remote-detail__confirm-category">
                <span className="remote-detail__confirm-category-label">{t('remote.confirmPassword')}</span>
                <CompactPassword
                  size="small"
                  value={importPassword}
                  placeholder={confirmState.option.unzipPassword
                    ? t('remote.confirmPasswordDefault', { password: confirmState.option.unzipPassword })
                    : t('remote.confirmPasswordNone')}
                  onChange={(e) => setImportPassword(e.target.value)}
                />
              </div>
            </div>
          )
        }
      />
    </div>
  );
};
