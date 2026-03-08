import { ColDef } from 'ag-grid-community';
import { Tag } from '../../../../shared/types/mod.types';
import { HighlightCellRenderer } from './HighlightCellRenderer';
import { TFunction } from 'i18next';

interface SearchHighlight {
  find: string;
  caseSensitive: boolean;
  useRegex: boolean;
}

export const getColumnDefs = (tags: Tag[], searchHighlight: SearchHighlight | null | undefined, t: TFunction): ColDef[] => [
  {
    field: 'name',
    headerName: t("common.name"),
    flex: 2,
    editable: true,
    cellEditor: 'agTextCellEditor',
    cellRenderer: HighlightCellRenderer,
    cellRendererParams: {
      searchConfig: searchHighlight,
    },
  },
  {
    field: 'author',
    headerName: t("common.author"),
    flex: 1,
    editable: true,
    cellEditor: 'agTextCellEditor',
    cellRenderer: HighlightCellRenderer,
    cellRendererParams: {
      searchConfig: searchHighlight,
    },
  },
  {
    field: 'tags',
    headerName: t("common.tags"),
    flex: 2,
    editable: true,
    cellEditor: 'agTextCellEditor',
    cellRenderer: HighlightCellRenderer,
    cellRendererParams: {
      searchConfig: searchHighlight,
    },
    valueFormatter: (params) => {
      if (Array.isArray(params.value)) {
        return params.value.join(', ');
      }
      return params.value || '';
    },
    valueParser: (params) => {
      // Parse comma-separated input into array
      if (typeof params.newValue === 'string') {
        return params.newValue.split(',').map((t: string) => t.trim()).filter((t: string) => t);
      }
      return params.newValue;
    },
    valueSetter: (params) => {
      // Handle comma-separated input
      if (typeof params.newValue === 'string') {
        params.data.tags = params.newValue.split(',').map((t: string) => t.trim()).filter((t: string) => t);
      } else {
        params.data.tags = params.newValue;
      }
      return true;
    },
  },
  {
    field: 'grading',
    headerName: t('mods.batchEdit.column.grade'),
    width: 100,
    editable: true,
    cellEditor: 'agSelectCellEditor',
    cellEditorParams: {
      values: ['G', 'P', 'R', 'X'],
    },
  },
  {
    field: 'description',
    headerName: t("common.description"),
    flex: 2,
    editable: true,
    cellEditor: 'agLargeTextCellEditor',
    cellEditorPopup: true,
    cellRenderer: HighlightCellRenderer,
    cellRendererParams: {
      searchConfig: searchHighlight,
    },
    cellEditorParams: {
      maxLength: 1000,
      rows: 10,
      cols: 50,
    },
  },
];
