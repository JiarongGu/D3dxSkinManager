import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Mirror the other remote tests' i18n mock, but interpolate params so the shown/total count is assertable.
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, o?: Record<string, unknown>) => (o ? `${k} ${o.shown}/${o.total}` : k),
    i18n: { language: 'en' },
  }),
}));

import { PaginatedEditList } from '../PaginatedEditList';

const matches = (item: string, filter: string) => !filter || item.includes(filter);

// PaginatedEditList owns only the FIXED search header + shown/total count + the page slice; the PAGER
// lives in the parent's pinned footer (LibraryEditView renders an antd Pagination from the exported
// paginateFiltered) — so page changes are driven through the `page` prop here, not an internal pager.
function Harness({ items, pageSize = 2, initialPage = 1 }: { items: string[]; pageSize?: number; initialPage?: number }) {
  const [filter, setFilter] = React.useState('');
  const [page, setPage] = React.useState(initialPage);
  return (
    <PaginatedEditList
      items={items}
      matches={(it) => matches(it, filter)}
      filter={filter}
      onFilterChange={setFilter}
      filterPlaceholder="search"
      page={page}
      onPageChange={setPage}
      pageSize={pageSize}
      renderRow={(item, index, isLast) => (
        <div key={index} data-testid="row" data-index={index} data-last={isLast ? 'y' : 'n'}>
          {item}
        </div>
      )}
    />
  );
}

const rowTexts = () => screen.getAllByTestId('row').map((r) => r.textContent);

describe('PaginatedEditList', () => {
  it('renders only the first page of rows plus the fixed search header + shown/total count', () => {
    render(<Harness items={['a', 'b', 'c', 'd', 'e']} />);
    expect(rowTexts()).toEqual(['a', 'b']); // pageSize 2
    expect(screen.getByPlaceholderText('search')).toBeTruthy();
    expect(screen.getByText('remote.filterCount 5/5')).toBeTruthy(); // shown/total
  });

  it('always renders the fixed search header + count, even for a short list (the pager is the parent’s)', () => {
    render(<Harness items={['a', 'b']} />);
    expect(rowTexts()).toEqual(['a', 'b']);
    // Search header is a FIXED part of this component (shown even with 0 rows) — not threshold-gated.
    expect(screen.getByPlaceholderText('search')).toBeTruthy();
    expect(screen.getByText('remote.filterCount 2/2')).toBeTruthy();
  });

  it('slices to the requested page via the `page` prop (parent owns the pager)', () => {
    render(<Harness items={['a', 'b', 'c', 'd', 'e']} initialPage={2} />);
    expect(rowTexts()).toEqual(['c', 'd']); // page 2 of pageSize 2
  });

  it('filters by the predicate, keeps the REAL index, and flags the last row', async () => {
    render(<Harness items={['a', 'b', 'c', 'd', 'e']} />);
    await userEvent.type(screen.getByPlaceholderText('search'), 'e');
    const rows = screen.getAllByTestId('row');
    expect(rows.map((r) => r.textContent)).toEqual(['e']);
    expect(rows[0].getAttribute('data-index')).toBe('4'); // real index in the full list, not 0
    expect(rows[0].getAttribute('data-last')).toBe('y'); // 'e' is the newest (last) item
    expect(screen.getByText('remote.filterCount 1/5')).toBeTruthy();
  });
});
