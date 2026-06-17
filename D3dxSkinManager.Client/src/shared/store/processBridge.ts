/**
 * Global bridge: backend ProcessRegistry → processStore.
 *
 * Call initProcessBridge() once at app startup. Does an initial GET_PROCESSES fetch, then keeps the
 * store in sync from the consolidated SYSTEM/PROCESS_LIST_UPDATED snapshot event. Because the backend
 * is authoritative, the frontend never mutates the list directly — it just mirrors snapshots.
 */
import { eventBus, Module, SystemEventType } from '../services/eventBus';
import { systemService } from '../services/ipc';
import { useProcessStore, ProcessInfo } from './processStore';

type ProcessSnapshot = { processes?: ProcessInfo[] } | undefined;

export function initProcessBridge(): () => void {
  // DEV-only: expose the store so the UI can be driven/verified in a plain Chrome tab (pure React,
  // no backend) — e.g. window.__processStore.getState().setProcesses([...]). Stripped from prod.
  if (import.meta.env.DEV) {
    (window as unknown as { __processStore?: unknown }).__processStore = useProcessStore;
  }

  // Initial snapshot (covers processes already running before this bridge subscribed).
  void systemService
    .getProcesses()
    .then((r) => useProcessStore.getState().setProcesses(r.processes ?? []))
    .catch(() => { /* backend not ready yet — the event will populate it */ });

  const unsub = eventBus.subscribe(
    Module.SYSTEM,
    SystemEventType.PROCESS_LIST_UPDATED,
    (e) => {
      const payload = e.payload as ProcessSnapshot;
      useProcessStore.getState().setProcesses(payload?.processes ?? []);
    },
  );

  return unsub;
}
