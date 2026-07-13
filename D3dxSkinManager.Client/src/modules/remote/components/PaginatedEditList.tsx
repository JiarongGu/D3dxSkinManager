import React from 'react';
import { useTranslation } from 'react-i18next';
import { SearchOutlined } from '@ant-design/icons';
import { CompactInput } from '../../../shared/components/compact';

export const LIST_PAGE_SIZE = 15;

/** Pure filter+page slice — shared by the list body (rows) and the footer pager (which the parent renders
 *  centered in the pinned footer). Returns the page slice plus the totals the pager needs. */
export function paginateFiltered<T>(items: T[], matches: (item: T) => boolean, page: number, pageSize = LIST_PAGE_SIZE) {
  const filtered = items.map((item, index) => ({ item, index })).filter((x) => matches(x.item));
  const maxPage = Math.max(1, Math.ceil(filtered.length / pageSize));
  const pageSafe = Math.min(page, maxPage);
  const paged = filtered.slice((pageSafe - 1) * pageSize, pageSafe * pageSize);
  return { paged, total: filtered.length, pageSafe, maxPage };
}

/**
 * A search-filtered, paginated list of editable rows — the shared machinery behind the library editor's
 * "Input rules" and "Tag labels" tabs (both can grow to hundreds of rows; only a page mounts at a time).
 * The FILTER narrows first, then the result pages. Extracted from RemoteLibraryManagementScreen where the
 * two tabs had byte-for-byte twin filter/paginate blocks.
 *
 * The row CONTENT is caller-specific (`renderRow`); this owns only the search box, the shown/total count,
 * the page slice, and the pager. `renderRow` receives the item's REAL index (into the full `items` array,
 * not the filtered/paged view) so edit/reorder/delete target the right entry, plus `isLast` so the caller
 * can attach a scroll-into-view ref to the newest row.
 *
 * Uses the `remote-lib-mgmt__*` styles from RemoteLibraryManagementScreen.css (its private sub-component).
 */
export interface PaginatedEditListProps<T> {
  /** The FULL list (unfiltered) — count/threshold/last-index are computed from this. */
  items: T[];
  /** Filter predicate for one item (caller closes over the current filter text + any alias/label maps). */
  matches: (item: T) => boolean;
  filter: string;
  onFilterChange: (value: string) => void;
  filterPlaceholder: string;
  page: number;
  onPageChange: (page: number) => void;
  /** Render one row. `index` is the item's position in the full `items` array; `isLast` marks the newest. */
  renderRow: (item: T, index: number, isLast: boolean) => React.ReactNode;
  /** Extra control pinned to the RIGHT end of the search row (e.g. the rules tab's "unused tags" toggle). */
  filterTrailing?: React.ReactNode;
  pageSize?: number;
  /** When set, the paged rows are wrapped in a div with this class (e.g. the alias grid). Omit to render
   *  rows as direct children so they keep the parent tab's flex-column gap (the rules layout). */
  rowsClassName?: string;
  /** Rendered when the full list is empty (e.g. a "no rules yet" hint). */
  emptyNode?: React.ReactNode;
}

export function PaginatedEditList<T>({
  items,
  matches,
  filter,
  onFilterChange,
  filterPlaceholder,
  page,
  onPageChange,
  renderRow,
  filterTrailing,
  pageSize = LIST_PAGE_SIZE,
  rowsClassName,
  emptyNode,
}: PaginatedEditListProps<T>) {
  const { t } = useTranslation();

  // Keep each row's REAL index so edit/reorder/delete target the right entry after filtering.
  const { paged, total } = paginateFiltered(items, matches, page, pageSize);
  const lastIndex = items.length - 1;

  const rows = paged.map(({ item, index }) => renderRow(item, index, index === lastIndex));

  return (
    <>
      {/* Search is ALWAYS shown (even with 0 rows) so the list header stays consistent; the pager lives
          in the pinned footer (rendered by the parent from paginateFiltered) — not inline here. */}
      <div className="remote-lib-mgmt__filter">
        <CompactInput
          prefix={<SearchOutlined />}
          allowClear
          value={filter}
          placeholder={filterPlaceholder}
          onChange={(e) => {
            onFilterChange(e.target.value);
            onPageChange(1);
          }}
        />
        <span className="remote-lib-mgmt__filter-count">
          {t('remote.filterCount', { shown: total, total: items.length })}
        </span>
        {filterTrailing && <span className="remote-lib-mgmt__filter-trailing">{filterTrailing}</span>}
      </div>
      {items.length === 0 && emptyNode}
      {rowsClassName ? <div className={rowsClassName}>{rows}</div> : rows}
    </>
  );
}
