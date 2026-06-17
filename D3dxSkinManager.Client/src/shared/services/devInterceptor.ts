/**
 * Dev-only IPC + event-bus interceptor (NEVER ships in prod — gated by import.meta.env.DEV at the
 * single call site in index.tsx).
 *
 * Why: during desktop-app testing the agent drives the UI over CDP, but native dialogs (the folder
 * picker) and event-driven flows can't be exercised by clicking. This wraps the two singletons —
 * `bridgeService.sendMessage` (the IPC seam) and `eventBus.emit` (the event hub) — to (1) record +
 * console.debug every request/response/event into ring buffers, and (2) expose `window.__d3dx` so a
 * CDP eval can invoke ANY IPC directly and await events:
 *
 *   window.__d3dx.call('MOD','LOAD',{ id })          // drive an IPC, bypass native dialogs
 *   window.__d3dx.waitEvent('MOD','MOD_LIST_UPDATED') // resolves on the next emit
 *   window.__d3dx.recentIpc(20) / .recentEvents(20)   // inspect traffic
 *
 * Driven from the toolkit via `node devtools/dev.mjs cdp ipc|events|iplog` (see devtools/scripts/
 * drive-cdp.mjs; the global name is project.config.mjs `devGlobal`). Adapted from SiblingApp's
 * devInterceptor.ts.
 */
import { bridgeService } from './bridgeService';
import { eventBus } from './eventBus';

interface IpcEntry {
  t: number;
  module: string;
  type: string;
  payload?: unknown;
  ms?: number;
  ok?: boolean;
  error?: string;
  result?: unknown;
}
interface EventEntry { t: number; module: string; type: string; payload?: unknown }

const RING = 300;

export function installDevInterceptor(): void {
  if (typeof window === 'undefined') return;
  const w = window as unknown as { __d3dx?: unknown };
  if (w.__d3dx) return; // idempotent across HMR / StrictMode double-invoke

  const ipc: IpcEntry[] = [];
  const events: EventEntry[] = [];
  const push = <T>(arr: T[], e: T) => { arr.push(e); if (arr.length > RING) arr.shift(); };

  type SendArgs = { module: string; type: string; profileId?: string; payload?: unknown };

  // --- wrap the IPC send seam ---
  const bridge = bridgeService as unknown as { sendMessage: (a: SendArgs) => Promise<unknown> };
  const origSend = bridge.sendMessage.bind(bridgeService);
  bridge.sendMessage = (args: SendArgs) => {
    const start = performance.now();
    const entry: IpcEntry = { t: Date.now(), module: args.module, type: args.type, payload: args.payload };
    push(ipc, entry);
    console.debug(`[IPC →] ${args.module}.${args.type}`, args.payload);
    return origSend(args).then(
      (res) => { entry.ms = Math.round(performance.now() - start); entry.ok = true; entry.result = res; console.debug(`[IPC ✓] ${args.module}.${args.type} (${entry.ms}ms)`, res); return res; },
      (err: { message?: string }) => { entry.ms = Math.round(performance.now() - start); entry.ok = false; entry.error = err?.message; console.debug(`[IPC ✗] ${args.module}.${args.type} (${entry.ms}ms)`, err?.message); throw err; },
    );
  };

  // --- wrap the event hub ---
  const hub = eventBus as unknown as { emit: (e: EventEntry) => void };
  const origEmit = hub.emit.bind(eventBus);
  hub.emit = (event: EventEntry) => {
    push(events, { t: Date.now(), module: event.module, type: event.type, payload: event.payload });
    console.debug(`[EVT] ${event.module}.${event.type}`, event.payload);
    origEmit(event);
  };

  w.__d3dx = {
    eventBus,
    bridge: bridgeService,
    ipc,
    events,
    /** Drive ANY IPC directly (bypasses native dialogs / UI). Returns the response promise. */
    call: (module: string, type: string, payload?: unknown, profileId?: string) =>
      bridge.sendMessage({ module, type, payload, profileId }),
    /** Resolve on the next matching event (or null on timeout) — for CDP awaitPromise verification. */
    waitEvent: (module: string, type: string, timeoutMs = 8000) =>
      new Promise((resolve) => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const off = (eventBus as any).subscribe(module, type, (e: unknown) => { off(); resolve(e); });
        setTimeout(() => { off(); resolve(null); }, timeoutMs);
      }),
    recentIpc: (n = 20) => ipc.slice(-n),
    recentEvents: (n = 20) => events.slice(-n),
    clear: () => { ipc.length = 0; events.length = 0; },
  };
  console.info('[d3dx] dev interceptor installed → window.__d3dx (call(), waitEvent(), recentIpc(), recentEvents())');
}
