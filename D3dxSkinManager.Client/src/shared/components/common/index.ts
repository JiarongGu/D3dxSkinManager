/**
 * Common shared components for the application
 *
 * This file exports common UI components.
 * For compact components, import from 'shared/components/compact'
 */

export { ContextMenu } from '../menu/ContextMenu';
export type { ContextMenuItem, ContextMenuProps } from '../menu/ContextMenu';

export { DragDropZone } from './DragDropZone';
export type { DragDropZoneProps } from './DragDropZone';

export { GradingTag } from './GradingTag';
export type { GradingTagProps } from './GradingTag';

export { StatusIcon } from './StatusIcon';
export type { StatusIconProps } from './StatusIcon';

export { AnnotatedTooltip, AnnotationProvider, annotations, useAnnotation } from './TooltipSystem';
export type { AnnotationLevel, TooltipLevel } from './TooltipSystem';

export { SlideInScreen } from './SlideInScreen';
export type { SlideInScreenProps } from './SlideInScreen';

export { MultiTagInput } from './MultiTagInput';
export type { MultiTagInputProps } from './MultiTagInput';
