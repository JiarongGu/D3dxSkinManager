import React, { useMemo, useRef, useCallback } from 'react';
import { AgGridReact } from 'ag-grid-react';
import { ModuleRegistry, AllCommunityModule, themeQuartz } from 'ag-grid-community';
import { ModInfo, Tag } from '../../../../shared/types/mod.types';
import { getColumnDefs } from './columnDefs';
import { useTranslation } from 'react-i18next';
import './BatchEditGrid.css';

// Register AG Grid modules
ModuleRegistry.registerModules([AllCommunityModule]);

// Create custom theme using CSS variables
const customTheme = themeQuartz.withParams({
  backgroundColor: 'var(--color-bg-container)',
  foregroundColor: 'var(--color-text-base)',
  borderColor: 'var(--color-border-secondary)',
  headerBackgroundColor: 'var(--color-bg-elevated)',
  headerTextColor: 'var(--color-text-base)',
  rowBorder: true,
  borderRadius: 0,
  headerFontSize: 14,
  headerFontWeight: 600,
  fontSize: 14,
  wrapperBorder: false,
});

interface BatchEditGridProps {
  mods: ModInfo[];
  tags: Tag[];
  onModsChange: (mods: ModInfo[]) => void;
  searchHighlight?: {
    find: string;
    caseSensitive: boolean;
    useRegex: boolean;
  } | null;
  gridRef?: React.RefObject<AgGridReact | null>;
}

export const BatchEditGrid: React.FC<BatchEditGridProps> = ({
  mods,
  tags,
  onModsChange,
  searchHighlight,
  gridRef: externalGridRef
}) => {
  const { t } = useTranslation();
  const internalGridRef = useRef<AgGridReact>(null);
  const gridRef = externalGridRef || internalGridRef;

  const columnDefs = useMemo(() => getColumnDefs(tags, searchHighlight, t), [tags, searchHighlight, t]);

  const defaultColDef = useMemo(() => ({
    sortable: true,
    filter: true,
    resizable: true,
    suppressKeyboardEvent: (params: any) => {
      // Allow Ctrl+C, Ctrl+V, Ctrl+X
      const event = params.event;
      const key = event.key;
      const ctrlPressed = event.ctrlKey || event.metaKey;

      // Allow clipboard operations
      if (ctrlPressed && (key === 'c' || key === 'v' || key === 'x')) {
        return false;
      }

      return false;
    },
  }), []);

  const onCellValueChanged = useCallback((event: any) => {
    // Update the mods array when cell changes
    const updatedMods = [...mods];
    const index = updatedMods.findIndex(m => m.sha === event.data.sha);
    if (index !== -1) {
      updatedMods[index] = { ...event.data };
      onModsChange(updatedMods);
    }
  }, [mods, onModsChange]);

  const getRowId = useCallback((params: any) => params.data.sha, []);

  return (
    <div className="batch-edit-grid" style={{ height: '100%', width: '100%' }}>
      <AgGridReact
        ref={gridRef}
        rowData={mods}
        getRowId={getRowId}
        columnDefs={columnDefs}
        defaultColDef={defaultColDef}
        onCellValueChanged={onCellValueChanged}
        rowHeight={39}
        headerHeight={39}
        theme={customTheme}
        undoRedoCellEditing={true}
        undoRedoCellEditingLimit={20}
        enableCellTextSelection={true}
        ensureDomOrder={true}
        animateRows={true}
      />
    </div>
  );
};
