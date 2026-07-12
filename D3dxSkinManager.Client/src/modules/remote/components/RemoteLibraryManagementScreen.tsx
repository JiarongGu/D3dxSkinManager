import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Spin, Tabs } from 'antd';
import {
  ArrowLeftOutlined,
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import {
  CompactButton,
  CompactIconButton,
  CompactSelect,
} from '../../../shared/components/compact';
import { StatusTag } from '../../../shared/components/common/StatusTag';
import { ConfirmDialog } from '../../../shared/components/dialogs/ConfirmDialog';
import type { CategoryInfo } from '../../../shared/types/category.types';
import type {
  RemoteLibrariesState,
  RemoteLibrary,
  RemoteSourceConfig,
  RemoteSourceInfo,
} from '../../../shared/types/remote.types';
import { RemoteSourceManagerScreen } from './RemoteSourceManagerScreen';
import { RemoteSourceEditor } from './RemoteSourceEditor';
import { LibraryEditView } from './LibraryEditView';
import { ParamField } from './ParamField';
import './RemoteLibraryManagementScreen.css';

interface RemoteLibraryManagementScreenProps {
  /** Called after any library change so the main view can reload. */
  onChanged?: () => void;
}

/**
 * Library management (remote-library-redesign.md), TWO TABS: "My libraries" (the profile's configured
 * libraries — the everyday tab: add site→game→sync, switch active, edit name + ORDERED tag→category
 * import rules, remove) and "Sites" (the adapter configs — rarely used; shipped ones are read-only
 * defaults in res/, custom sites live here). This is the thin router: the library editor lives in
 * LibraryEditView, the site editor in RemoteSourceEditor.
 */
export const RemoteLibraryManagementScreen: React.FC<RemoteLibraryManagementScreenProps> = ({ onChanged }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [sources, setSources] = useState<RemoteSourceInfo[]>([]);
  const [libState, setLibState] = useState<RemoteLibrariesState>();
  const [categories, setCategories] = useState<CategoryInfo[]>([]);
  // Add flow state.
  const [adding, setAdding] = useState(false);
  const [addSource, setAddSource] = useState<string>();
  const [addList, setAddList] = useState<string>();
  const [addParamValues, setAddParamValues] = useState<Record<string, string>>({});
  // The library being edited (undefined = list mode) — the working copy lives in LibraryEditView.
  const [editing, setEditing] = useState<RemoteLibrary>();
  // Controlled main tab (libraries / sites) so it's preserved when returning from an edit screen
  // (an uncontrolled Tabs reset to the default "libraries" after editing a site — lost the Sites tab).
  const [mainTab, setMainTab] = useState('libraries');
  // Editing/adding a SITE adapter — hosted here as a dedicated full screen (pinned header + actions),
  // consistent with the library editor. undefined = closed; { initial } open (undefined initial = new).
  // `origin` enables the editor's "compare with default" for a customized source.
  const [editSource, setEditSource] = useState<{ initial?: RemoteSourceConfig; origin?: RemoteSourceInfo['origin'] }>();
  const [removeTarget, setRemoveTarget] = useState<RemoteLibrary>();

  const reload = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      const [list, state, cats] = await Promise.all([
        api.remote.getSources(selectedProfileId),
        api.remote.libraryGetState(selectedProfileId),
        api.category.getCategoryTree(selectedProfileId).catch(() => [] as CategoryInfo[]),
      ]);
      setSources(list);
      setLibState(state);
      setCategories(cats);
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const notifyChanged = useCallback(async () => {
    await reload();
    onChanged?.();
  }, [reload, onChanged]);

  const handleAdd = async () => {
    if (!selectedProfileId || !addSource || !addList) return;
    try {
      const result = await api.remote.libraryAdd(selectedProfileId, addSource, addList, undefined, undefined, true, addParamValues);
      notification.info(t('remote.libraryAdded', { name: result.library.name }));
      setAdding(false);
      setAddSource(undefined);
      setAddList(undefined);
      setAddParamValues({});
      await notifyChanged();
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleSetActive = async (library: RemoteLibrary) => {
    if (!selectedProfileId || library.id === libState?.activeLibraryId) return;
    try {
      setLibState(await api.remote.librarySetActive(selectedProfileId, library.id));
      onChanged?.();
    } catch (error: unknown) {
      handleError(error);
    }
  };

  /** Full reindex (re-crawl every page + prune removed entries) — the heavy sync lives HERE, not
   * in the browse toolbar; the toolbar only offers the cheap incremental update. */
  const fullReindex = async (library: RemoteLibrary) => {
    if (!selectedProfileId) return;
    try {
      const ack = await api.remote.indexSync(selectedProfileId, library.sourceId, library.listId, true);
      notification.info(t(ack.started ? 'remote.syncStarted' : 'remote.syncRunningOrDone'));
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleRemove = async () => {
    if (!selectedProfileId || !removeTarget) return;
    try {
      await api.remote.libraryRemove(selectedProfileId, removeTarget.id);
      setRemoveTarget(undefined);
      await notifyChanged();
    } catch (error: unknown) {
      handleError(error);
    }
  };

  if (!libState) {
    return (
      <div className="remote-lib-mgmt__loading">
        <Spin />
      </div>
    );
  }

  const addSourceInfo = sources.find((s) => s.id === addSource);

  // ---- EDIT A LIBRARY: dedicated full screen (keyed by id so switching libraries remounts fresh) ----
  if (editing) {
    return (
      <LibraryEditView
        key={editing.id}
        library={editing}
        sources={sources}
        categories={categories}
        onSaved={() => {
          setEditing(undefined);
          void notifyChanged();
        }}
        onCancel={() => setEditing(undefined)}
      />
    );
  }

  // ---- EDIT/ADD A SITE: dedicated full screen (pinned header + the editor's own pinned actions),
  // consistent with the library editor — no tab bar / hint clutter, actions always reachable. --------
  if (editSource) {
    return (
      <div className="remote-lib-mgmt remote-lib-mgmt--fill">
        <div className="remote-lib-mgmt__edit-header">
          <CompactIconButton icon={<ArrowLeftOutlined />} title={t('common.cancel')} onClick={() => setEditSource(undefined)} />
          <span className="remote-lib-mgmt__edit-title">
            {editSource.initial ? t('remote.editSource') : t('remote.addSource')}
          </span>
        </div>
        <RemoteSourceEditor
          initial={editSource.initial}
          origin={editSource.origin}
          onCancel={() => setEditSource(undefined)}
          onSaved={() => {
            setEditSource(undefined);
            void notifyChanged();
          }}
        />
      </div>
    );
  }

  // ---- TABS: libraries (primary) / sites (rare) ------------------------------------------------
  const librariesTab = (
    <div className="remote-lib-mgmt__tab">
      {libState.libraries.length === 0 && (
        <div className="remote-lib-mgmt__empty">{t('remote.noLibraries')}</div>
      )}
      {libState.libraries.map((library) => {
        const src = sources.find((s) => s.id === library.sourceId);
        const listName = src?.lists.find((l) => l.id === library.listId)?.name ?? library.listId;
        const isActive = library.id === libState.activeLibraryId;
        // Detail line: source · game (+ rules). Skip when it just repeats the default name.
        const detailBase = `${src?.name ?? library.sourceId} · ${listName}`;
        const rulesSuffix = library.tagRules.length > 0 ? ` · ${t('remote.rulesCount', { count: library.tagRules.length })}` : '';
        const detail = detailBase === library.name && !rulesSuffix ? undefined : `${detailBase === library.name ? '' : detailBase}${rulesSuffix}`.replace(/^ · /, '');
        return (
          <div
            key={library.id}
            className={isActive ? 'remote-lib-mgmt__row remote-lib-mgmt__row--active' : 'remote-lib-mgmt__row'}
            title={isActive ? undefined : t('remote.clickToActivate')}
            onClick={() => void handleSetActive(library)}
          >
            <div className="remote-lib-mgmt__row-main">
              <span className="remote-lib-mgmt__row-name">{library.name}</span>
              {detail && <span className="remote-lib-mgmt__row-detail">{detail}</span>}
            </div>
            {isActive && <StatusTag tone="success" label={t('remote.activeLibrary')} />}
            <CompactIconButton
              icon={<ReloadOutlined />}
              title={t('remote.fullReindexHint')}
              onClick={(e) => {
                e.stopPropagation();
                void fullReindex(library);
              }}
            />
            <CompactIconButton
              icon={<EditOutlined />}
              title={t('common.edit')}
              onClick={(e) => {
                e.stopPropagation();
                setEditing(library);
              }}
            />
            <CompactIconButton
              tone="danger"
              icon={<DeleteOutlined />}
              title={t('common.remove')}
              onClick={(e) => {
                e.stopPropagation();
                setRemoveTarget(library);
              }}
            />
          </div>
        );
      })}

      {!adding && (
        <CompactButton icon={<PlusOutlined />} onClick={() => setAdding(true)}>
          {t('remote.addLibrary')}
        </CompactButton>
      )}
      {adding && (
        <div className="remote-lib-mgmt__add">
          <CompactSelect
            className="remote-lib-mgmt__add-source"
            value={addSource}
            placeholder={t('remote.setupPickSource')}
            options={sources.map((s) => ({ value: s.id, label: s.name }))}
            onChange={(v) => {
              setAddSource(v);
              const src = sources.find((s) => s.id === v);
              setAddList(src?.lists[0]?.id);
              // Seed param inputs with the source's declared defaults.
              setAddParamValues(Object.fromEntries((src?.params ?? [])
                .filter((p) => p.default != null)
                .map((p) => [p.key, p.default as string])));
            }}
          />
          <CompactSelect
            className="remote-lib-mgmt__add-list"
            value={addList}
            placeholder={t('remote.setupPickGame')}
            options={(addSourceInfo?.lists ?? []).map((l) => ({ value: l.id, label: l.name }))}
            onChange={setAddList}
          />
          {/* Source-declared input params (baseUrl override, api key, a select, …) — the library's values. */}
          {(addSourceInfo?.params ?? []).map((p) => (
            <ParamField
              key={p.key}
              param={p}
              value={addParamValues[p.key] ?? ''}
              onChange={(val) => setAddParamValues((pv) => ({ ...pv, [p.key]: val }))}
            />
          ))}
          <CompactButton type="primary" disabled={!addSource || !addList} onClick={() => void handleAdd()}>
            {t('remote.addAndSync')}
          </CompactButton>
          <CompactButton onClick={() => setAdding(false)}>{t('common.cancel')}</CompactButton>
        </div>
      )}
    </div>
  );

  const sitesTab = (
    <div className="remote-lib-mgmt__tab">
      <div className="remote-lib-mgmt__sites-hint">{t('remote.sitesHint')}</div>
      <RemoteSourceManagerScreen
        onChanged={() => void notifyChanged()}
        onEdit={(cfg, origin) => setEditSource({ initial: cfg, origin })}
      />
    </div>
  );

  return (
    <div className="remote-lib-mgmt remote-lib-mgmt--fill">
      <Tabs
        className="remote-lib-mgmt__tabs"
        activeKey={mainTab}
        onChange={setMainTab}
        items={[
          { key: 'libraries', label: t('remote.tabLibraries'), children: librariesTab },
          { key: 'sites', label: t('remote.tabSites'), children: sitesTab },
        ]}
      />

      <ConfirmDialog
        visible={!!removeTarget}
        title={t('remote.removeLibrary')}
        okType="danger"
        content={removeTarget ? t('remote.removeLibraryConfirm', { name: removeTarget.name }) : null}
        onOk={handleRemove}
        onCancel={() => setRemoveTarget(undefined)}
      />
    </div>
  );
};
