import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Spin } from 'antd';
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { CompactButton, CompactIconButton } from '../../../shared/components/compact';
import { ConfirmDialog } from '../../../shared/components/dialogs/ConfirmDialog';
import type { RemoteSourceConfig, RemoteSourceInfo } from '../../../shared/types/remote.types';
import { RemoteSourceEditor } from './RemoteSourceEditor';
import './RemoteSourceManagerScreen.css';

interface RemoteSourceManagerScreenProps {
  /** Called after any save/delete so the library view can reload its source list. */
  onChanged?: () => void;
}

/**
 * Manage remote-library adapters in-app: list, add, edit (via the form-based {@link RemoteSourceEditor} —
 * no more raw JSON), live-test a candidate config against the real site, delete. Adapters are still
 * plain JSON on disk; the editor exposes them as a friendly form (with an Advanced raw-JSON escape hatch).
 */
export const RemoteSourceManagerScreen: React.FC<RemoteSourceManagerScreenProps> = ({ onChanged }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [sources, setSources] = useState<RemoteSourceInfo[]>([]);
  const [loading, setLoading] = useState(true);
  // undefined = editor closed; { initial } = open (initial undefined → a new blank adapter).
  const [editing, setEditing] = useState<{ initial?: RemoteSourceConfig }>();
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
      setEditing({ initial: full });
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleSaved = async () => {
    setEditing(undefined);
    await reload();
    onChanged?.();
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

  if (editing) {
    return (
      <div className="remote-source-manager">
        <RemoteSourceEditor initial={editing.initial} onCancel={() => setEditing(undefined)} onSaved={handleSaved} />
      </div>
    );
  }

  return (
    <div className="remote-source-manager">
      <div className="remote-source-manager__list">
        {loading && <Spin size="small" />}
        {!loading &&
          sources.map((source) => (
            <div key={source.id} className="remote-source-manager__row">
              <div className="remote-source-manager__row-main">
                <span className="remote-source-manager__name">{source.name}</span>
                <span className="remote-source-manager__url">{source.baseUrl}</span>
              </div>
              <CompactIconButton icon={<EditOutlined />} title={t('remote.editSource')} onClick={() => void handleEdit(source)} />
              <CompactIconButton tone="danger" icon={<DeleteOutlined />} title={t('remote.deleteSource')} onClick={() => setDeleteTarget(source)} />
            </div>
          ))}
        <CompactButton icon={<PlusOutlined />} onClick={() => setEditing({ initial: undefined })}>
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
