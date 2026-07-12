#!/usr/bin/env node
// app-dev.mjs — one allow-listed command for the desktop-app lifecycle (kill / start / restart / wait).
// Why: the agent's start/stop commands were compound PowerShell ($env:…; Start-Process; loop) that the
// permission prefix-matcher couldn't allow, so they kept prompting. Consolidating into ONE node script
// lets a single allow rule (`Bash(node devtools/dev.mjs app:*)`) cover the whole loop — unattended dev.
//
// Usage:  node devtools/dev.mjs app <action> [port]
//   kill            — force-kill the DEV instance of D3dxSkinManager.exe (path-matched to the repo bin
//                     exe — a user-installed copy running elsewhere is never touched); frees the DLL lock
//   start [port]    — launch the app exe in Development mode (loads the live Vite server) with a CDP
//                     remote-debugging port (RANDOM per launch unless [port] given; persisted to
//                     devtools/.cdp-port); logs → app-dev-stdout/stderr.txt. ENSURES the Vite dev
//                     server is up first — dev mode loads it, so without it the webview = chrome-error.
//   restart [port]  — kill + start + wait-for-CDP (the usual post-backend-build step)  [DEFAULT]
//   vite            — start the Vite dev server (:3517) if not already up (background; logs → vite-*.txt)
//   wait [port]     — just poll the CDP endpoint until it answers
//   reset-db        — kill the app + delete the dev sqlite db (app.db + -wal/-shm) for a clean slate
// Zero deps (Node 24 global fetch). Windows-only (taskkill).

import { spawn, execSync } from 'node:child_process';
import { openSync, rmSync, existsSync } from 'node:fs';
import { resolve, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import cfg from '../project.config.mjs';
import { randomCdpPort, writeCdpPort, readCdpPort } from './_cdp-port.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const EXE = resolve(repoRoot, cfg.exe);
const EXE_NAME = basename(cfg.exe);

const argv = process.argv.slice(2);
const action = (argv[0] || 'restart').toLowerCase();
// CDP port: a launch (start/restart/rebuild) RANDOMIZES it (unless an explicit port arg is given) and
// persists it to devtools/.cdp-port so check/cdp/review target the same instance; non-launch actions
// read that persisted port. Explicit numeric arg always wins.
const explicitPort = argv.find((a) => /^\d+$/.test(a));
const isLaunch = action === 'start' || action === 'restart' || action === 'rebuild';
const port = explicitPort ? Number(explicitPort) : (isLaunch ? randomCdpPort() : readCdpPort());
if (isLaunch) writeCdpPort(port);
// Debug flags as ARGS (so the whole command still matches the `node devtools/dev.mjs app:*` allow rule —
// env-prefixed commands like `FOO=1 node ...` don't match it and prompt). Mapped from project.config.
const dbgEnv = {};
for (const [flag, env] of Object.entries(cfg.debugFlags ?? {})) {
  if (flag.endsWith('=')) { const a = argv.find((x) => x.startsWith(flag)); if (a) dbgEnv[env] = a.slice(flag.length); }
  else if (argv.includes(flag)) dbgEnv[env] = '1';
}
// --hide=<ms> overrides the player chrome auto-hide timeout (default 60000 keeps it up for captures).
// Pass a small value (e.g. --hide=1500) to capture the chrome-HIDDEN / video-only player state.
const hideArg = argv.find((a) => a.startsWith('--hide='));
const chromeHideMs = hideArg ? hideArg.slice('--hide='.length) : '60000';

// PIDs of instances running OUR repo build only (ExecutablePath == the repo bin exe). The user may be
// running their own INSTALLED copy of the app at the same time — a name-based `taskkill /IM` would kill
// that too (it did, 2026-07-05). Path-matching guarantees we only ever touch the dev instance.
function devPids() {
  const procName = EXE_NAME.replace(/\.exe$/i, '');
  try {
    const out = execSync(
      `powershell -NoProfile -Command "(Get-Process ${procName} -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq '${EXE.replace(/'/g, "''")}' }).Id"`,
      { stdio: 'pipe' },
    ).toString().trim();
    return out ? out.split(/\r?\n/).map((s) => Number(s.trim())).filter(Boolean) : [];
  } catch { return []; }
}

function kill() {
  // Kill by PID (path-matched dev instances only) — NEVER `taskkill /IM <name>` (kills the user's own
  // installed instance too). /T also ends child processes (WebView2 helpers). We do NOT nuke dotnet.exe.
  const pids = devPids();
  for (const pid of pids) {
    try { execSync(`taskkill /F /PID ${pid} /T`, { stdio: 'ignore' }); } catch { /* already gone */ }
  }
  return pids;
}

function start() {
  const out = openSync(resolve(repoRoot, 'app-dev-stdout.txt'), 'a');
  const err = openSync(resolve(repoRoot, 'app-dev-stderr.txt'), 'a');
  const child = spawn(EXE, [], {
    detached: true,
    stdio: ['ignore', out, err],
    env: {
      ...process.env,
      ...cfg.devEnv, // dev-mode → loads the live Vite dev server
      // The app appends this to its own CoreWebView2EnvironmentOptions.AdditionalBrowserArguments in
      // dev mode (WebViewInitializer) so CDP attaches. (WebView2 only reads this env automatically when
      // the app does NOT set AdditionalBrowserArguments itself — this app does, hence the manual append.)
      WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${port}`,
      ...(cfg.chromeHideMsEnv ? { [cfg.chromeHideMsEnv]: chromeHideMs } : {}),
      ...dbgEnv, // verification-only debug flags (off by default)
    },
  });
  child.unref(); // survive this node process exiting
}

async function waitForCdp(timeoutSec = 30) {
  const deadline = Date.now() + timeoutSec * 1000;
  while (Date.now() < deadline) {
    try {
      const r = await fetch(`http://127.0.0.1:${port}/json/list`, { signal: AbortSignal.timeout(2000) });
      if (r.ok) return true;
    } catch { /* not up yet */ }
    await new Promise((r) => setTimeout(r, 1000));
  }
  return false;
}

// The Vite dev server the app loads in Development mode. Without it the webview shows a chrome-error
// (net::ERR_CONNECTION_REFUSED) — the #1 cause of "UI didn't start". Port comes from project.config
// (viteUrlMatch, kept in sync with vite.config.ts). Zero-dep background spawn + poll.
const VITE_PORT = Number(cfg.viteUrlMatch);

async function waitForVite(timeoutSec = 45) {
  const deadline = Date.now() + timeoutSec * 1000;
  while (Date.now() < deadline) {
    try {
      // MUST probe `localhost` (not 127.0.0.1): Vite 7 binds `localhost`, which resolves to IPv6 ::1 on
      // this box — 127.0.0.1 (IPv4) gets connection-refused, a false negative that spawns a duplicate
      // server ("port in use"). The app navigates to http://localhost:3517 too (WebViewInitializer).
      const r = await fetch(`http://localhost:${VITE_PORT}/`, { signal: AbortSignal.timeout(2000) });
      if (r.ok) return true;
    } catch { /* not up yet */ }
    await new Promise((r) => setTimeout(r, 1000));
  }
  return false;
}

function viteStart() {
  const out = openSync(resolve(repoRoot, 'vite-stdout.txt'), 'a');
  const err = openSync(resolve(repoRoot, 'vite-stderr.txt'), 'a');
  // Run Vite's JS entry via THIS node binary directly — no shell. A `shell:true` spawn of npm.cmd on
  // Windows silently dropped the piped FDs (empty logs, server never came up); node+vite.js is reliable.
  const viteJs = resolve(repoRoot, cfg.clientDir, 'node_modules/vite/bin/vite.js');
  const child = spawn(process.execPath, [viteJs], {
    cwd: resolve(repoRoot, cfg.clientDir),
    detached: true,
    stdio: ['ignore', out, err],
  });
  child.unref();
}

// Idempotent: returns true if Vite is (or comes) up. Only spawns a new server when none is listening.
async function ensureVite() {
  if (await waitForVite(2)) return true;
  console.log(`[${ms()}] starting Vite dev server on :${VITE_PORT}…`);
  viteStart();
  return waitForVite();
}

const ms = () => new Date().toISOString().slice(11, 19);

// Build the backend, printing ONLY error lines (so the whole build is one allow-listed `node` command —
// no `dotnet build ... | grep` compound that the permission matcher can't allow). Returns true on success.
function build() {
  try {
    execSync(`dotnet build ${resolve(repoRoot, cfg.csproj)} -c Debug -v q -nologo`, { cwd: repoRoot, stdio: 'pipe' });
    console.log(`[${ms()}] build OK`);
    return true;
  } catch (e) {
    const out = (e.stdout?.toString() || '') + (e.stderr?.toString() || '');
    const errs = out.split(/\r?\n/).filter((l) => /: error|error [A-Z]{2}\d|Error\(s\)/.test(l));
    console.log(`[${ms()}] BUILD FAILED:\n${errs.join('\n') || out.slice(-1500)}`);
    return false;
  }
}

if (action === 'kill') {
  const pids = kill();
  console.log(`[${ms()}] killed ${pids.length} dev instance(s) of ${EXE_NAME}${pids.length ? ` (pid ${pids.join(', ')})` : ''} — other instances untouched`);
} else if (action === 'reset-db') {
  // Clean slate for testing: kill the app (it locks the db) then delete app.db + WAL/SHM sidecars.
  // Dev db lives under the exe's data dir (IWorkspaceContext RootPath = {exeDir}/data, DatabasePath = app.db).
  kill();
  await new Promise((r) => setTimeout(r, 700));
  const dataDir = resolve(dirname(EXE), 'data');
  const removed = [];
  for (const f of ['app.db', 'app.db-wal', 'app.db-shm']) {
    const p = resolve(dataDir, f);
    if (existsSync(p)) { rmSync(p, { force: true }); removed.push(f); }
  }
  console.log(`[${ms()}] reset-db: removed [${removed.join(', ') || 'nothing'}] from ${dataDir}`);
} else if (action === 'wait') {
  console.log((await waitForCdp()) ? `[${ms()}] CDP up on :${port}` : `[${ms()}] CDP NOT up on :${port}`);
} else if (action === 'vite') {
  console.log((await ensureVite())
    ? `[${ms()}] vite dev server up on :${VITE_PORT}`
    : `[${ms()}] vite NOT up (check vite-stderr.txt)`);
} else if (action === 'build') {
  process.exit(build() ? 0 : 1);
} else if (action === 'tsc') {
  // Frontend typecheck as one allow-listed command (the authority over the vite-plugin-checker overlay).
  try {
    execSync('npx tsc --noEmit', { cwd: resolve(repoRoot, cfg.clientDir), stdio: 'pipe' });
    console.log(`[${ms()}] tsc OK`);
  } catch (e) {
    const out = (e.stdout?.toString() || '') + (e.stderr?.toString() || '');
    console.log(`[${ms()}] TSC ERRORS:\n${out.split(/\r?\n/).filter((l) => /error TS/.test(l)).slice(0, 40).join('\n') || out.slice(-1500)}`);
    process.exit(1);
  }
} else if (action === 'test') {
  // Vitest in the client dir as one allow-listed `node` call (no `cd` prefix). Optional path filter arg.
  const pattern = argv.slice(1).find((a) => !/^\d+$/.test(a)) || '';
  try {
    execSync(`npx vitest run ${pattern}`.trim(), { cwd: resolve(repoRoot, cfg.clientDir), stdio: 'inherit' });
  } catch { process.exit(1); }
} else if (action === 'start' || action === 'restart' || action === 'rebuild') {
  if (action === 'rebuild') {
    // kill → build → relaunch → wait, in ONE allow-listed command (the usual backend-change loop).
    kill(); await new Promise((r) => setTimeout(r, 600));
    if (!build()) process.exit(1);
  } else if (action === 'restart') { kill(); await new Promise((r) => setTimeout(r, 800)); }
  // Dev mode loads the live Vite server; without it the webview = chrome-error. Ensure it FIRST.
  if (!(await ensureVite())) console.log(`[${ms()}] WARNING: Vite not up — UI will show chrome-error (see vite-stderr.txt)`);
  start();
  const up = await waitForCdp();
  console.log(up ? `[${ms()}] app started; CDP up on :${port}` : `[${ms()}] app started; CDP NOT up (check app-dev-stderr.txt)`);
} else {
  console.error(`unknown action "${action}" — use kill|reset-db|build|tsc|test|start|restart|rebuild|wait`);
  process.exit(1);
}
