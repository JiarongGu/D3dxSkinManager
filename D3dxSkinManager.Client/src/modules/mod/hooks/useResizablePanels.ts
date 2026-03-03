import { useState, useCallback, useEffect, useRef } from 'react';
import { settingsService } from '../../setting/services/settingsService';
import { useModsStore } from '../store/modsStore';

interface PanelSizes {
  categoryWidth: number; // percentage
  modListWidth: number; // percentage
  previewWidth: number; // calculated (remaining)
}

/**
 * Custom hook for managing resizable panel sizes
 * - Reads panel sizes from Zustand store (no loading delay)
 * - Provides drag-to-resize functionality
 * - Persists panel sizes to settings
 */
export function useResizablePanels() {
  const sizes = useModsStore(s => s.panelSizes);
  const setPanelSizes = useModsStore(s => s.setPanelSizes);
  const [isResizing, setIsResizing] = useState<'category' | 'modList' | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const startXRef = useRef<number>(0);
  const startSizesRef = useRef<PanelSizes>({ categoryWidth: 20, modListWidth: 35, previewWidth: 45 });

  // Save panel sizes to settings
  const saveSizes = useCallback(async (newSizes: PanelSizes) => {
    try {
      const panelSize = `${newSizes.categoryWidth} ${newSizes.modListWidth}`;
      await settingsService.updateModPanelSize(panelSize);
    } catch (error: unknown) {
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

      setPanelSizes(newSizes);
    };

    const handleMouseUp = () => {
      setIsResizing(null);
      // Save sizes to backend when resize ends
      void saveSizes(sizes);
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);

    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isResizing, sizes, saveSizes, setPanelSizes]);

  return {
    sizes,
    isResizing,
    startResize,
    containerRef
  };
}
