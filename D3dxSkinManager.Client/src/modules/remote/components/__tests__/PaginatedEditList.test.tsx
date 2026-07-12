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

function Harness({ items, pageSize = 2, filterThreshold = 2 }: { items: string[]; pageSize?: number; filterThreshold?: number }) {
  const [filter, setFilter] = React.useState('');
  const [page, setPage] = React.useState(1);
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
      filterThreshold={filterThreshold}
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
  it('renders only the first page of rows and a pager when the list exceeds one page', () => {
    render(<Harness items={['a', 'b', 'c', 'd', 'e']} />);
    expect(rowTexts()).toEqual(['a', 'b']); // pageSize 2
    expect(screen.getByPlaceholderText('search')).toBeTruthy(); // 5 > threshold 2
    expect(screen.getByText('remote.filterCount 5/5')).toBeTruthy(); // shown/total
  });

  it('hides the search box and pager for a list at/under the threshold', () => {
    render(<Harness items={['a', 'b']} />);
    expect(rowTexts()).toEqual(['a', 'b']);
    expect(screen.queryByPlaceholderText('search')).toBeNull();
    // total (2) not > pageSize (2) → no pager
    expect(screen.queryByText('remote.filterCount 2/2')).toBeNull();
  });

  it('pages to the next slice on pagination change', async () => {
    render(<Harness items={['a', 'b', 'c', 'd', 'e']} />);
    await userEvent.click(screen.getByTitle('2')); // antd pager item for page 2
    expect(rowTexts()).toEqual(['c', 'd']);
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
