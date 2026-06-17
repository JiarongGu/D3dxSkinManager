#!/usr/bin/env node
// shot-wgc.mjs — OCCLUSION-IMMUNE screenshot of the real desktop app via Windows.Graphics.Capture.
//
// Why this exists: shot-app.ps1 (ddagrab / DXGI desktop-duplication) grabs whatever is ON SCREEN, so
// it only works when D3dxSkinManager is foregrounded + unoccluded — but the agent must surface the console to
// grant permissions, which COVERS D3dxSkinManager and corrupts the grab. WGC window-capture pulls the target
// window's OWN composited frame regardless of z-order, so it works even while D3dxSkinManager is UNDER the
// console. It also captures the child LibVLC video surface (DWM composes child windows in).
//
// Usage:  node devtools/dev.mjs shot [label] [--proc D3dxSkinManager] [--hwnd 0x1234]
//   Builds the tiny devtools/wgc-shot tool on first use (cached after), then writes a timestamped PNG
//   to devtools/screenshots/<yyyyMMdd-HHmmss>-<label>.png and prints the path. Read that PNG to SEE the
//   real composited app (incl. native video). The top-level layered player CHROME is a separate window
//   — verify it via its own bin/**/data/logs/chrome-last.png dump (see desktop-app-testing.md).
// Single allow-listed command (Bash(node devtools/dev.mjs shot:*)) → unattended capture, no prompts.

import { spawnSync, execSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import cfg from '../project.config.mjs';
import { shotPath, finalizeShot } from './_capture-util.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const toolDir = resolve(repoRoot, 'devtools/wgc-shot');
const tfmDir = resolve(toolDir, 'bin/Release');

const argv = process.argv.slice(2);
const label = (argv.find((a) => !a.startsWith('--')) || 'app').replace(/[^a-z0-9._-]/gi, '_');
const proc = argv[argv.indexOf('--proc') + 1] && argv.includes('--proc') ? argv[argv.indexOf('--proc') + 1] : cfg.processName;
const hwnd = argv.includes('--hwnd') ? argv[argv.indexOf('--hwnd') + 1] : null;

function findExe() {
  if (!existsSync(tfmDir)) return null;
  // bin/Release/net10.0-windows10.0.xxxxx.0/wgc-shot.exe
  for (const d of readdirSync(tfmDir)) {
    const exe = join(tfmDir, d, 'wgc-shot.exe');
    if (existsSync(exe)) return exe;
  }
  return null;
}

let exe = findExe();
if (!exe) {
  console.log('[shot-wgc] building devtools/wgc-shot (first run)...');
  execSync('dotnet build -c Release -v q -nologo', { cwd: toolDir, stdio: 'inherit' });
  exe = findExe();
  if (!exe) { console.error('[shot-wgc] build produced no exe'); process.exit(1); }
}

const out = shotPath(label); // <prefix>-<ts>-<label>.png (centralized naming + prune)

const args = hwnd ? ['--hwnd', hwnd, '--out', out] : ['--proc', proc, '--out', out];
const r = spawnSync(exe, args, { stdio: 'inherit' });
// Downscale the capture under the agent's image-read limit (~2000px) + prune old shots, so an oversized
// image never enters the conversation context (it would re-error every turn). See screenshot-hygiene rule.
if ((r.status ?? 0) === 0 && existsSync(out)) finalizeShot(out);
process.exit(r.status ?? 0);
