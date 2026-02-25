import React from 'react';
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
      className={`resize-handle ${isResizing ? 'resize-handle-active' : ''}`}
      onMouseDown={onMouseDown}
      role="separator"
      aria-orientation="vertical"
    >
      <div className="resize-handle-indicator" />
    </div>
  );
};
