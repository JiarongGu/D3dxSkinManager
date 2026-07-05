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
import { PillLabel } from '../../../shared/components/common/PillLabel';
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
  detailUrl,
  fallbackTitle,
}) => {
  const { t } = useTranslation();
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

  return (
    <div className="remote-detail">
      {/* LEFT: review — tags + gallery (title comes from the slide-in header). */}
      <div className="remote-detail__review">
        {allTags.length > 0 && (
          <div className="remote-detail__tags">
            {allTags.map((tag) => (
              <PillLabel key={tag} label={tag} title={tag} />
            ))}
          </div>
        )}
        <ImageGallery
          images={detail.images}
          resolveSrc={(u) => remoteImageUrl(u) ?? u}
          alt={detail.title}
        />
        {detail.images.length === 0 && (
          <div className="remote-detail__no-images">{t('remote.noImages')}</div>
        )}
      </div>

      {/* RIGHT: actions — open page + every download option stacked. */}
      <div className="remote-detail__actions">
        <div className="remote-detail__actions-title">{t('remote.actionsTitle')}</div>
        <CompactButton icon={<GlobalOutlined />} onClick={() => void api.system.openUrl(detail.detailUrl)}>
          {t('remote.openPage')}
        </CompactButton>
        {detail.downloads.length === 0 && (
          <span className="remote-detail__no-download">{t('remote.noDownloads')}</span>
        )}
        {detail.downloads.map((option) => (
          <CompactButton
            key={option.url}
            type={isImportable(option.type) ? 'primary' : 'default'}
            icon={isImportable(option.type) ? <CloudDownloadOutlined /> : <LinkOutlined />}
            loading={resolving === option.url}
            onClick={() => void handleDownload(option)}
          >
            {isImportable(option.type)
              ? t('remote.downloadImport', { host: option.name })
              : t('remote.openExternal', { host: option.name })}
          </CompactButton>
        ))}
        <span className="remote-detail__meta">
          {t('remote.detailMeta', { images: detail.images.length, downloads: detail.downloads.length })}
        </span>
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
