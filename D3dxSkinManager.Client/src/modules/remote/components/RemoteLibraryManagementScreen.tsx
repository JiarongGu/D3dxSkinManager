import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Select, Spin } from 'antd';
import {
  ArrowDownOutlined,
  ArrowUpOutlined,
  CheckCircleFilled,
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
  CompactDivider,
  CompactField,
  CompactIconButton,
  CompactInput,
  CompactSection,
  CompactSelect,
} from '../../../shared/components/compact';
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
 * Library management (remote-library-redesign.md): the profile's configured libraries — add
 * (site → game → sync), edit (name + ORDERED tag→category import rules), remove, set active.
 * The sites/adapters section (read-only defaults in res/ + advanced custom configs) sits below.
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
  // Edit state — a working copy of the library being edited.
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
    if (!selectedProfileId) return;
    try {
      setLibState(await api.remote.librarySetActive(selectedProfileId, library.id));
      onChanged?.();
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const startEdit = async (library: RemoteLibrary) => {
    // Deep-copy the rules so cancel discards edits; load the library's tag options for the pickers.
    setEditing({ ...library, tagRules: library.tagRules.map((r) => ({ ...r, tags: [...r.tags] })) });
    if (selectedProfileId) {
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

  // ---- EDIT MODE: name + ordered tag rules ---------------------------------------------------
  if (editing) {
    return (
      <div className="remote-lib-mgmt">
        <CompactSection title={t('remote.editLibrary', { name: editing.name })}>
          <CompactField label={t('remote.fieldName')}>
            <CompactInput value={editing.name} onChange={(e) => setEditing((l) => l && { ...l, name: e.target.value })} />
          </CompactField>
        </CompactSection>

        <CompactSection title={t('remote.tagRules')}>
          <div className="remote-lib-mgmt__rules-hint">{t('remote.tagRulesHint')}</div>
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
          <CompactButton size="small" icon={<PlusOutlined />} onClick={addRule}>
            {t('remote.addRule')}
          </CompactButton>
          <div className="remote-lib-mgmt__rules-default">{t('remote.tagRulesDefault')}</div>
        </CompactSection>

        <div className="remote-lib-mgmt__actions">
          <CompactButton onClick={() => setEditing(undefined)}>{t('common.cancel')}</CompactButton>
          <CompactButton type="primary" onClick={() => void handleSaveEdit()}>
            {t('common.save')}
          </CompactButton>
        </div>
      </div>
    );
  }

  // ---- LIST MODE: my libraries + add + sites section ------------------------------------------
  return (
    <div className="remote-lib-mgmt">
      <CompactSection title={t('remote.myLibraries')}>
        {libState.libraries.length === 0 && (
          <div className="remote-lib-mgmt__empty">{t('remote.noLibraries')}</div>
        )}
        {libState.libraries.map((library) => {
          const src = sources.find((s) => s.id === library.sourceId);
          const listName = src?.lists.find((l) => l.id === library.listId)?.name ?? library.listId;
          const isActive = library.id === libState.activeLibraryId;
          return (
            <div key={library.id} className="remote-lib-mgmt__row">
              <div className="remote-lib-mgmt__row-main" onClick={() => void handleSetActive(library)}>
                <span className="remote-lib-mgmt__row-name">
                  {isActive && <CheckCircleFilled className="remote-lib-mgmt__active" />}
                  {library.name}
                </span>
                <span className="remote-lib-mgmt__row-detail">
                  {src?.name ?? library.sourceId} · {listName}
                  {library.tagRules.length > 0 && ` · ${t('remote.rulesCount', { count: library.tagRules.length })}`}
                </span>
              </div>
              <CompactIconButton icon={<EditOutlined />} title={t('common.edit')} onClick={() => void startEdit(library)} />
              <CompactIconButton tone="danger" icon={<DeleteOutlined />} title={t('common.remove')} onClick={() => setRemoveTarget(library)} />
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
      </CompactSection>

      <CompactDivider />

      <CompactSection title={t('remote.sitesSection')}>
        <div className="remote-lib-mgmt__sites-hint">{t('remote.sitesHint')}</div>
        <RemoteSourceManagerScreen onChanged={() => void notifyChanged()} />
      </CompactSection>

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
