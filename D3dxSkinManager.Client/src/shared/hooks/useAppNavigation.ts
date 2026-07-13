/**
 * Lightweight app-level navigation.
 *
 * AppContent registers the real tab-switcher via `registerNavigateToTab()`.
 * Tools/screens call `navigateToTab()` or `navigateToModSearch()` without
 * needing context providers or direct store access.
 */

import { loadAllMods, selectCategory } from '../../modules/mod/operations/categoryOperations';
import { useModsStore } from '../../modules/mod/store/modsStore';

let _navigateToTab: ((tab: string) => void) | undefined;

/** Register the tab navigation handler (called once in AppContent) */
export function registerNavigateToTab(fn: (tab: string) => void) {
  _navigateToTab = fn;
  return () => { _navigateToTab = undefined; };
}

/** Navigate to a top-level tab from anywhere */
export function navigateToTab(tab: string) {
  _navigateToTab?.(tab);
}

/**
 * Navigate to mods tab with search query pre-filled.
 * If categoryId is provided, selects that category first.
 * Otherwise switches to "all" view mode so every mod is findable.
 *
 * @param profileId - Active profile ID
 * @param modIds - Mod IDs to search for (joined with | for OR logic)
 * @param categoryId - Optional category to select (from analysis session, etc.)
 */
export async function navigateToModSearch(profileId: string, modIds: string[], categoryId?: string) {
  if (!profileId || modIds.length === 0) return;
  if (categoryId) {
    await selectCategory(profileId, categoryId);
  } else {
    // No explicit category → load ALL to find the mod, then select the mod's OWN category so the
    // tree highlights where it lives (was: left on "all"/no category — #26). selectCategory no-ops
    // if the category isn't a tree node, so this is a safe best-effort with no regression.
    await loadAllMods(profileId);
    const located = useModsStore.getState().mods?.find(m => modIds.includes(m.id));
    if (located?.category) await selectCategory(profileId, located.category);
  }
  const store = useModsStore.getState();
  store.setSearchQuery(modIds.join('|'));
  // Auto-select first matching mod so preview panel shows it
  const firstMatch = store.mods?.find(m => modIds.includes(m.id));
  store.setSelectedMod(firstMatch);
  navigateToTab('mods');
}
