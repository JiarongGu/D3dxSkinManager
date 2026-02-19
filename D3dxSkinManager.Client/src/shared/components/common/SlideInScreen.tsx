import React, { useEffect, useRef } from 'react';
import { CloseOutlined } from '@ant-design/icons';
import { Button } from 'antd';
import { useSlideInScreen } from '../../context/SlideInScreenContext';
import './SlideInScreen.css';

export interface SlideInScreenProps {
  id: string;
  title: string;
  children: React.ReactNode;
  width?: string;
  level: number;
  onClose: () => void;
  isClosing?: boolean;
}

/**
 * SlideInScreen component - Application-style slide-in panel
 * Slides in from the right with blur backdrop indicating depth level
 * Animates out when closing
 */
export function SlideInScreen({
  id,
  title,
  children,
  width = '80%',
  level,
  onClose,
  isClosing = false,
}: SlideInScreenProps) {
  const containerRef = useRef<HTMLDivElement>(null);

  // Handle ESC key to close top screen
  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isClosing) {
        onClose();
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [onClose, isClosing]);

  // Calculate blur backdrop width based on level - smaller widths
  const blurWidth = level === 1 ? '5%' : `${5 + (level - 1) * 3}%`;

  return (
    <div
      className={`slide-in-screen-container slide-in-screen-level-${level} ${isClosing ? 'closing' : ''}`}
      ref={containerRef}
    >
      {/* Blur backdrop indicator */}
      <div
        className="slide-in-screen-blur-backdrop"
        style={{ width: blurWidth }}
        onClick={() => !isClosing && onClose()}
      >
        {/* Soft edge gradient */}
        <div className="slide-in-screen-blur-edge" />
      </div>

      {/* Main screen panel */}
      <div
        className="slide-in-screen-panel"
        style={{ width }}
      >
        {/* Header */}
        <div className="slide-in-screen-header">
          <h2 className="slide-in-screen-title">{title}</h2>
          <Button
            type="text"
            icon={<CloseOutlined />}
            onClick={() => !isClosing && onClose()}
            className="slide-in-screen-close-btn"
          />
        </div>

        {/* Content */}
        <div className="slide-in-screen-content">
          {children}
        </div>
      </div>
    </div>
  );
}

/**
 * SlideInScreenManager - Renders all active slide-in screens
 * Must be placed inside SlideInScreenProvider
 */
export function SlideInScreenManager() {
  const { screens, closeScreen } = useSlideInScreen();

  if (screens.length === 0) {
    return null;
  }

  return (
    <div className="slide-in-screen-manager">
      {screens.map((screen, index) => (
        <SlideInScreen
          key={screen.id}
          id={screen.id}
          title={screen.title}
          width={screen.width}
          level={index + 1}
          onClose={() => closeScreen(screen.id)}
          isClosing={screen.isClosing}
        >
          {screen.content}
        </SlideInScreen>
      ))}
    </div>
  );
}
