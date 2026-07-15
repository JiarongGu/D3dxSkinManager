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
export { default as CompactButton, CompactPrimaryButton, CompactTextButton, CompactLinkButton, CompactDangerButton, CompactWarningButton, CompactSuccessButton } from './CompactButton';
export type { CompactButtonProps, CompactButtonSize } from './CompactButton';

// Tab / top-toolbar nav item (transparent, fills toolbar height, active tint) — app-header tabs + profile trigger
export { CompactTab } from './CompactTab';
export type { CompactTabProps } from './CompactTab';

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

// Field component (labeled-field row for config/tooling screens)
export { CompactField } from './CompactField';
export type { CompactFieldProps } from './CompactField';

// Icon button (standardized borderless tone-aware icon action)
export { CompactIconButton } from './CompactIconButton';
export type { CompactIconButtonProps, IconButtonTone } from './CompactIconButton';

// Count chip (label + count pill / filter toggle; keeps CJK label + Latin count aligned)
export { CountChip } from './CountChip';
export type { CountChipProps, CountChipTone } from './CountChip';

// Input components
export { CompactInput, CompactTextArea, CompactPassword } from './CompactInput';
export type { CompactInputProps, CompactTextAreaProps, CompactPasswordProps, CompactInputSize } from './CompactInput';

// Numeric input (consistent heights with CompactInput)
export { CompactInputNumber } from './CompactInputNumber';
export type { CompactInputNumberProps } from './CompactInputNumber';

// Select component
export { CompactSelect } from './CompactSelect';
export type { CompactSelectProps, CompactSelectSize } from './CompactSelect';

// AutoComplete (typeahead free-text input; reuses CompactSelect sizing)
export { CompactAutoComplete } from './CompactAutoComplete';
export type { CompactAutoCompleteProps, CompactAutoCompleteSize } from './CompactAutoComplete';

// Upload component
export { CompactUpload } from './CompactUpload';
export type { CompactUploadProps } from './CompactUpload';

// Thumbnail Upload component
export { CompactThumbnailUpload } from './CompactThumbnailUpload';
export type { CompactThumbnailUploadProps } from './CompactThumbnailUpload';

// Switch component
export { CompactSwitch } from './CompactSwitch';
export type { CompactSwitchProps } from './CompactSwitch';

// Checkbox component
export { CompactCheckbox } from './CompactCheckbox';
export type { CompactCheckboxProps } from './CompactCheckbox';
