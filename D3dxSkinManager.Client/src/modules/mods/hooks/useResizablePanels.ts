import { useState, useCallback, useEffect, useRef } from 'react';
import { settingsService } from '../../settings/services/settingsService';

interface PanelSizes {
  categoryWidth: number; // percentage
  modListWidth: number; // percentage
  previewWidth: number; // calculated (remaining)
}

const DEFAULT_SIZES: PanelSizes = {
  categoryWidth: 20,
  modListWidth: 35,
  previewWidth: 45
};

/**
 * Custom hook for managing resizable panel sizes
 * - Loads panel sizes from global settings
 * - Provides drag-to-resize functionality
 * - Persists panel sizes to settings
 */
export function useResizablePanels() {
  const [sizes, setSizes] = useState<PanelSizes>(DEFAULT_SIZES);
  const [isResizing, setIsResizing] = useState<'category' | 'modList' | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const startXRef = useRef<number>(0);
  const startSizesRef = useRef<PanelSizes>(DEFAULT_SIZES);

  // Load panel sizes from settings on mount
  useEffect(() => {
    const loadSizes = async () => {
      try {
        const settings = await settingsService.getGlobalSettings();
        if (settings.tabs?.mod?.panelSize) {
          const [category, modList] = settings.tabs.mod.panelSize.split(' ').map(Number);
          if (!isNaN(category) && !isNaN(modList)) {
            setSizes({
              categoryWidth: category,
              modListWidth: modList,
              previewWidth: 100 - category - modList
            });
          }
        }
      } catch (error) {
        console.error('Failed to load panel sizes:', error);
      }
    };
    void loadSizes();
  }, []);

  // Save panel sizes to settings
  const saveSizes = useCallback(async (newSizes: PanelSizes) => {
    try {
      const panelSize = `${newSizes.categoryWidth} ${newSizes.modListWidth}`;
      await settingsService.updateModPanelSize(panelSize);
    } catch (error) {
      console.error('Failed to save panel sizes:', error);
    }
  }, []);

  // Start resizing
  const startResize = useCallback((panel: 'category' | 'modList', event: React.MouseEvent) => {
    event.preventDefault();
    setIsResizing(panel);
    startXRef.current = event.clientX;
    startSizesRef.current = { ...sizes };
  }, [sizes]);

  // Handle mouse move during resize
  useEffect(() => {
    if (!isResizing || !containerRef.current) return;

    const handleMouseMove = (event: MouseEvent) => {
      if (!containerRef.current) return;

      const containerWidth = containerRef.current.offsetWidth;
      const deltaX = event.clientX - startXRef.current;
      const deltaPercent = (deltaX / containerWidth) * 100;

      const newSizes = { ...startSizesRef.current };

      if (isResizing === 'category') {
        // Resize category panel (affects category and mod list)
        newSizes.categoryWidth = Math.max(10, Math.min(50, startSizesRef.current.categoryWidth + deltaPercent));
        newSizes.previewWidth = 100 - newSizes.categoryWidth - newSizes.modListWidth;
      } else if (isResizing === 'modList') {
        // Resize mod list panel (affects mod list and preview)
        newSizes.modListWidth = Math.max(20, Math.min(60, startSizesRef.current.modListWidth + deltaPercent));
        newSizes.previewWidth = 100 - newSizes.categoryWidth - newSizes.modListWidth;
      }

      // Ensure preview has minimum width
      if (newSizes.previewWidth < 20) {
        if (isResizing === 'category') {
          newSizes.categoryWidth = 100 - newSizes.modListWidth - 20;
        } else {
          newSizes.modListWidth = 100 - newSizes.categoryWidth - 20;
        }
        newSizes.previewWidth = 20;
      }

      setSizes(newSizes);
    };

    const handleMouseUp = () => {
      setIsResizing(null);
      // Save sizes when resize ends
      void saveSizes(sizes);
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);

    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isResizing, sizes, saveSizes]);

  return {
    sizes,
    isResizing,
    startResize,
    containerRef
  };
}
