/**
 * Compact Component Library
 *
 * Standardized components for consistent sizing and styling throughout the application.
 * All compact components use flat design in dark theme (no shadows).
 *
 * Usage:
 * import { CompactButton, CompactCard } from 'shared/components/compact';
 */

// Button components
export { default as CompactButton, CompactPrimaryButton, CompactTextButton, CompactLinkButton, CompactDangerButton } from './CompactButton';
export type { CompactButtonProps, CompactButtonSize } from './CompactButton';

// Card component
export { CompactCard } from './CompactCard';
export type { CompactCardProps } from './CompactCard';

// Space component
export { CompactSpace } from './CompactSpace';
export type { CompactSpaceProps } from './CompactSpace';

// Divider component
export { CompactDivider } from './CompactDivider';
export type { CompactDividerProps } from './CompactDivider';

// Text components
export { CompactTitle, CompactParagraph, CompactText } from './CompactText';
export type { CompactTitleProps, CompactParagraphProps } from './CompactText';

// Alert component
export { CompactAlert } from './CompactAlert';
export type { CompactAlertProps } from './CompactAlert';

// Section component
export { CompactSection } from './CompactSection';
export type { CompactSectionProps } from './CompactSection';

// Input components
export { CompactInput, CompactTextArea, CompactPassword } from './CompactInput';
export type { CompactInputProps, CompactTextAreaProps, CompactPasswordProps, CompactInputSize } from './CompactInput';

// Select component
export { CompactSelect } from './CompactSelect';
export type { CompactSelectProps, CompactSelectSize } from './CompactSelect';

// Upload component
export { CompactUpload } from './CompactUpload';
export type { CompactUploadProps } from './CompactUpload';
