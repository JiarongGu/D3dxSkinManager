import { useState, useCallback, useEffect, useRef, useMemo } from 'react';
import { debounce } from 'lodash-es';
import { useModsStore } from '../store/modsStore';
import { profileService } from '../../../shared/services/ipc';
import { useProfile } from '../../../shared/context/ProfileContext';
import logger from '../../../shared/utils/logger';

interface PanelSizes {
  categoryWidth: number; // percentage
  modListWidth: number; // percentage
  previewWidth: number; // calculated (remaining)
}

interface UseResizablePanelsOptions {
  minCategoryWidth?: number; // Minimum width for category panel in pixels (default: 240)
  minModListWidth?: number;  // Minimum width for mod list panel in pixels (default: 260)
}

/**
 * Custom hook for managing resizable panel sizes
 * - Reads panel sizes from Zustand store (no loading delay)
 * - Provides drag-to-resize functionality
 * - Persists panel sizes to settings with debouncing
 * - Enforces configurable minimum widths for category and mod list panels
 */
export function useResizablePanels(options: UseResizablePanelsOptions = {}) {
  const { minCategoryWidth = 240, minModListWidth = 260 } = options;
  const storedSizes = useModsStore(s => s.panelSizes);
  const setPanelSizes = useModsStore(s => s.setPanelSizes);
  const { selectedProfileId } = useProfile();
  const [isResizing, setIsResizing] = useState<'category' | 'modList' | null>(null);
  const [containerWidth, setContainerWidth] = useState<number>(0);
  const containerRef = useRef<HTMLDivElement>(null);
  const startXRef = useRef<number>(0);
  const startSizesRef = useRef<PanelSizes>({ categoryWidth: 20, modListWidth: 35, previewWidth: 45 });

  // Track container width changes on window resize
  useEffect(() => {
    const updateContainerWidth = () => {
      if (containerRef.current) {
        setContainerWidth(containerRef.current.offsetWidth);
      }
    };

    // Initial measurement
    updateContainerWidth();

    // Listen to window resize
    window.addEventListener('resize', updateContainerWidth);

    return () => {
      window.removeEventListener('resize', updateContainerWidth);
    };
  }, []);

  // Calculate adjusted sizes to ensure minimum widths for panels
  const sizes = useMemo(() => {
    // Return undefined if sizes haven't been loaded yet
    if (!storedSizes) return undefined;

    if (!containerWidth) return storedSizes;

    const minCategoryWidthPercent = (minCategoryWidth / containerWidth) * 100;
    const minModListWidthPercent = (minModListWidth / containerWidth) * 100;

    let adjustedSizes = { ...storedSizes };

    // Enforce minimum category width
    if (adjustedSizes.categoryWidth < minCategoryWidthPercent) {
      adjustedSizes.categoryWidth = minCategoryWidthPercent;
    }

    // Enforce minimum mod list width
    if (adjustedSizes.modListWidth < minModListWidthPercent) {
      adjustedSizes.modListWidth = minModListWidthPercent;
    }

    // Recalculate preview width
    adjustedSizes.previewWidth = 100 - adjustedSizes.categoryWidth - adjustedSizes.modListWidth;

    return adjustedSizes;
  }, [storedSizes, containerWidth, minCategoryWidth, minModListWidth]);

  // Create a debounced save function with 200ms delay
  const debouncedSave = useMemo(
    () => debounce(async (newSizes: PanelSizes, profileId: string | undefined) => {
      if (!profileId) {
        logger.warn('[useResizablePanels] No profile selected, cannot save panel sizes');
        return;
      }

      try {
        // Round to 1 decimal place for cleaner values
        const categoryWidth = Math.round(newSizes.categoryWidth * 10) / 10;
        const modListWidth = Math.round(newSizes.modListWidth * 10) / 10;
        const panelSize = `${categoryWidth} ${modListWidth}`;

        logger.info('[useResizablePanels] Saving panel sizes for profile', profileId, ':', panelSize, 'Preview:', Math.round(newSizes.previewWidth * 10) / 10);
        await profileService.updateModPanelSize(profileId, panelSize);
      } catch (error: unknown) {
        logger.error('[useResizablePanels] Failed to save panel sizes:', error);
      }
    }, 200),
    []
  );

  // Cleanup debounced function on unmount
  useEffect(() => {
    return () => {
      debouncedSave.cancel();
    };
  }, [debouncedSave]);

  // Start resizing
  const startResize = useCallback((panel: 'category' | 'modList', event: React.MouseEvent) => {
    // Don't allow resizing if sizes haven't been loaded yet
    if (!sizes) return;

    event.preventDefault();
    setIsResizing(panel);
    startXRef.current = event.clientX;
    startSizesRef.current = { ...sizes };
  }, [sizes]);

  // Handle mouse move during resize
  useEffect(() => {
    if (!isResizing) return;

    const handleMouseMove = (event: MouseEvent) => {
      if (!containerRef.current) return;

      const containerWidth = containerRef.current.offsetWidth;
      const deltaX = event.clientX - startXRef.current;
      const deltaPercent = (deltaX / containerWidth) * 100;

      const newSizes = { ...startSizesRef.current };

      // Calculate minimum widths as percentages based on configured minimums
      const minCategoryWidthPercent = (minCategoryWidth / containerWidth) * 100;
      const minModListWidthPercent = (minModListWidth / containerWidth) * 100;

      if (isResizing === 'category') {
        // Resize category panel (affects category and mod list)
        // Enforce minimum width (calculated as percentage, max 50%)
        newSizes.categoryWidth = Math.max(minCategoryWidthPercent, Math.min(50, startSizesRef.current.categoryWidth + deltaPercent));
        newSizes.previewWidth = 100 - newSizes.categoryWidth - newSizes.modListWidth;
      } else if (isResizing === 'modList') {
        // Resize mod list panel (affects mod list and preview)
        // Enforce minimum width (calculated as percentage, max 60%)
        newSizes.modListWidth = Math.max(minModListWidthPercent, Math.min(60, startSizesRef.current.modListWidth + deltaPercent));
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

      // Update the store immediately for UI responsiveness
      setPanelSizes(newSizes);
      // Trigger debounced save
      debouncedSave(newSizes, selectedProfileId);
    };

    const handleMouseUp = () => {
      setIsResizing(null);
      // Ensure final save happens
      debouncedSave.flush();
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);

    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isResizing, setPanelSizes, debouncedSave, selectedProfileId, minCategoryWidth, minModListWidth]);

  return {
    sizes,
    isResizing,
    startResize,
    containerRef
  };
}
