import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Spin } from 'antd';
import { CloudDownloadOutlined, LinkOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { formatBytes } from '../../../shared/utils/formatBytes';
import { CompactButton } from '../../../shared/components/compact';
import { ConfirmDialog } from '../../../shared/components/dialogs/ConfirmDialog';
import { KeyValueRows } from '../../../shared/components/common/KeyValueRows';
import type {
  RemoteDownloadOption,
  RemoteModDetail,
  RemoteResolveResult,
} from '../../../shared/types/remote.types';
import './RemoteModDetailScreen.css';

interface RemoteModDetailScreenProps {
  sourceId: string;
  detailUrl: string;
  fallbackTitle?: string;
}

/**
 * Slide-in detail screen for one remote mod: preview images + download options.
 * Resolvable options (Cloudreve) confirm with the real file name/size, then start the background
 * download+import (result lands in the Activity panel + mod list). External options open in the
 * system browser.
 */
export const RemoteModDetailScreen: React.FC<RemoteModDetailScreenProps> = ({
  sourceId,
  detailUrl,
  fallbackTitle,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [detail, setDetail] = useState<RemoteModDetail>();
  const [loading, setLoading] = useState(true);
  const [activeImage, setActiveImage] = useState(0);
  const [resolving, setResolving] = useState<string>();
  const [confirmState, setConfirmState] = useState<{
    option: RemoteDownloadOption;
    resolved: RemoteResolveResult;
  }>();

  useEffect(() => {
    if (!selectedProfileId) return;
    void (async () => {
      try {
        setLoading(true);
        setDetail(await api.remote.getDetail(selectedProfileId, sourceId, detailUrl));
      } catch (error: unknown) {
        handleError(error);
      } finally {
        setLoading(false);
      }
    })();
  }, [selectedProfileId, sourceId, detailUrl]);

  const handleDownload = async (option: RemoteDownloadOption) => {
    if (!selectedProfileId) return;
    if (option.type !== 'cloudreve') {
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
      await api.remote.downloadImport(selectedProfileId, sourceId, detail, confirmState.option);
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
      <div className="remote-detail__header">
        <h2 className="remote-detail__title">{detail.title || fallbackTitle}</h2>
        <div className="remote-detail__actions">
          {detail.downloads.length === 0 && (
            <span className="remote-detail__no-download">{t('remote.noDownloads')}</span>
          )}
          {detail.downloads.map((option) => (
            <CompactButton
              key={option.url}
              type={option.type === 'cloudreve' ? 'primary' : 'default'}
              icon={option.type === 'cloudreve' ? <CloudDownloadOutlined /> : <LinkOutlined />}
              loading={resolving === option.url}
              onClick={() => void handleDownload(option)}
            >
              {option.type === 'cloudreve'
                ? t('remote.downloadImport', { host: option.name })
                : t('remote.openExternal', { host: option.name })}
            </CompactButton>
          ))}
        </div>
      </div>

      {detail.images.length > 0 && (
        <div className="remote-detail__gallery">
          <div className="remote-detail__main-image-wrap">
            <img
              className="remote-detail__main-image"
              src={detail.images[Math.min(activeImage, detail.images.length - 1)]}
              alt={detail.title}
            />
          </div>
          {detail.images.length > 1 && (
            <div className="remote-detail__thumbs">
              {detail.images.map((image, index) => (
                <img
                  key={image}
                  src={image}
                  alt=""
                  loading="lazy"
                  className={
                    index === activeImage
                      ? 'remote-detail__thumb remote-detail__thumb--active'
                      : 'remote-detail__thumb'
                  }
                  onClick={() => setActiveImage(index)}
                />
              ))}
            </div>
          )}
        </div>
      )}

      <ConfirmDialog
        visible={!!confirmState}
        title={t('remote.confirmImportTitle')}
        onOk={handleConfirmImport}
        onCancel={() => setConfirmState(undefined)}
        content={
          confirmState && (
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
          )
        }
      />
    </div>
  );
};
