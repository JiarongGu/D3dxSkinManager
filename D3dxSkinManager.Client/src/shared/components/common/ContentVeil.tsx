import React from 'react';
import classNames from 'classnames';
import './ContentVeil.css';

interface ContentVeilProps {
  /** True → the wrapped media is veiled (CSS blur + badge) until hovered. */
  veiled: boolean;
  /** Badge text shown over the veil (pass the i18n'd label; default is the English fallback). */
  badge?: string;
  /** Hover reveals by default; pass false for surfaces that must stay veiled. */
  revealOnHover?: boolean;
  className?: string;
  children: React.ReactNode;
}

/**
 * L1 atom: a pure-CSS veil for sensitive media — `filter: blur()` over the wrapped image plus a
 * centered badge; hovering reveals (opt-out via revealOnHover={false}). Purely visual — the
 * verdict/decision lives in the consumer (useNsfwVerdicts + the global blur toggle).
 */
export const ContentVeil: React.FC<ContentVeilProps> = ({
  veiled,
  badge = 'Sensitive',
  revealOnHover = true,
  className,
  children,
}) => (
  <div
    className={classNames('content-veil', className, {
      'content-veil--active': veiled,
      'content-veil--hover-reveal': veiled && revealOnHover,
    })}
  >
    {children}
    {veiled && <span className="content-veil__badge">{badge}</span>}
  </div>
);
