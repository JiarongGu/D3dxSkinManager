import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pagination, Tabs, Tooltip } from 'antd';
import {
  ArrowDownOutlined,
  ArrowLeftOutlined,
  ArrowUpOutlined,
  CloudSyncOutlined,
  DeleteOutlined,
  PlusOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { api } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { canonicalJson } from '../../../shared/utils/canonicalJson';
import {
  CompactButton,
  CompactCheckbox,
  CompactField,
  CompactIconButton,
  CompactInput,
  CompactSelect,
} from '../../../shared/components/compact';
import { CategorySelect } from '../../../shared/components/CategorySelect';
import { flattenCategoryOptions } from '../../../shared/utils/categoryTree';
import type { CategoryInfo } from '../../../shared/types/category.types';
import type { RemoteLibrary, RemoteSourceInfo, RemoteTagRule } from '../../../shared/types/remote.types';
import { PaginatedEditList, paginateFiltered, LIST_PAGE_SIZE } from './PaginatedEditList';
import { ParamField } from './ParamField';

interface LibraryEditViewProps {
  /** The library to edit — deep-copied into a working copy on mount so cancel discards. Parent keys
   *  this component by `library.id` so switching libraries remounts with a fresh working copy. */
  library: RemoteLibrary;
  sources: RemoteSourceInfo[];
  categories: CategoryInfo[];
  /** Saved successfully (parent closes the editor + reloads). */
  onSaved: () => void;
  /** Cancel / back (parent closes the editor). */
  onCancel: () => void;
}

const deepCopy = (l: RemoteLibrary): RemoteLibrary => ({ ...l, tagRules: l.tagRules.map((r) => ({ ...r, tags: [...r.tags] })) });

/**
 * The library editor — a dedicated `--fill` screen (pinned header + scrollable 3-tab body + pinned
 * footer): Detail (name/source/game/params) · Input rules (ordered tag→category) · Tag labels (aliases).
 * Extracted from RemoteLibraryManagementScreen (remote-library-redesign.md). L3: owns the working-copy
 * state, loads the library's index tags + per-profile aliases on mount, and persists both on Save.
 */
export const LibraryEditView: React.FC<LibraryEditViewProps> = ({ library, sources, categories, onSaved, onCancel }) => {
  const { t, i18n } = useTranslation();
  const { selectedProfileId } = useProfile();

  // Working copy + a frozen baseline (same initial value) for dirty-tracking — canonicalJson compares by
  // value, and edits produce NEW objects (immutable updates), so the baseline stays at the initial state.
  const [editing, setEditing] = useState<RemoteLibrary>(() => deepCopy(library));
  const [editLibBaseline, setEditLibBaseline] = useState<RemoteLibrary>(() => deepCopy(library));
  const [editTab, setEditTab] = useState('detail');
  // Tag ALIASES for the current app language (raw tag → display label; searchable). PER-PROFILE.
  const [editingAliases, setEditingAliases] = useState<{ tag: string; label: string }[]>([]);
  const [editAliasBaseline, setEditAliasBaseline] = useState<{ tag: string; label: string }[]>([]);
  const [tagOptions, setTagOptions] = useState<string[]>([]);
  // Filters + pages for the rules / tag-label lists (can grow to hundreds — PaginatedEditList pages them).
  const [ruleFilter, setRuleFilter] = useState('');
  const [aliasFilter, setAliasFilter] = useState('');
  const [rulePage, setRulePage] = useState(1);
  const [aliasPage, setAliasPage] = useState(1);
  // #22: rules tab — hide tags already assigned to a rule from the tag picker (find unassigned tags).
  const [onlyUnusedRuleTags, setOnlyUnusedRuleTags] = useState(false);
  // Page size auto-fits the visible list height (min 5) so the pager appears only when rows actually
  // overflow — recomputed on resize from the scroll area's height (ROW_H ≈ one row incl. the flex gap).
  const rootRef = useRef<HTMLDivElement>(null);
  const [pageSize, setPageSize] = useState(LIST_PAGE_SIZE);
  useEffect(() => {
    const root = rootRef.current;
    if (!root) return;
    const pane = (root.querySelector('.ant-tabs-tabpane-active') as HTMLElement | null) ?? root;
    const scroll = pane.querySelector('.remote-lib-mgmt__rows-scroll') as HTMLElement | null;
    if (!scroll) return;
    // Size the page to how many rows fit in the ROWS-SCROLL area (the search is a fixed header outside
    // it). Measure a REAL row + its container gap so rule rows (~50px) and alias rows (~40px) each fill
    // their tab exactly. Observe the scroller so the flex-height settle AND window resizes both recompute.
    const recompute = () => {
      const row = scroll.querySelector('.remote-lib-mgmt__rule, .remote-lib-mgmt__alias-row') as HTMLElement | null;
      const gapRaw = row?.parentElement ? parseFloat(getComputedStyle(row.parentElement).rowGap) : NaN;
      const gap = Number.isFinite(gapRaw) ? gapRaw : 8;
      const rowH = (row ? row.getBoundingClientRect().height : 40) + gap;
      setPageSize(Math.max(5, Math.floor(scroll.clientHeight / rowH)));
    };
    recompute();
    const ro = new ResizeObserver(recompute);
    ro.observe(scroll);
    return () => ro.disconnect();
    // Re-measure when the tab changes AND when a list first gains rows (an empty tab has no real row to
    // measure, so rowH would stay at the fallback and mis-size once rows appear).
  }, [editTab, editing.tagRules.length, editingAliases.length]);

  // id → breadcrumb label, for filtering rules by their target category name.
  const catLabelById = useMemo(
    () => Object.fromEntries(flattenCategoryOptions(categories).map((o) => [o.value, o.label])),
    [categories],
  );

  // Load THIS library's index tags (seed the rule tag-pickers) + this profile's aliases for the current
  // language, once when the profile is ready. `library`/`i18n.language` are stable for this mount (parent
  // keys by id), so read them at call time — this is a load-once effect, not a stale-closure omission.
  useEffect(() => {
    if (!selectedProfileId) return;
    void api.remote
      .indexTags(selectedProfileId, library.sourceId, library.listId)
      .then((tags) => setTagOptions(tags.map((x) => x.name)))
      .catch(() => setTagOptions([]));
    void api.remote
      .labelsGet(selectedProfileId, library.sourceId)
      .then((labels) => {
        const langMap = labels?.[i18n.language] ?? {};
        const loaded = Object.entries(langMap).map(([tag, label]) => ({ tag, label }));
        setEditingAliases(loaded);
        setEditAliasBaseline(loaded); // baseline for dirty-tracking (aliases load async)
      })
      .catch(handleError);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId]);

  const handleSaveEdit = async () => {
    if (!selectedProfileId) return;
    try {
      await api.remote.libraryUpdate(selectedProfileId, editing);
      // Persist the tag aliases PER-PROFILE (this language's table only) — no longer on the global source.
      const labels = Object.fromEntries(
        editingAliases.filter((a) => a.tag.trim() && a.label.trim()).map((a) => [a.tag.trim(), a.label.trim()]),
      );
      await api.remote.labelsSet(selectedProfileId, editing.sourceId, i18n.language, labels);
      // #20: saving must NOT close the editor. Reset the dirty baselines (Save disables until the next
      // edit) + toast; the parent now reloads in place instead of closing on onSaved.
      setEditLibBaseline(deepCopy(editing));
      setEditAliasBaseline(editingAliases.map((a) => ({ ...a })));
      notification.info(t('remote.librarySaved', { name: editing.name }));
      onSaved();
    } catch (error: unknown) {
      handleError(error);
    }
  };

  /** Sync THIS library from its Detail tab (library → detail). "Update" = cheap incremental (matches the
   *  browse toolbar); "Full re-sync" = re-crawl every page + prune entries the site no longer lists. */
  const syncLibrary = async (full: boolean) => {
    if (!selectedProfileId) return;
    try {
      const ack = await api.remote.indexSync(selectedProfileId, editing.sourceId, editing.listId, full);
      notification.info(t(ack.started ? 'remote.syncStarted' : 'remote.syncRunningOrDone'));
    } catch (error: unknown) {
      handleError(error);
    }
  };

  // ---- tag-rule editing (ordered; first match wins) ----------------------------------------
  const setRule = (i: number, patch: Partial<RemoteTagRule>) =>
    setEditing((e) => ({ ...e, tagRules: e.tagRules.map((r, idx) => (idx === i ? { ...r, ...patch } : r)) }));
  const addRule = () => {
    setRuleFilter(''); // a new blank row won't match an active filter — clear it so the row is visible
    setRulePage(Number.MAX_SAFE_INTEGER); // jump to the last page (clamped) so the new row shows
    pendingFocus.current = true;
    setEditing((e) => ({ ...e, tagRules: [...e.tagRules, { name: '', tags: [], categoryId: '' }] }));
  };
  const removeRule = (i: number) =>
    setEditing((e) => ({ ...e, tagRules: e.tagRules.filter((_, idx) => idx !== i) }));
  const moveRule = (i: number, dir: -1 | 1) =>
    setEditing((e) => {
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
    setEditing((e) => ({ ...e, paramValues: { ...(e.paramValues ?? {}), [key]: val } }));

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
  }, [editing.tagRules.length, editingAliases.length]);

  // Show a tag's ALIAS (label) in the rule tag-picker so a rule built against a labeled tag reads
  // naturally — the stored/matched value stays the RAW tag (identity). Reflects unsaved alias edits.
  const aliasLabel = (tag: string) =>
    editingAliases.find((a) => a.tag === tag && a.label.trim())?.label.trim() ?? tag;
  // The params this library's source declares — rendered on the Detail tab.
  const editParams = sources.find((s) => s.id === editing.sourceId)?.params ?? [];
  // Dirty-tracking → Save disabled until name/source/game/rules/params/aliases actually change.
  const dirty = canonicalJson(editing) !== canonicalJson(editLibBaseline) || canonicalJson(editingAliases) !== canonicalJson(editAliasBaseline);

  // ---- filter predicates (rules + aliases can grow to hundreds; PaginatedEditList pages them) ----
  const rf = ruleFilter.trim().toLowerCase();
  const ruleMatch = (r: RemoteTagRule) =>
    !rf || [r.name, r.titlePattern ?? '', catLabelById[r.categoryId] ?? '', ...r.tags, ...r.tags.map(aliasLabel)]
      .some((s) => s.toLowerCase().includes(rf));
  const af = aliasFilter.trim().toLowerCase();
  const aliasMatch = (a: { tag: string; label: string }) =>
    !af || a.tag.toLowerCase().includes(af) || a.label.toLowerCase().includes(af);
  // One label per tag: a tag already used by ANOTHER row is excluded from a row's picker.
  const usedAliasTags = new Set(editingAliases.map((a) => a.tag).filter(Boolean));
  // #22: tags already assigned to ANY rule — the "only unused" toggle hides these from the rule tag
  // picker so unassigned tags are easy to find (the current row's own tags always stay visible).
  const usedRuleTags = new Set(editing.tagRules.flatMap((r) => r.tags));

  return (
    <div className="remote-lib-mgmt remote-lib-mgmt--fill" ref={rootRef}>
      <div className="remote-lib-mgmt__edit-header">
        <CompactIconButton icon={<ArrowLeftOutlined />} title={t('common.cancel')} onClick={onCancel} />
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
                        onChange={(e) => setEditing((l) => ({ ...l, name: e.target.value }))}
                      />
                    </CompactField>
                    <div className="remote-lib-mgmt__edit-fields-row">
                      <CompactField label={t('remote.fieldSource')} description={t('remote.switchSourceHint')}>
                        <CompactSelect
                          value={editing.sourceId}
                          options={sources.map((s) => ({ value: s.id, label: s.name }))}
                          onChange={(v) =>
                            setEditing((e) => ({
                              ...e,
                              sourceId: v as string,
                              listId: sources.find((s) => s.id === v)?.lists[0]?.id ?? e.listId,
                            }))
                          }
                          style={{ width: '100%' }}
                        />
                      </CompactField>
                      <CompactField label={t('remote.fieldGame')}>
                        <CompactSelect
                          value={editing.listId}
                          options={(sources.find((s) => s.id === editing.sourceId)?.lists ?? []).map((l) => ({ value: l.id, label: l.name }))}
                          onChange={(v) => setEditing((e) => ({ ...e, listId: v as string }))}
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
                  {/* Detail fetch mode for this library's mods (per-library). */}
                  <CompactField label={t('remote.detailModeLabel')} description={t('remote.detailModeHint')}>
                    <CompactSelect
                      value={editing.preferCache ? 'cache' : 'live'}
                      options={[
                        { value: 'live', label: t('remote.detailModeLive') },
                        { value: 'cache', label: t('remote.detailModeCache') },
                      ]}
                      onChange={(v) => setEditing((e) => ({ ...e, preferCache: v === 'cache' }))}
                      style={{ width: '100%' }}
                    />
                  </CompactField>
                  {/* Sync lives with the library (library → detail): the cheap incremental Update +
                      the heavy Full re-sync (re-crawl + prune) — both fire-and-forget to the Activity panel. */}
                  <CompactField label={t('remote.syncSectionLabel')} description={t('remote.syncSectionHint')}>
                    <div className="remote-lib-mgmt__sync-actions">
                      <CompactButton icon={<CloudSyncOutlined />} onClick={() => void syncLibrary(false)}>
                        {t('remote.syncUpdate')}
                      </CompactButton>
                      <CompactButton icon={<ReloadOutlined />} onClick={() => void syncLibrary(true)}>
                        {t('remote.syncFull')}
                      </CompactButton>
                    </div>
                  </CompactField>
                </div>
              ),
            },
            {
              key: 'rules',
              label: <Tooltip title={`${t('remote.tagRulesHint')} ${t('remote.tagRulesDefault')}`}>{t('remote.tabInputRule')}</Tooltip>,
              children: (
                <div className="remote-lib-mgmt__tab remote-lib-mgmt__tab--list">
                  <PaginatedEditList
                    items={editing.tagRules}
                    matches={ruleMatch}
                    filter={ruleFilter}
                    onFilterChange={setRuleFilter}
                    filterPlaceholder={t('remote.filterRules')}
                    page={rulePage}
                    onPageChange={setRulePage}
                    pageSize={pageSize}
                    filterTrailing={
                      <CompactCheckbox
                        className="remote-lib-mgmt__unused-toggle"
                        checked={onlyUnusedRuleTags}
                        onChange={(e) => setOnlyUnusedRuleTags(e.target.checked)}
                      >
                        {t('remote.onlyUnusedTags')}
                      </CompactCheckbox>
                    }
                    emptyNode={<div className="remote-lib-mgmt__rules-empty">{t('remote.noRules')}</div>}
                    renderRow={(rule, i, isLast) => (
                      <div
                        key={i}
                        className="remote-lib-mgmt__rule"
                        ref={isLast ? lastAddedRowRef : undefined}
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
                          options={tagOptions
                            .filter((tag) => !onlyUnusedRuleTags || !usedRuleTags.has(tag) || rule.tags.includes(tag))
                            .map((tag) => ({ value: tag, label: aliasLabel(tag) }))}
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
                    )}
                  />
                </div>
              ),
            },
            {
              key: 'aliases',
              label: <Tooltip title={t('remote.tagAliasesHint')}>{t('remote.tabTagLabel')}</Tooltip>,
              children: (
                <div className="remote-lib-mgmt__tab remote-lib-mgmt__tab--list">
                  {/* One row per tag → one label (translation). The tag is a SEARCHABLE single-select;
                      tags already used by another row are excluded so a tag can't be labeled twice. */}
                  <PaginatedEditList
                    items={editingAliases}
                    matches={aliasMatch}
                    filter={aliasFilter}
                    onFilterChange={setAliasFilter}
                    filterPlaceholder={t('remote.filterAliases')}
                    page={aliasPage}
                    onPageChange={setAliasPage}
                    pageSize={pageSize}
                    rowsClassName="remote-lib-mgmt__alias-grid"
                    renderRow={(alias, i, isLast) => (
                      <div
                        key={i}
                        className="remote-lib-mgmt__alias-row"
                        ref={isLast ? lastAddedRowRef : undefined}
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
                    )}
                  />
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
        {/* Pager for the ACTIVE list tab, centered between Add (left) and Cancel/Save (right). */}
        <div className="remote-lib-mgmt__actions-pager">
          {editTab === 'rules' && (() => {
            const p = paginateFiltered(editing.tagRules, ruleMatch, rulePage, pageSize);
            return p.total > pageSize ? (
              <Pagination size="small" current={p.pageSafe} pageSize={pageSize} total={p.total} showSizeChanger={false} onChange={setRulePage} />
            ) : null;
          })()}
          {editTab === 'aliases' && (() => {
            const p = paginateFiltered(editingAliases, aliasMatch, aliasPage, pageSize);
            return p.total > pageSize ? (
              <Pagination size="small" current={p.pageSafe} pageSize={pageSize} total={p.total} showSizeChanger={false} onChange={setAliasPage} />
            ) : null;
          })()}
        </div>
        <div className="remote-lib-mgmt__actions-right">
          <CompactButton onClick={onCancel}>{t('common.cancel')}</CompactButton>
          <CompactButton type="primary" disabled={!dirty} onClick={() => void handleSaveEdit()}>
            {t('common.save')}
          </CompactButton>
        </div>
      </div>
    </div>
  );
};
