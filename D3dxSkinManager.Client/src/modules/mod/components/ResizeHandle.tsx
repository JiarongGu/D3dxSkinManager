import React from 'react';
import classNames from 'classnames';
import './ResizeHandle.css';

interface ResizeHandleProps {
  onMouseDown: (event: React.MouseEvent) => void;
  isResizing: boolean;
}

/**
 * Resize handle component for panel boundaries
 * Provides visual feedback and drag functionality
 */
export const ResizeHandle: React.FC<ResizeHandleProps> = ({ onMouseDown, isResizing }) => {
  return (
    <div
      className={classNames('resize-handle', { 'resize-handle-active': isResizing })}
      onMouseDown={onMouseDown}
      role="separator"
      aria-orientation="vertical"
    >
    </div>
  );
};
