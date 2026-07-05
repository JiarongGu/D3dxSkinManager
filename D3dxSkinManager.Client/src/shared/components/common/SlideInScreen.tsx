import React, { useEffect, useRef, createContext, useContext } from 'react';
import classNames from 'classnames';
import { CloseOutlined } from '@ant-design/icons';
import { Spin } from 'antd';
import { useSlideInScreenContext } from '../../context/SlideInScreenContext';
import './SlideInScreen.css';
import { CompactButton } from '../compact';

// Context to track which screen we're currently inside
const CurrentSlideInScreenContext = createContext<string | undefined>(undefined);

export function useCurrentSlideInScreenId(): string | undefined {
  return useContext(CurrentSlideInScreenContext);
}

export interface SlideInScreenProps {
  id: string;
  title: string;
  children: React.ReactNode;
  width?: string;
  level: number;
  onClose: () => void;
  isClosing?: boolean;
  loading?: boolean;
  loadingText?: string;
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
  loading = false,
  loadingText,
}: SlideInScreenProps) {
  const containerRef = useRef<HTMLDivElement>(null);

  // Handle ESC key to close top screen (disabled during loading)
  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isClosing && !loading) {
        onClose();
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [onClose, isClosing, loading]);

  // Calculate blur backdrop width based on level - smaller widths
  const blurWidth = level === 1 ? '5%' : `${5 + (level - 1) * 3}%`;

  return (
    <div
      className={classNames('slide-in-screen-container', `slide-in-screen-level-${level}`, { closing: isClosing })}
      ref={containerRef}
    >
      {/* Blur backdrop indicator */}
      <div
        className="slide-in-screen-blur-backdrop"
        style={{ width: blurWidth, cursor: loading ? 'default' : 'pointer' }}
        onClick={() => !isClosing && !loading && onClose()}
      >
        {/* Soft edge gradient */}
        <div className="slide-in-screen-blur-edge" />
      </div>

      {/* Main screen panel */}
      <div
        className="slide-in-screen-panel"
        style={{ width }}
      >
        {/* Loading Overlay */}
        {loading && (
          <div className="slide-in-screen-loading-overlay">
            <Spin size="large" />
            {loadingText && <div className="slide-in-screen-loading-text">{loadingText}</div>}
          </div>
        )}

        {/* Header */}
        <div className="slide-in-screen-header">
          <h2 className="slide-in-screen-title">{title}</h2>
          <CompactButton
            type="text"
            icon={<CloseOutlined />}
            onClick={() => !isClosing && !loading && onClose()}
            disabled={loading}
            className="slide-in-screen-close-btn"
          />
        </div>

        {/* Content */}
        <div className="slide-in-screen-content">
          <CurrentSlideInScreenContext.Provider value={id}>
            {children}
          </CurrentSlideInScreenContext.Provider>
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
  const { screens, closeScreen } = useSlideInScreenContext();

  if (screens.length === 0) {
    return null;
  }

  return (
    <div className="slide-in-screen-manager">
      {screens.map((screen) => (
        <SlideInScreen
          key={screen.id}
          id={screen.id}
          title={screen.title}
          width={screen.width}
          level={screen.level}
          onClose={() => closeScreen(screen.id)}
          isClosing={screen.isClosing}
          loading={screen.loading}
          loadingText={screen.loadingText}
        >
          {screen.content}
        </SlideInScreen>
      ))}
    </div>
  );
}
