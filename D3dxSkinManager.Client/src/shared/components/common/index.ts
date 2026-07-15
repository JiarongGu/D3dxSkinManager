/**
 * Common shared components for the application
 *
 * This file exports common UI components.
 * For compact components, import from 'shared/components/compact'
 */

export { ContextMenu } from '../menu/ContextMenu';
export type { ContextMenuItem, ContextMenuProps } from '../menu/ContextMenu';

export { StatusIcon } from './StatusIcon';
export type { StatusIconProps } from './StatusIcon';

// AnnotationProvider is an L3 context (touches services/ipc + the settings store) — it lives in
// shared/context, not this atom barrel. Re-exported here for back-compat with existing import sites.
export { AnnotationProvider, useAnnotation } from '../../context/AnnotationContext';
export type { AnnotationLevel } from '../../context/AnnotationContext';

export { SlideInScreen } from './SlideInScreen';
export type { SlideInScreenProps } from './SlideInScreen';

export { CloseButton } from './CloseButton';
export type { CloseButtonProps } from './CloseButton';

export { DataTable } from './DataTable';
export type { DataTableProps, ColumnsType, ColumnType } from './DataTable';

export { CountBadge } from './CountBadge';
export type { CountBadgeProps } from './CountBadge';

export { StatusTag } from './StatusTag';
export type { StatusTagProps, StatusTone } from './StatusTag';

export { HealthStatusIcon } from './HealthStatusIcon';

export { KeyCaptureInput } from './KeyCaptureInput';

export { KeyValueRows } from './KeyValueRows';
export type { KeyValueRowsProps, KeyValueRowItem } from './KeyValueRows';

export { XboxButtonPicker } from './XboxButtonPicker';

export { SearchToolbar } from './SearchToolbar';
export type { SearchToolbarProps } from './SearchToolbar';
