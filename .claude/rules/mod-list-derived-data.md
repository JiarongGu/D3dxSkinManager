# Surfacing per-mod (or per-category) backend data on the mod/category UI

When you want to show derived backend data next to each mod row or category card — load state, last-scan
health, fix status, sizes, anything keyed by mod id — follow this **6-step chain**. It keeps the heavy
work on the backend, the live data in the mods store, and the view dumb. Two instances exist to copy:
**`activeMods`** (per-category active indicator) and **`modHealth`** (mod-list last-scan health badge).

## The chain

1. **Backend query → service → facade IPC.**
   - Repo: add a query returning the data (e.g. `GetLatestFindingPerModAsync` — one row per mod).
   - Service: shape it into a small DTO list (e.g. `ModHealthSummary { ModId, … }`), filter to what the
     UI needs (don't ship healthy/empty rows).
   - Facade: one IPC route returning the list (e.g. `ToolFacade` `ANALYSIS_GET_LATEST_HEALTH`). It
     serializes camelCase (see `enum-serialization.md`).

2. **Frontend IPC service method** — `toolService`/`modService` `sendArrayMessage<T>('TYPE', profileId)`.

3. **modsStore field + setter** — a `Record<modId, T>` (O(1) row lookup) or `T[]`. Mirror `activeMods`/
   `modHealth`: add to the state interface, `initialState`, an action `setX`. (DEV `window.__modsStore`
   exposure already covers it.)

4. **`modOperations.refreshX(profileId)`** — call the service, build the map, `useModsStore.getState().setX(map)`.
   Swallow + log errors (never throw into the provider).

5. **`ModProvider` wiring** — call `refreshX` in the **profile-change effect** (initial load) AND subscribe
   to the event that invalidates it, re-calling `refreshX`; add the unsubscribe to the cleanup return.
   - `activeMods` ← refreshed in `handleModListUpdate` (load/unload/delete) + on profile change.
   - `modHealth` ← refreshed on `Module.TOOL` / `ToolsEventType.MOD_ANALYSIS_COMPLETE` + on profile change
     (health only changes when a scan finishes, not on load/unload).
   - Pick the event by *what actually changes the data* — don't blanket-refresh on `MOD_LIST_UPDATED`.

6. **Consumer reads the store + renders** — `useModsStore(s => s.x)`; render a small L1 atom
   (`HealthStatusIcon`, a dot, a `StatusTag`). Keep it dumb; no IPC in the view.

## Freshness honesty
Some derived data is **always-fresh** (filesystem-derived: `activeMods`, the mod-card loaded/unavailable/
orphaned states) and some is **point-in-time** (`modHealth` reflects the mod as last scanned). For
point-in-time data, say so in the tooltip ("Last scan: …") and only show high-confidence states
(warning/error) — never imply it's live. Don't try to gray-out "stale since edited": a fix-patch may not
bump any timestamp, so you can't reliably detect it.
