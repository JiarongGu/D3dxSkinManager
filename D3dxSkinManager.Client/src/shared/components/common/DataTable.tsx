/**
 * DataTable - Standardized table component with consistent styling
 *
 * Features:
 * - Consistent table styling across the application
 * - Built-in pagination with customizable options
 * - Loading state handling
 * - Row selection support
 * - Responsive design
 * - Customizable page size options
 */

import React from 'react';
import { Table, TableProps } from 'antd';
import { useTranslation } from 'react-i18next';
import './DataTable.css';

export interface DataTableProps<T = any> extends Omit<TableProps<T>, 'pagination'> {
  /**
   * Enable/disable pagination or provide custom pagination config
   * @default true
   */
  pagination?: boolean | {
    pageSize?: number;
    showSizeChanger?: boolean;
    showTotal?: boolean | ((total: number, range: [number, number]) => React.ReactNode);
    pageSizeOptions?: string[];
    position?: ('topLeft' | 'topCenter' | 'topRight' | 'bottomLeft' | 'bottomCenter' | 'bottomRight')[];
  };

  /**
   * Compact mode for dense data display
   * @default false
   */
  compact?: boolean;

  /**
   * Show borders around cells
   * @default false
   */
  bordered?: boolean;

  /**
   * Enable row hover effect
   * @default true
   */
  hoverable?: boolean;

  /**
   * Custom empty text
   */
  emptyText?: string;
}

export function DataTable<T extends object = any>({
  pagination = true,
  compact = false,
  bordered = false,
  hoverable = true,
  emptyText,
  size,
  className,
  ...restProps
}: DataTableProps<T>) {
  const { t } = useTranslation();

  // Build pagination config
  const paginationConfig = React.useMemo(() => {
    if (pagination === false) {
      return false;
    }

    const defaultConfig = {
      pageSize: 10,
      showSizeChanger: true,
      pageSizeOptions: ['10', '20', '50', '100'],
      showTotal: (total: number) => t('common.table.total', { count: total }),
      position: ['bottomRight'] as ('bottomRight')[],
    };

    if (pagination === true) {
      return defaultConfig;
    }

    // Merge custom config with defaults
    return {
      ...defaultConfig,
      ...pagination,
      showTotal: pagination.showTotal === false
        ? undefined
        : pagination.showTotal === true || pagination.showTotal === undefined
          ? defaultConfig.showTotal
          : pagination.showTotal,
    };
  }, [pagination, t]);

  // Build class names
  const tableClassName = React.useMemo(() => {
    const classes = ['data-table'];
    if (compact) classes.push('data-table-compact');
    if (!hoverable) classes.push('data-table-no-hover');
    if (className) classes.push(className);
    return classes.join(' ');
  }, [compact, hoverable, className]);

  // Determine table size
  const tableSize = size || (compact ? 'small' : 'middle');

  // Custom locale for empty state
  const locale = emptyText ? { emptyText } : undefined;

  return (
    <Table<T>
      {...restProps}
      className={tableClassName}
      size={tableSize}
      bordered={bordered}
      pagination={paginationConfig as any}
      locale={locale}
    />
  );
}

/**
 * Re-export common table types for convenience
 */
export type { ColumnsType, ColumnType } from 'antd/es/table';
