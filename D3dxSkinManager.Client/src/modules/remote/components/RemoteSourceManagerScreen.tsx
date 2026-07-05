import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Input, Spin } from 'antd';
import { DeleteOutlined, EditOutlined, ExperimentOutlined, PlusOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { CompactButton } from '../../../shared/components/compact';
import { CompactIconButton } from '../../../shared/components/compact';
import { ConfirmDialog } from '../../../shared/components/dialogs/ConfirmDialog';
import type { RemoteSourceConfigDto, RemoteSourceInfo, RemoteSourceTestResult } from '../../../shared/types/remote.types';
import './RemoteSourceManagerScreen.css';

interface RemoteSourceManagerScreenProps {
  /** Called after any save/delete so the library view can reload its source list. */
  onChanged?: () => void;
}

/**
 * Manage remote-library adapters in-app: list, add (from the shipped template), edit, live-test a
 * candidate config against the real site (parse page 1 + first detail), delete. Adapters are plain
 * JSON — the editor is a validated JSON textarea, which keeps every current and future field
 * editable without a bespoke form per field.
 */
export const RemoteSourceManagerScreen: React.FC<RemoteSourceManagerScreenProps> = ({ onChanged }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [sources, setSources] = useState<RemoteSourceInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [editorText, setEditorText] = useState<string>();
  const [testing, setTesting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [testResult, setTestResult] = useState<RemoteSourceTestResult>();
  const [testError, setTestError] = useState<string>();
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

  const parseEditor = (): RemoteSourceConfigDto | undefined => {
    try {
      return JSON.parse(editorText ?? '') as RemoteSourceConfigDto;
    } catch (e) {
      setTestError(String(e));
      return undefined;
    }
  };

  const handleAdd = async () => {
    if (!selectedProfileId) return;
    try {
      const template = await api.remote.getSourceTemplate(selectedProfileId);
      setEditorText(template);
      setTestResult(undefined);
      setTestError(undefined);
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleEdit = async (source: RemoteSourceInfo) => {
    try {
      if (!selectedProfileId) return;
      const full = await api.remote.getSourceConfig(selectedProfileId, source.id);
      setEditorText(JSON.stringify(full, null, 2));
      setTestResult(undefined);
      setTestError(undefined);
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleTest = async () => {
    if (!selectedProfileId) return;
    const config = parseEditor();
    if (!config) return;
    try {
      setTesting(true);
      setTestError(undefined);
      setTestResult(await api.remote.testSource(selectedProfileId, config));
    } catch (error: unknown) {
      setTestResult(undefined);
      handleError(error);
    } finally {
      setTesting(false);
    }
  };

  const handleSave = async () => {
    if (!selectedProfileId) return;
    const config = parseEditor();
    if (!config) return;
    try {
      setSaving(true);
      const saved = await api.remote.saveSource(selectedProfileId, config);
      notification.success(t('remote.sourceSaved', { name: saved.name }));
      setEditorText(undefined);
      setTestResult(undefined);
      await reload();
      onChanged?.();
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setSaving(false);
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
                <span className="remote-source-manager__name">{source.name}</span>
                <span className="remote-source-manager__url">{source.baseUrl}</span>
              </div>
              <CompactIconButton icon={<EditOutlined />} title={t('remote.editSource')} onClick={() => void handleEdit(source)} />
              <CompactIconButton tone="danger" icon={<DeleteOutlined />} title={t('remote.deleteSource')} onClick={() => setDeleteTarget(source)} />
            </div>
          ))}
        <CompactButton icon={<PlusOutlined />} onClick={() => void handleAdd()}>
          {t('remote.addSource')}
        </CompactButton>
      </div>

      {editorText !== undefined && (
        <div className="remote-source-manager__editor">
          <div className="remote-source-manager__hint">{t('remote.editorHint')}</div>
          <Input.TextArea
            className="remote-source-manager__textarea"
            value={editorText}
            onChange={(e) => setEditorText(e.target.value)}
            autoSize={{ minRows: 14, maxRows: 22 }}
            spellCheck={false}
          />
          <div className="remote-source-manager__actions">
            <CompactButton icon={<ExperimentOutlined />} loading={testing} onClick={() => void handleTest()}>
              {t('remote.testSource')}
            </CompactButton>
            <CompactButton type="primary" loading={saving} onClick={() => void handleSave()}>
              {t('remote.saveSource')}
            </CompactButton>
          </div>
          {testError && <div className="remote-source-manager__test remote-source-manager__test--error">{testError}</div>}
          {testResult && (
            <div className="remote-source-manager__test">
              {testResult.cardCount === 0
                ? t('remote.testNoCards')
                : t('remote.testResult', {
                    cards: testResult.cardCount,
                    pages: testResult.totalPages ?? '?',
                    title: testResult.detailTitle ?? '—',
                    downloads: testResult.detailDownloads.length,
                    images: testResult.detailImageCount,
                  })}
              {testResult.sampleTitles.length > 0 && (
                <div className="remote-source-manager__samples">{testResult.sampleTitles.join(' · ')}</div>
              )}
            </div>
          )}
        </div>
      )}

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
