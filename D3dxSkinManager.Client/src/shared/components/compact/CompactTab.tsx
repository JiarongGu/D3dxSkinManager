import React from 'react';
import classNames from 'classnames';
import { Button, ButtonProps } from 'antd';
import './CompactTab.css';

/**
 * CompactTab — L1 atom for TOP-TOOLBAR / header nav items (the app-header tabs + the profile
 * switcher trigger). A borderless, transparent, full-toolbar-height (40px) item with a hover bg
 * tint and an `active` primary-tint state.
 *
 * Why a dedicated atom (not CompactButton): CompactButton enforces a boxed 32px height + border
 * via `!important`, which clobbers custom toolbar styling (this was the "tab/profile button broken"
 * regression). Toolbar items are text-style and fill the 40px bar — a different visual contract, so
 * per the L1/L2 atomic rules they get their own atom instead of overriding CompactButton or using
 * raw antd.
 */
export interface CompactTabProps extends Omit<ButtonProps, 'type' | 'size'> {
  /** Selected/active tab — primary color + tint background. */
  active?: boolean;
}

export const CompactTab = React.forwardRef<HTMLButtonElement, CompactTabProps>(
  ({ active = false, className, children, ...rest }, ref) => (
    <Button
      ref={ref}
      type="text"
      className={classNames('compact-tab', { 'compact-tab--active': active }, className)}
      {...rest}
    >
      {children}
    </Button>
  ),
);
CompactTab.displayName = 'CompactTab';

export default CompactTab;
