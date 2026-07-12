import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pagination, Spin, Tabs, Tooltip } from 'antd';
import {
  ArrowDownOutlined,
  ArrowLeftOutlined,
  ArrowUpOutlined,
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
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
import { flattenCategoryOptions } from '../../../shared/utils/categoryTree';
import type { CategoryInfo } from '../../../shared/types/category.types';
import type {
  RemoteLibrariesState,
  RemoteLibrary,
  RemoteSourceConfig,
  RemoteSourceInfo,
  RemoteSourceParam,
  RemoteTagRule,
} from '../../../shared/types/remote.types';
import { RemoteSourceManagerScreen } from './RemoteSourceManagerScreen';
import { RemoteSourceEditor } from './RemoteSourceEditor';
import './RemoteLibraryManagementScreen.css';

interface RemoteLibraryManagementScreenProps {
  /** Called after any library change so the main view can reload. */
  onChanged?: () => void;
}

/** Rules/aliases can grow to hundreds — paginate so only a page of editable rows (each an antd Select)
 *  mounts at a time; the search filter narrows first, then this pages the result. */
const PAGE_SIZE = 15;

/** Order-independent JSON equality → dirty-tracking for the library editor's Save button. */
const canon = (o: unknown): string =>
  JSON.stringify(o, (_k, v) =>
    v && typeof v === 'object' && !Array.isArray(v)
      ? Object.keys(v as object).sort().reduce((acc, k) => {
          (acc as Record<string, unknown>)[k] = (v as Record<string, unknown>)[k];
          return acc;
        }, {} as Record<string, unknown>)
      : v,
  );

/** One library-configurable source param, rendered as a text input or a select (remote-library-redesign.md).
 *  The value goes into the library's paramValues and substitutes for {param.<key>} in the effective config. */
const ParamField: React.FC<{ param: RemoteSourceParam; value: string; onChange: (v: string) => void }> = ({ param, value, onChange }) => (
  <div className="remote-lib-mgmt__param-field">
    <span className="remote-lib-mgmt__param-label">{param.label || param.key}{param.required ? ' *' : ''}</span>
    {param.type === 'select' ? (
      <CompactSelect
        className="remote-lib-mgmt__param-input"
        value={value || undefined}
        placeholder={param.label || param.key}
        options={param.options.map((o) => ({ value: o.value, label: o.label || o.value }))}
        onChange={(v) => onChange((v as string) ?? '')}
      />
    ) : (
      <CompactInput
        className="remote-lib-mgmt__param-input"
        value={value}
        placeholder={param.default ?? param.label ?? param.key}
        onChange={(e) => onChange(e.target.value)}
      />
    )}
  </div>
);

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
  // Which edit tab is active (detail / rules / aliases) — drives the context-sensitive footer Add.
  const [editTab, setEditTab] = useState('detail');
  // Controlled main tab (libraries / sites) so it's preserved when returning from an edit screen
  // (an uncontrolled Tabs reset to the default "libraries" after editing a site — lost the Sites tab).
  const [mainTab, setMainTab] = useState('libraries');
  // Editing/adding a SITE adapter — hosted here as a dedicated full screen (pinned header + actions),
  // consistent with the library editor. undefined = closed; { initial } open (undefined initial = new).
  // `origin` enables the editor's "compare with default" for a customized source.
  const [editSource, setEditSource] = useState<{ initial?: RemoteSourceConfig; origin?: RemoteSourceInfo['origin'] }>();
  // Tag ALIASES for the current app language (raw tag → display label; searchable). PER-PROFILE now
  // (were on the global source config, which leaked across profiles) — edited here like rules.
  const [editingAliases, setEditingAliases] = useState<{ tag: string; label: string }[]>([]);
  // Baselines captured on open → dirty-tracking so Save is disabled when nothing changed (user ask).
  const [editLibBaseline, setEditLibBaseline] = useState<RemoteLibrary>();
  const [editAliasBaseline, setEditAliasBaseline] = useState<{ tag: string; label: string }[]>([]);
  const [removeTarget, setRemoveTarget] = useState<RemoteLibrary>();
  // Values for the picked source's declared params, collected in the ADD flow (seeded from defaults).
  const [addParamValues, setAddParamValues] = useState<Record<string, string>>({});
  // Filters for the rules / tag-label lists — they can grow to hundreds; a search keeps them usable.
  const [ruleFilter, setRuleFilter] = useState('');
  const [aliasFilter, setAliasFilter] = useState('');
  // Current page per list (1-based; clamped to the max page in render).
  const [rulePage, setRulePage] = useState(1);
  const [aliasPage, setAliasPage] = useState(1);

  // id → breadcrumb label, for filtering rules by their target category name.
  const catLabelById = useMemo(
    () => Object.fromEntries(flattenCategoryOptions(categories).map((o) => [o.value, o.label])),
    [categories],
  );

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

  const startEdit = async (library: RemoteLibrary) => {
    const working = { ...library, tagRules: library.tagRules.map((r) => ({ ...r, tags: [...r.tags] })) };
    setEditing(working);
    setEditLibBaseline(working); // canon() compares by value, so the shared ref is fine as a baseline
    setEditingAliases([]);
    setEditAliasBaseline([]);
    setRuleFilter('');
    setAliasFilter('');
    setRulePage(1);
    setAliasPage(1);
    setEditTab('detail');
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
          const loaded = Object.entries(langMap).map(([tag, label]) => ({ tag, label }));
          setEditingAliases(loaded);
          setEditAliasBaseline(loaded); // baseline for dirty-tracking (aliases load async)
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
    setRuleFilter(''); // a new blank row won't match an active filter — clear it so the row is visible
    setRulePage(Number.MAX_SAFE_INTEGER); // jump to the last page (clamped) so the new row shows
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
    setAliasFilter('');
    setAliasPage(Number.MAX_SAFE_INTEGER); // jump to the last page (clamped) so the new row shows
    pendingFocus.current = true;
    setEditingAliases((a) => [...a, { tag: '', label: '' }]);
  };
  // A library's value for one of its source's declared params (substituted into the effective config).
  const setEditParam = (key: string, val: string) =>
    setEditing((e) => e && { ...e, paramValues: { ...(e.paramValues ?? {}), [key]: val } });

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
    // The params this library's source declares — rendered as an editable "Params" tab.
    const editParams = sources.find((s) => s.id === editing.sourceId)?.params ?? [];
    // Dirty-tracking → Save disabled until name/source/game/rules/params/aliases actually change.
    const dirty = !!editLibBaseline && (canon(editing) !== canon(editLibBaseline) || canon(editingAliases) !== canon(editAliasBaseline));

    // ---- filtering (rules + aliases can grow to hundreds — a search keeps them navigable) ----
    const FILTER_AT = 6; // only surface the search box once a list is big enough to need it
    const rf = ruleFilter.trim().toLowerCase();
    const ruleMatch = (r: RemoteTagRule) =>
      !rf || [r.name, r.titlePattern ?? '', catLabelById[r.categoryId] ?? '', ...r.tags, ...r.tags.map(aliasLabel)]
        .some((s) => s.toLowerCase().includes(rf));
    const af = aliasFilter.trim().toLowerCase();
    const aliasMatch = (a: { tag: string; label: string }) =>
      !af || a.tag.toLowerCase().includes(af) || a.label.toLowerCase().includes(af);
    // Filter → then paginate. Keep each row's REAL index so edit/reorder/delete target the right entry.
    const filteredRules = editing.tagRules.map((r, i) => ({ r, i })).filter((x) => ruleMatch(x.r));
    const filteredAliases = editingAliases.map((a, i) => ({ a, i })).filter((x) => aliasMatch(x.a));
    const shownRules = filteredRules.length;
    const shownAliases = filteredAliases.length;
    const ruleMax = Math.max(1, Math.ceil(filteredRules.length / PAGE_SIZE));
    const rulePageSafe = Math.min(rulePage, ruleMax);
    const pagedRules = filteredRules.slice((rulePageSafe - 1) * PAGE_SIZE, rulePageSafe * PAGE_SIZE);
    const aliasMax = Math.max(1, Math.ceil(filteredAliases.length / PAGE_SIZE));
    const aliasPageSafe = Math.min(aliasPage, aliasMax);
    const pagedAliases = filteredAliases.slice((aliasPageSafe - 1) * PAGE_SIZE, aliasPageSafe * PAGE_SIZE);
    // One label per tag: a tag already used by ANOTHER row is excluded from a row's picker.
    const usedAliasTags = new Set(editingAliases.map((a) => a.tag).filter(Boolean));
    return (
      <div className="remote-lib-mgmt remote-lib-mgmt--fill">
        <div className="remote-lib-mgmt__edit-header">
          <CompactIconButton icon={<ArrowLeftOutlined />} title={t('common.cancel')} onClick={() => setEditing(undefined)} />
          <span className="remote-lib-mgmt__edit-title">{t('remote.editLibrary', { name: editing.name })}</span>
        </div>

        {/* Three tabs — Detail (name/source/game/params) · Input rules · Tag labels. The verbose
            explanations live on each tab's tooltip (hover) so the page stays clean. */}
        <Tabs
          className="remote-lib-mgmt__tabs"
          activeKey={editTab}
          onChange={setEditTab}
          items={[
              {
                key: 'detail',
                label: t('remote.tabDetail'),
                children: (
                  <div className="remote-lib-mgmt__tab">
                    <div className="remote-lib-mgmt__edit-fields">
                      <CompactField label={t('remote.fieldName')}>
                        <CompactInput
                          value={editing.name}
                          onChange={(e) => setEditing((l) => l && { ...l, name: e.target.value })}
                        />
                      </CompactField>
                      <div className="remote-lib-mgmt__edit-fields-row">
                        <CompactField label={t('remote.fieldSource')} description={t('remote.switchSourceHint')}>
                          <CompactSelect
                            value={editing.sourceId}
                            options={sources.map((s) => ({ value: s.id, label: s.name }))}
                            onChange={(v) =>
                              setEditing((e) => e && {
                                ...e,
                                sourceId: v as string,
                                listId: sources.find((s) => s.id === v)?.lists[0]?.id ?? e.listId,
                              })
                            }
                            style={{ width: '100%' }}
                          />
                        </CompactField>
                        <CompactField label={t('remote.fieldGame')}>
                          <CompactSelect
                            value={editing.listId}
                            options={(sources.find((s) => s.id === editing.sourceId)?.lists ?? []).map((l) => ({ value: l.id, label: l.name }))}
                            onChange={(v) => setEditing((e) => e && { ...e, listId: v as string })}
                            style={{ width: '100%' }}
                          />
                        </CompactField>
                      </div>
                    </div>
                    {editParams.map((p) => (
                      <ParamField
                        key={p.key}
                        param={p}
                        value={editing.paramValues?.[p.key] ?? ''}
                        onChange={(val) => setEditParam(p.key, val)}
                      />
                    ))}
                  </div>
                ),
              },
              {
                key: 'rules',
                label: <Tooltip title={`${t('remote.tagRulesHint')} ${t('remote.tagRulesDefault')}`}>{t('remote.tabInputRule')}</Tooltip>,
                children: (
                  <div className="remote-lib-mgmt__tab">
                    {editing.tagRules.length === 0 && (
                      <div className="remote-lib-mgmt__rules-empty">{t('remote.noRules')}</div>
                    )}
                    {/* Search once the list is big; reorder is disabled while filtered (order is relative). */}
                    {editing.tagRules.length > FILTER_AT && (
                      <div className="remote-lib-mgmt__filter">
                        <CompactInput
                          prefix={<SearchOutlined />}
                          allowClear
                          value={ruleFilter}
                          placeholder={t('remote.filterRules')}
                          onChange={(e) => { setRuleFilter(e.target.value); setRulePage(1); }}
                        />
                        <span className="remote-lib-mgmt__filter-count">
                          {t('remote.filterCount', { shown: shownRules, total: editing.tagRules.length })}
                        </span>
                      </div>
                    )}
                    {pagedRules.map(({ r: rule, i }) => (
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
                        {/* Reorder is relative to neighbours → disabled while filtered (rows are hidden). */}
                        <CompactIconButton icon={<ArrowUpOutlined />} title={rf ? t('remote.reorderClearFilter') : t('common.moveUp')} disabled={!!rf} onClick={() => moveRule(i, -1)} />
                        <CompactIconButton icon={<ArrowDownOutlined />} title={rf ? t('remote.reorderClearFilter') : t('common.moveDown')} disabled={!!rf} onClick={() => moveRule(i, 1)} />
                        <CompactIconButton tone="danger" icon={<DeleteOutlined />} title={t('common.remove')} onClick={() => removeRule(i)} />
                      </div>
                    ))}
                    {filteredRules.length > PAGE_SIZE && (
                      <Pagination
                        className="remote-lib-mgmt__pager"
                        size="small"
                        current={rulePageSafe}
                        pageSize={PAGE_SIZE}
                        total={filteredRules.length}
                        showSizeChanger={false}
                        onChange={setRulePage}
                      />
                    )}
                  </div>
                ),
              },
              {
                key: 'aliases',
                label: <Tooltip title={t('remote.tagAliasesHint')}>{t('remote.tabTagLabel')}</Tooltip>,
                children: (
                  <div className="remote-lib-mgmt__tab">
                    {editingAliases.length > FILTER_AT && (
                      <div className="remote-lib-mgmt__filter">
                        <CompactInput
                          prefix={<SearchOutlined />}
                          allowClear
                          value={aliasFilter}
                          placeholder={t('remote.filterAliases')}
                          onChange={(e) => { setAliasFilter(e.target.value); setAliasPage(1); }}
                        />
                        <span className="remote-lib-mgmt__filter-count">
                          {t('remote.filterCount', { shown: shownAliases, total: editingAliases.length })}
                        </span>
                      </div>
                    )}
                    {/* One row per tag → one label (translation). The tag is a SEARCHABLE single-select;
                        tags already used by another row are excluded so a tag can't be labeled twice. */}
                    <div className="remote-lib-mgmt__alias-grid">
                      {pagedAliases.map(({ a: alias, i }) => (
                        <div
                          key={i}
                          className="remote-lib-mgmt__alias-row"
                          ref={i === editingAliases.length - 1 ? lastAddedRowRef : undefined}
                        >
                          <CompactSelect
                            className="remote-lib-mgmt__alias-tag"
                            showSearch
                            optionFilterProp="label"
                            value={alias.tag || undefined}
                            placeholder={t('remote.aliasRawTag')}
                            options={tagOptions
                              .filter((tag) => tag === alias.tag || !usedAliasTags.has(tag))
                              .map((tag) => ({ value: tag, label: tag }))}
                            onChange={(v) =>
                              setEditingAliases((a) => a.map((x, idx) => (idx === i ? { ...x, tag: (v as string) ?? '' } : x)))
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
                    {filteredAliases.length > PAGE_SIZE && (
                      <Pagination
                        className="remote-lib-mgmt__pager"
                        size="small"
                        current={aliasPageSafe}
                        pageSize={PAGE_SIZE}
                        total={filteredAliases.length}
                        showSizeChanger={false}
                        onChange={setAliasPage}
                      />
                    )}
                  </div>
                ),
              },
            ]}
        />

        {/* Pinned footer: the context-sensitive Add on the LEFT stays reachable no matter how far the
            list is scrolled (was at the top of the list and scrolled away); cancel/save on the right. */}
        <div className="remote-lib-mgmt__actions">
          {/* Add applies only to the two list tabs (Detail has no list). */}
          {(editTab === 'rules' || editTab === 'aliases') && (
            <CompactButton
              icon={<PlusOutlined />}
              onClick={editTab === 'aliases' ? addAlias : addRule}
            >
              {editTab === 'aliases' ? t('remote.addAlias') : t('remote.addRule')}
            </CompactButton>
          )}
          <div className="remote-lib-mgmt__actions-right">
            <CompactButton onClick={() => setEditing(undefined)}>{t('common.cancel')}</CompactButton>
            <CompactButton type="primary" disabled={!dirty} onClick={() => void handleSaveEdit()}>
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
