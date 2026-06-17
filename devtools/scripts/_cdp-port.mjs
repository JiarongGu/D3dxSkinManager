// _cdp-port.mjs — the CDP remote-debugging port is THROWAWAY (a per-session debugging channel), so the
// toolkit randomizes it each launch to avoid colliding with other Chromium/WebView2 instances (e.g.
// another app, or a stale run). `app-dev` picks a random port, launches the app with it, and persists
// it here; `check` / `drive-cdp` / `review` read it so the whole loop targets the same live instance
// without anyone passing the number around. An explicit numeric arg always overrides; cfg.cdpPort is
// the last-resort fallback.
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import cfg from '../project.config.mjs';

// devtools/.cdp-port (git-ignored scratch)
const PORT_FILE = resolve(dirname(fileURLToPath(import.meta.url)), '..', '.cdp-port');

/** A random high port in 9300–9999 (avoids the common 9222/9223 used by other tooling). */
export function randomCdpPort() {
  return 9300 + Math.floor(Math.random() * 700);
}

/** Persist the port chosen for the current app launch. */
export function writeCdpPort(port) {
  try { writeFileSync(PORT_FILE, String(port), 'utf8'); } catch { /* best effort */ }
}

/** The port of the current app launch: persisted value if valid, else cfg.cdpPort fallback. */
export function readCdpPort() {
  try {
    const n = Number(readFileSync(PORT_FILE, 'utf8').trim());
    if (Number.isInteger(n) && n >= 1024 && n <= 65535) return n;
  } catch { /* no persisted port yet */ }
  return cfg.cdpPort;
}

/** Resolve the port for a command: explicit numeric arg wins, else the persisted/launch port. */
export function resolvePort(argv) {
  const explicit = argv.find((a) => /^\d+$/.test(a) && Number(a) >= 1024);
  return explicit ? Number(explicit) : readCdpPort();
}
