import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Select, Spin, Tabs } from 'antd';
import {
  ArrowDownOutlined,
  ArrowLeftOutlined,
  ArrowUpOutlined,
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
} from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import {
  CompactButton,
  CompactField,
  CompactIconButton,
  CompactInput,
  CompactSelect,
} from '../../../shared/components/compact';
import { StatusTag } from '../../../shared/components/common/StatusTag';
import { CategorySelect } from '../../../shared/components/CategorySelect';
import { ConfirmDialog } from '../../../shared/components/dialogs/ConfirmDialog';
import type { CategoryInfo } from '../../../shared/types/category.types';
import type {
  RemoteLibrariesState,
  RemoteLibrary,
  RemoteSourceInfo,
  RemoteTagRule,
} from '../../../shared/types/remote.types';
import { RemoteSourceManagerScreen } from './RemoteSourceManagerScreen';
import './RemoteLibraryManagementScreen.css';

interface RemoteLibraryManagementScreenProps {
  /** Called after any library change so the main view can reload. */
  onChanged?: () => void;
}

/**
 * Library management (remote-library-redesign.md), TWO TABS: "My libraries" (the profile's configured
 * libraries — the everyday tab: add site→game→sync, switch active, edit name + ORDERED tag→category
 * import rules, remove) and "Sites" (the adapter configs — rarely used; shipped ones are read-only
 * defaults in res/, custom sites live here).
 */
export const RemoteLibraryManagementScreen: React.FC<RemoteLibraryManagementScreenProps> = ({ onChanged }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [sources, setSources] = useState<RemoteSourceInfo[]>([]);
  const [libState, setLibState] = useState<RemoteLibrariesState>();
  const [categories, setCategories] = useState<CategoryInfo[]>([]);
  const [tagOptions, setTagOptions] = useState<string[]>([]);
  // Add flow state.
  const [adding, setAdding] = useState(false);
  const [addSource, setAddSource] = useState<string>();
  const [addList, setAddList] = useState<string>();
  // Edit state — a working copy of the library being edited (rules deep-copied so cancel discards).
  const [editing, setEditing] = useState<RemoteLibrary>();
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
      const result = await api.remote.libraryAdd(selectedProfileId, addSource, addList, undefined, undefined, true);
      notification.info(t('remote.libraryAdded', { name: result.library.name }));
      setAdding(false);
      setAddSource(undefined);
      setAddList(undefined);
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

  const startEdit = async (library: RemoteLibrary) => {
    setEditing({ ...library, tagRules: library.tagRules.map((r) => ({ ...r, tags: [...r.tags] })) });
    if (selectedProfileId) {
      // Seed the rule tag-pickers with the tags actually present in this library's index.
      void api.remote
        .indexTags(selectedProfileId, library.sourceId, library.listId)
        .then((tags) => setTagOptions(tags.map((x) => x.name)))
        .catch(() => setTagOptions([]));
    }
  };

  const handleSaveEdit = async () => {
    if (!selectedProfileId || !editing) return;
    try {
      await api.remote.libraryUpdate(selectedProfileId, editing);
      setEditing(undefined);
      await notifyChanged();
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

  // ---- tag-rule editing (ordered; first match wins) ----------------------------------------
  const setRule = (i: number, patch: Partial<RemoteTagRule>) =>
    setEditing((e) => e && { ...e, tagRules: e.tagRules.map((r, idx) => (idx === i ? { ...r, ...patch } : r)) });
  const addRule = () =>
    setEditing((e) => e && { ...e, tagRules: [...e.tagRules, { name: '', tags: [], categoryId: '' }] });
  const removeRule = (i: number) =>
    setEditing((e) => e && { ...e, tagRules: e.tagRules.filter((_, idx) => idx !== i) });
  const moveRule = (i: number, dir: -1 | 1) =>
    setEditing((e) => {
      if (!e) return e;
      const j = i + dir;
      if (j < 0 || j >= e.tagRules.length) return e;
      const rules = [...e.tagRules];
      [rules[i], rules[j]] = [rules[j], rules[i]];
      return { ...e, tagRules: rules };
    });

  if (!libState) {
    return (
      <div className="remote-lib-mgmt__loading">
        <Spin />
      </div>
    );
  }

  const addSourceInfo = sources.find((s) => s.id === addSource);

  // ---- EDIT MODE: pinned header + scrollable body + pinned actions ----------------------------
  if (editing) {
    return (
      <div className="remote-lib-mgmt remote-lib-mgmt--fill">
        <div className="remote-lib-mgmt__edit-header">
          <CompactIconButton icon={<ArrowLeftOutlined />} title={t('common.cancel')} onClick={() => setEditing(undefined)} />
          <span className="remote-lib-mgmt__edit-title">{t('remote.editLibrary', { name: editing.name })}</span>
        </div>

        <div className="remote-lib-mgmt__scroll">
          <CompactField label={t('remote.fieldName')}>
            <CompactInput
              className="remote-lib-mgmt__name-input"
              value={editing.name}
              onChange={(e) => setEditing((l) => l && { ...l, name: e.target.value })}
            />
          </CompactField>

          <div className="remote-lib-mgmt__section-head">
            <span className="remote-lib-mgmt__section-title">{t('remote.tagRules')}</span>
            <CompactButton size="small" icon={<PlusOutlined />} onClick={addRule}>
              {t('remote.addRule')}
            </CompactButton>
          </div>
          <div className="remote-lib-mgmt__rules-hint">
            {t('remote.tagRulesHint')} {t('remote.tagRulesDefault')}
          </div>
          {editing.tagRules.length === 0 && (
            <div className="remote-lib-mgmt__rules-empty">{t('remote.noRules')}</div>
          )}
          {editing.tagRules.map((rule, i) => (
            <div key={i} className="remote-lib-mgmt__rule">
              <span className="remote-lib-mgmt__rule-order">{i + 1}</span>
              <CompactInput
                className="remote-lib-mgmt__rule-name"
                value={rule.name}
                placeholder={t('remote.ruleName')}
                onChange={(e) => setRule(i, { name: e.target.value })}
              />
              <Select
                className="remote-lib-mgmt__rule-tags"
                mode="tags"
                size="middle"
                value={rule.tags}
                placeholder={t('remote.ruleTags')}
                options={tagOptions.map((tag) => ({ value: tag, label: tag }))}
                onChange={(tags) => setRule(i, { tags })}
              />
              <CompactInput
                className="remote-lib-mgmt__rule-pattern"
                value={rule.titlePattern ?? ''}
                placeholder={t('remote.ruleTitlePattern')}
                spellCheck={false}
                onChange={(e) => setRule(i, { titlePattern: e.target.value || undefined })}
              />
              <CategorySelect
                className="remote-lib-mgmt__rule-category"
                categories={categories}
                value={rule.categoryId || undefined}
                placeholder={t('remote.rulePickCategory')}
                onChange={(id) => setRule(i, { categoryId: id ?? '' })}
              />
              <CompactIconButton icon={<ArrowUpOutlined />} title={t('common.moveUp')} onClick={() => moveRule(i, -1)} />
              <CompactIconButton icon={<ArrowDownOutlined />} title={t('common.moveDown')} onClick={() => moveRule(i, 1)} />
              <CompactIconButton tone="danger" icon={<DeleteOutlined />} title={t('common.remove')} onClick={() => removeRule(i)} />
            </div>
          ))}
        </div>

        <div className="remote-lib-mgmt__actions">
          <CompactButton onClick={() => setEditing(undefined)}>{t('common.cancel')}</CompactButton>
          <CompactButton type="primary" onClick={() => void handleSaveEdit()}>
            {t('common.save')}
          </CompactButton>
        </div>
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
              icon={<EditOutlined />}
              title={t('common.edit')}
              onClick={(e) => {
                e.stopPropagation();
                void startEdit(library);
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
              setAddList(sources.find((s) => s.id === v)?.lists[0]?.id);
            }}
          />
          <CompactSelect
            className="remote-lib-mgmt__add-list"
            value={addList}
            placeholder={t('remote.setupPickGame')}
            options={(addSourceInfo?.lists ?? []).map((l) => ({ value: l.id, label: l.name }))}
            onChange={setAddList}
          />
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
      <RemoteSourceManagerScreen onChanged={() => void notifyChanged()} />
    </div>
  );

  return (
    <div className="remote-lib-mgmt remote-lib-mgmt--fill">
      <Tabs
        className="remote-lib-mgmt__tabs"
        defaultActiveKey="libraries"
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
