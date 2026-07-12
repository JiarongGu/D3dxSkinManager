import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Spin } from 'antd';
import { DeleteOutlined, DiffOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { CompactButton, CompactIconButton } from '../../../shared/components/compact';
import { StatusTag } from '../../../shared/components/common/StatusTag';
import { ConfirmDialog } from '../../../shared/components/dialogs/ConfirmDialog';
import type { RemoteSourceConfig, RemoteSourceInfo } from '../../../shared/types/remote.types';
import './RemoteSourceManagerScreen.css';

interface RemoteSourceManagerScreenProps {
  /** Called after a delete so the library view can reload its source list. */
  onChanged?: () => void;
  /** Open the site editor — `config` = an existing adapter to edit, undefined = a new blank one.
   * `origin` enables the editor's "compare with default" for a customized source.
   * Editing is hosted by the parent as a dedicated full screen (pinned header + actions). */
  onEdit: (config?: RemoteSourceConfig, origin?: RemoteSourceInfo['origin']) => void;
}

/**
 * The remote-library ADAPTER LIST (list + delete + open-editor). Editing/adding is lifted to the parent
 * ({@link RemoteLibraryManagementScreen}) so it gets a dedicated screen with a pinned header and actions
 * — consistent with the library editor. This component just lists sites and routes edit/add up.
 */
export const RemoteSourceManagerScreen: React.FC<RemoteSourceManagerScreenProps> = ({ onChanged, onEdit }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [sources, setSources] = useState<RemoteSourceInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [deleteTarget, setDeleteTarget] = useState<RemoteSourceInfo>();

  const reload = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      setLoading(true);
      setSources(await api.remote.getSources(selectedProfileId));
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setLoading(false);
    }
  }, [selectedProfileId]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const handleEdit = async (source: RemoteSourceInfo) => {
    if (!selectedProfileId) return;
    try {
      const full = await api.remote.getSourceConfig(selectedProfileId, source.id);
      onEdit(full, source.origin);
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleDelete = async () => {
    if (!selectedProfileId || !deleteTarget) return;
    try {
      await api.remote.deleteSource(selectedProfileId, deleteTarget.id);
      notification.success(t('remote.sourceDeleted'));
      setDeleteTarget(undefined);
      await reload();
      onChanged?.();
    } catch (error: unknown) {
      handleError(error);
    }
  };

  return (
    <div className="remote-source-manager">
      <div className="remote-source-manager__list">
        {loading && <Spin size="small" />}
        {!loading &&
          sources.map((source) => (
            <div key={source.id} className="remote-source-manager__row">
              <div className="remote-source-manager__row-main">
                <div className="remote-source-manager__name-row">
                  <span className="remote-source-manager__name">{source.name}</span>
                  {/* No chip for a plain default (== master); "Modified" when it really differs, "Custom"
                      for a user-added source. Same-as-master overlays are dropped backend-side. */}
                  {source.origin === 'customized' && (
                    <StatusTag
                      tone="warning"
                      icon={<DiffOutlined />}
                      label={t('remote.origin.customized')}
                      title={t('remote.origin.customizedHint')}
                    />
                  )}
                  {source.origin === 'custom' && (
                    <StatusTag tone="info" icon={null} label={t('remote.origin.custom')} />
                  )}
                </div>
                <span className="remote-source-manager__url">{source.baseUrl}</span>
              </div>
              {/* Editing a shipped source is "use as template" — Save writes a sparse local overlay. */}
              <CompactIconButton
                icon={<EditOutlined />}
                title={source.origin === 'default' ? t('remote.useAsTemplate') : t('remote.editSource')}
                onClick={() => void handleEdit(source)}
              />
              {/* A custom (user-added) source can be deleted. A "modified" shipped source is reverted
                  via the editor's Compare-with-default (Take all) — no separate reset button needed. */}
              {source.origin === 'custom' && (
                <CompactIconButton
                  tone="danger"
                  icon={<DeleteOutlined />}
                  title={t('remote.deleteSource')}
                  onClick={() => setDeleteTarget(source)}
                />
              )}
            </div>
          ))}
        <CompactButton icon={<PlusOutlined />} onClick={() => onEdit(undefined)}>
          {t('remote.addSource')}
        </CompactButton>
      </div>

      <ConfirmDialog
        visible={!!deleteTarget}
        title={t('remote.deleteSource')}
        okType="danger"
        content={deleteTarget ? t('remote.deleteSourceConfirm', { name: deleteTarget.name }) : null}
        onOk={handleDelete}
        onCancel={() => setDeleteTarget(undefined)}
      />
    </div>
  );
};
