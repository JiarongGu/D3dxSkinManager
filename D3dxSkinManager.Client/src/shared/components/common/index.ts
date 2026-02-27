/**
 * Common shared components for the application
 *
 * This file exports common UI components.
 * For compact components, import from 'shared/components/compact'
 */

export { ContextMenu } from '../menu/ContextMenu';
export type { ContextMenuItem, ContextMenuProps } from '../menu/ContextMenu';

export { GradingTag } from '../../../modules/mod/components/GradingTag';
export type { GradingTagProps } from '../../../modules/mod/components/GradingTag';

export { StatusIcon } from './StatusIcon';
export type { StatusIconProps } from './StatusIcon';

export { AnnotatedTooltip, AnnotationProvider, annotations, useAnnotation } from './TooltipSystem';
export type { AnnotationLevel, TooltipLevel } from './TooltipSystem';

export { SlideInScreen } from './SlideInScreen';
export type { SlideInScreenProps } from './SlideInScreen';

export { CloseButton } from './CloseButton';
export type { CloseButtonProps } from './CloseButton';

export { DataTable } from './DataTable';
export type { DataTableProps, ColumnsType, ColumnType } from './DataTable';
