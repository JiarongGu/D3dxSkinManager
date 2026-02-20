import { useState, useCallback, useMemo } from 'react';
import { notification } from '../../../shared/utils/notification';
import { ModInfo, ModFilters } from '../../../shared/types/mod.types';
import { modService } from '../../mods/services/modService';
import { profile } from 'console';

export const useModFilters = (profileId: string, mods: ModInfo[]) => {
  const [filters, setFilters] = useState<ModFilters>({
    searchTerm: '',
    selectedObject: '',
    selectedGrading: ''
  });
  const [loading, setLoading] = useState(false);

  const filteredMods = useMemo(() => {
    let filtered = [...mods];

    // Apply object filter
    if (filters.selectedObject) {
      filtered = filtered.filter(mod => mod.category === filters.selectedObject);
    }

    // Apply grading filter
    if (filters.selectedGrading) {
      filtered = filtered.filter(mod => mod.grading === filters.selectedGrading);
    }

    // Apply search filter (basic client-side)
    if (filters.searchTerm) {
      const lowerSearch = filters.searchTerm.toLowerCase();
      filtered = filtered.filter(mod =>
        mod.name.toLowerCase().includes(lowerSearch) ||
        mod.category.toLowerCase().includes(lowerSearch) ||
        mod.author.toLowerCase().includes(lowerSearch) ||
        mod.tags.some(tag => tag.toLowerCase().includes(lowerSearch))
      );
    }

    return filtered;
  }, [mods, filters]);

  const handleSearch = useCallback(async (searchTerm: string) => {
    if (!searchTerm.trim()) {
      setFilters(prev => ({ ...prev, searchTerm: '' }));
      return mods;
    }

    try {
      setLoading(true);
      // Use backend search for advanced features (negation, multi-term)
      const results = await modService.searchMods(profileId, searchTerm);
      return results;
    } catch (error) {
      notification.error('Search failed: ' + (error as Error).message);
      return mods;
    } finally {
      setLoading(false);
    }
  }, [mods, profileId]);

  const updateFilter = useCallback((key: keyof ModFilters, value: string) => {
    setFilters(prev => ({ ...prev, [key]: value }));
  }, []);

  const clearFilters = useCallback(() => {
    setFilters({
      searchTerm: '',
      selectedObject: '',
      selectedGrading: ''
    });
  }, []);

  const hasActiveFilters = useMemo(() => {
    return !!(filters.searchTerm || filters.selectedObject || filters.selectedGrading);
  }, [filters]);

  return {
    filters,
    filteredMods,
    loading,
    updateFilter,
    clearFilters,
    handleSearch,
    hasActiveFilters
  };
};
