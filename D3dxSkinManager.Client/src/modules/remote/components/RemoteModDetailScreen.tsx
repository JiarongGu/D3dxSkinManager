import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Spin } from 'antd';
import { CloudDownloadOutlined, GlobalOutlined, LinkOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { formatBytes } from '../../../shared/utils/formatBytes';
import { remoteImageUrl } from '../../../shared/utils/imageUrlHelper';
import { CompactButton } from '../../../shared/components/compact';
import { ConfirmDialog } from '../../../shared/components/dialogs/ConfirmDialog';
import { KeyValueRows } from '../../../shared/components/common/KeyValueRows';
import { ImageGallery } from '../../../shared/components/common/ImageGallery';
import { orderTagsForDisplay, remoteTagLabel } from '../../../shared/utils/remoteTagLabel';
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
}) => {
  const { t, i18n } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [detail, setDetail] = useState<RemoteModDetail>();
  const [loading, setLoading] = useState(true);
  const [resolving, setResolving] = useState<string>();
  const [categories, setCategories] = useState<CategoryInfo[]>([]);
  // Download-time category choice (undefined = follow the library's tag rules).
  const [importCategory, setImportCategory] = useState<string>();
  const [confirmState, setConfirmState] = useState<{
    option: RemoteDownloadOption;
    resolved: RemoteResolveResult;
  }>();

  useEffect(() => {
    if (!selectedProfileId) return;
    void (async () => {
      try {
        setLoading(true);
        const [loaded, cats] = await Promise.all([
          api.remote.getDetail(selectedProfileId, sourceId, detailUrl, listId),
          api.category.getCategoryTree(selectedProfileId).catch(() => [] as CategoryInfo[]),
        ]);
        setDetail(loaded);
        setCategories(cats);
        // Images render through the app://remote-image proxy — fetched+cached on demand, no preload.
      } catch (error: unknown) {
        handleError(error);
      } finally {
        setLoading(false);
      }
    })();
  }, [selectedProfileId, sourceId, detailUrl]);

  // Cloudreve (share API) AND direct (URL is the file, e.g. GameBanana /dl/{id}) both download+import
  // in-app; everything else (Quark, unknown hosts) opens in the browser.
  const isImportable = (type: string) => type === 'cloudreve' || type === 'direct';

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
      });
      notification.info(t('remote.importStarted', { name: detail.title || fallbackTitle }));
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setConfirmState(undefined);
    }
  };

  if (loading) {
    return (
      <div className="remote-detail__loading">
        <Spin />
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
    </div>
  );

  const titleAndTags = (
    <>
      <div className="remote-detail__name" title={detail.title || fallbackTitle}>
        {detail.title || fallbackTitle}
      </div>
      {allTags.length > 0 && (
        <div className="remote-detail__tags">
          {orderTagsForDisplay(allTags).map((tag) => (
            <span key={tag} className="remote-detail__hero-tag remote-detail__hero-tag--flat" title={tag}>
              {remoteTagLabel(tagLabels, i18n.language, tag)}
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
                        {orderTagsForDisplay(allTags).map((tag) => (
                          <span key={tag} className="remote-detail__hero-tag" title={tag}>
                            {remoteTagLabel(tagLabels, i18n.language, tag)}
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
            </div>
          )
        }
      />
    </div>
  );
};
