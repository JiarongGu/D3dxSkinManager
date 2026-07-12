import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Spin, Tabs } from 'antd';
import {
  ArrowDownOutlined,
  ArrowLeftOutlined,
  ArrowUpOutlined,
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
  RemoteSourceConfig,
  RemoteSourceInfo,
  RemoteTagRule,
} from '../../../shared/types/remote.types';
import { RemoteSourceManagerScreen } from './RemoteSourceManagerScreen';
import { RemoteSourceEditor } from './RemoteSourceEditor';
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
  const { t, i18n } = useTranslation();
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
  // Which edit tab is active — drives the context-sensitive Add button in the pinned footer.
  const [editTab, setEditTab] = useState('rules');
  // Controlled main tab (libraries / sites) so it's preserved when returning from an edit screen
  // (an uncontrolled Tabs reset to the default "libraries" after editing a site — lost the Sites tab).
  const [mainTab, setMainTab] = useState('libraries');
  // Editing/adding a SITE adapter — hosted here as a dedicated full screen (pinned header + actions),
  // consistent with the library editor. undefined = closed; { initial } open (undefined initial = new).
  const [editSource, setEditSource] = useState<{ initial?: RemoteSourceConfig }>();
  // Tag ALIASES for the current app language (raw tag → display label; searchable). PER-PROFILE now
  // (were on the global source config, which leaked across profiles) — edited here like rules.
  const [editingAliases, setEditingAliases] = useState<{ tag: string; label: string }[]>([]);
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
    setEditingAliases([]);
    if (selectedProfileId) {
      // Seed the rule tag-pickers with the tags actually present in this library's index.
      void api.remote
        .indexTags(selectedProfileId, library.sourceId, library.listId)
        .then((tags) => setTagOptions(tags.map((x) => x.name)))
        .catch(() => setTagOptions([]));
      // Load THIS PROFILE's tag aliases for the current app language (edited like rules).
      void api.remote
        .labelsGet(selectedProfileId, library.sourceId)
        .then((labels) => {
          const langMap = labels?.[i18n.language] ?? {};
          setEditingAliases(Object.entries(langMap).map(([tag, label]) => ({ tag, label })));
        })
        .catch(handleError);
    }
  };

  const handleSaveEdit = async () => {
    if (!selectedProfileId || !editing) return;
    try {
      await api.remote.libraryUpdate(selectedProfileId, editing);
      // Persist the tag aliases PER-PROFILE (this language's table only) — no longer on the global source.
      const labels = Object.fromEntries(
        editingAliases.filter((a) => a.tag.trim() && a.label.trim()).map((a) => [a.tag.trim(), a.label.trim()]),
      );
      await api.remote.labelsSet(selectedProfileId, editing.sourceId, i18n.language, labels);
      setEditing(undefined);
      await notifyChanged();
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

  // ---- tag-rule editing (ordered; first match wins) ----------------------------------------
  const setRule = (i: number, patch: Partial<RemoteTagRule>) =>
    setEditing((e) => e && { ...e, tagRules: e.tagRules.map((r, idx) => (idx === i ? { ...r, ...patch } : r)) });
  const addRule = () => {
    pendingFocus.current = true;
    setEditing((e) => e && { ...e, tagRules: [...e.tagRules, { name: '', tags: [], categoryId: '' }] });
  };
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
  const addAlias = () => {
    pendingFocus.current = true;
    setEditingAliases((a) => [...a, { tag: '', label: '' }]);
  };

  // Adding a row from the pinned footer must bring the NEW row into view + focus it (the list
  // scrolls; a row appended off-screen looked like nothing happened). The ref rides the LAST row;
  // the flag limits the effect to add (a delete also changes length but must not steal focus).
  const lastAddedRowRef = useRef<HTMLDivElement | null>(null);
  const pendingFocus = useRef(false);
  useEffect(() => {
    if (!pendingFocus.current) return;
    pendingFocus.current = false;
    const row = lastAddedRowRef.current;
    row?.scrollIntoView({ block: 'nearest' });
    row?.querySelector<HTMLInputElement>('input')?.focus();
  }, [editing?.tagRules.length, editingAliases.length]);

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
    // Show a tag's ALIAS (label) in the rule tag-picker so a rule built against a labeled tag reads
    // naturally — the stored/matched value stays the RAW tag (identity). Reflects unsaved alias edits.
    const aliasLabel = (tag: string) =>
      editingAliases.find((a) => a.tag === tag && a.label.trim())?.label.trim() ?? tag;
    return (
      <div className="remote-lib-mgmt remote-lib-mgmt--fill">
        <div className="remote-lib-mgmt__edit-header">
          <CompactIconButton icon={<ArrowLeftOutlined />} title={t('common.cancel')} onClick={() => setEditing(undefined)} />
          <span className="remote-lib-mgmt__edit-title">{t('remote.editLibrary', { name: editing.name })}</span>
        </div>

        {/* Name lives in the tab bar (left extra) so it shares the row with the tabs — saves a whole
            field row. RULES and TAG NAMES are separate concerns → separate tabs; the tab NAV stays
            pinned (only the content scrolls) and the Add button lives in the pinned footer. */}
        <Tabs
          className="remote-lib-mgmt__tabs"
          activeKey={editTab}
          onChange={setEditTab}
          tabBarExtraContent={{
            left: (
              <div className="remote-lib-mgmt__name-inline">
                <span className="remote-lib-mgmt__name-inline-label">{t('remote.fieldName')}</span>
                <CompactInput
                  className="remote-lib-mgmt__name-input"
                  value={editing.name}
                  onChange={(e) => setEditing((l) => l && { ...l, name: e.target.value })}
                />
              </div>
            ),
          }}
          items={[
              {
                key: 'rules',
                label: t('remote.tagRules'),
                children: (
                  <div className="remote-lib-mgmt__tab">
                    <span className="remote-lib-mgmt__rules-hint">
                      {t('remote.tagRulesHint')} {t('remote.tagRulesDefault')}
                    </span>
                    {editing.tagRules.length === 0 && (
                      <div className="remote-lib-mgmt__rules-empty">{t('remote.noRules')}</div>
                    )}
                    {editing.tagRules.map((rule, i) => (
                      <div
                        key={i}
                        className="remote-lib-mgmt__rule"
                        ref={i === editing.tagRules.length - 1 ? lastAddedRowRef : undefined}
                      >
                        <span className="remote-lib-mgmt__rule-order">{i + 1}</span>
                        <CompactInput
                          className="remote-lib-mgmt__rule-name"
                          value={rule.name}
                          placeholder={t('remote.ruleName')}
                          onChange={(e) => setRule(i, { name: e.target.value })}
                        />
                        <CompactSelect
                          className="remote-lib-mgmt__rule-tags"
                          mode="tags"
                          value={rule.tags}
                          placeholder={t('remote.ruleTags')}
                          options={tagOptions.map((tag) => ({ value: tag, label: aliasLabel(tag) }))}
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
                ),
              },
              {
                key: 'aliases',
                label: t('remote.tagAliases'),
                children: (
                  <div className="remote-lib-mgmt__tab">
                    {/* Display names that are ALSO searchable; they live on the SOURCE config
                        (shared vocabulary for every library of this site). */}
                    <span className="remote-lib-mgmt__rules-hint">{t('remote.tagAliasesHint')}</span>
                    {editingAliases.map((alias, i) => (
                      <div
                        key={i}
                        className="remote-lib-mgmt__rule"
                        ref={i === editingAliases.length - 1 ? lastAddedRowRef : undefined}
                      >
                        <span className="remote-lib-mgmt__rule-order">{i + 1}</span>
                        <CompactSelect
                          className="remote-lib-mgmt__alias-tag"
                          mode="tags"
                          maxCount={1}
                          value={alias.tag ? [alias.tag] : []}
                          placeholder={t('remote.aliasRawTag')}
                          options={tagOptions.map((tag) => ({ value: tag, label: tag }))}
                          onChange={(v) =>
                            setEditingAliases((a) => a.map((x, idx) => (idx === i ? { ...x, tag: v[v.length - 1] ?? '' } : x)))
                          }
                        />
                        <CompactInput
                          className="remote-lib-mgmt__alias-label"
                          value={alias.label}
                          placeholder={t('remote.aliasLabel')}
                          onChange={(e) =>
                            setEditingAliases((a) => a.map((x, idx) => (idx === i ? { ...x, label: e.target.value } : x)))
                          }
                        />
                        <CompactIconButton
                          tone="danger"
                          icon={<DeleteOutlined />}
                          title={t('common.remove')}
                          onClick={() => setEditingAliases((a) => a.filter((_, idx) => idx !== i))}
                        />
                      </div>
                    ))}
                  </div>
                ),
              },
            ]}
        />

        {/* Pinned footer: the context-sensitive Add on the LEFT stays reachable no matter how far the
            list is scrolled (was at the top of the list and scrolled away); cancel/save on the right. */}
        <div className="remote-lib-mgmt__actions">
          <CompactButton
            icon={<PlusOutlined />}
            onClick={editTab === 'aliases' ? addAlias : addRule}
          >
            {editTab === 'aliases' ? t('remote.addAlias') : t('remote.addRule')}
          </CompactButton>
          <div className="remote-lib-mgmt__actions-right">
            <CompactButton onClick={() => setEditing(undefined)}>{t('common.cancel')}</CompactButton>
            <CompactButton type="primary" onClick={() => void handleSaveEdit()}>
              {t('common.save')}
            </CompactButton>
          </div>
        </div>
      </div>
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
      <RemoteSourceManagerScreen
        onChanged={() => void notifyChanged()}
        onEdit={(cfg) => setEditSource({ initial: cfg })}
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
