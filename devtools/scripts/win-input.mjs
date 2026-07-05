#!/usr/bin/env node
// win-input.mjs — background NATIVE MOUSE input to the real D3dxSkinManager window, so the agent can self-verify
// native surfaces the DOM/CDP can't reach (the LibVLC overlay + the enhance compositor's Stop/Pause pills,
// the wipe-divider drag, the right-click enhance menu). Technique from MaaNTE/MaaFramework: PostMessage to
// the target HWND (no real cursor move, no focus steal, occlusion-immune) + a WM_ACTIVATE wake.
//
// Usage:  node devtools/dev.mjs input <click|rclick|move|drag> <x> <y> [x2 y2] [--proc D3dxSkinManager] [--hwnd 0x..]
//   x,y are FRACTIONS (0..1) of the top-level window CLIENT area (resolution-independent). Builds the tiny
//   devtools/win-input tool on first use (cached). Pair with `shot`/`grab` to capture the result.
// Allow-listed via `Bash(node devtools/dev.mjs input:*)` → prompt-free.

import { spawnSync, execSync } from 'node:child_process';
import { existsSync, readdirSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import cfg from '../project.config.mjs';
import { devMainWindowHwnd } from './_dev-window.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const toolDir = resolve(repoRoot, 'devtools/win-input');
const binDir = resolve(toolDir, 'bin/Release');

function findExe() {
  if (!existsSync(binDir)) return null;
  for (const d of readdirSync(binDir)) {
    const exe = join(binDir, d, 'win-input.exe');
    if (existsSync(exe)) return exe;
  }
  return null;
}

let exe = findExe();
if (!exe) {
  console.log('[win-input] building devtools/win-input (first run)...');
  execSync('dotnet build -c Release -v q -nologo', { cwd: toolDir, stdio: 'inherit' });
  exe = findExe();
  if (!exe) { console.error('[win-input] build produced no exe'); process.exit(1); }
}

const argv = process.argv.slice(2);
// Target the DEV instance's window (path-matched HWND) — a bare process-name match can send real
// input to the user's own installed instance running alongside (see _dev-window.mjs).
if (!argv.includes('--proc') && !argv.includes('--hwnd')) {
  const hwnd = devMainWindowHwnd();
  if (hwnd) argv.push('--hwnd', hwnd);
  else argv.push('--proc', cfg.processName);
}
const r = spawnSync(exe, argv, { stdio: 'inherit' });
process.exit(r.status ?? 0);
