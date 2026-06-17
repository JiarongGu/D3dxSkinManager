// Shared helper for the desktop-checker tools: all captures go into devtools/screenshots/ as
// <prefix>-<YYYYMMDD-HHMMSS>-<label>.png (prefix = project.config.mjs `shotPrefix`) so runs are easy
// to trace + diff over time AND captures from this project are namespaced (a shared screenshots dir or
// a copied toolkit won't collide / cross-prune). Pruning is scoped to this prefix for the same reason.
import { mkdirSync, readdirSync, statSync, rmSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import cfg from '../project.config.mjs';

const DIR = dirname(fileURLToPath(import.meta.url));
export const SHOTS_DIR = resolve(DIR, '..', 'screenshots');

// Project-specific filename prefix (e.g. "d3dx-"). Empty if unset.
const PREFIX = cfg.shotPrefix ? `${cfg.shotPrefix}-` : '';

// The agent's image-read tool REJECTS images wider/taller than ~2000px, and a rejected image then
// lingers in the conversation context and re-errors every turn. WGC window captures are ~2226px wide,
// so EVERY shot is downscaled in place to <= MAX_DIM and the folder is pruned to the most recent KEEP
// files — captures are throwaway scratch (scripts-live-in-repo.md), never committed.
const MAX_DIM = 1600;
const KEEP = 8;

/** Downscale a PNG IN PLACE so neither dimension exceeds MAX_DIM (GDI+ via PowerShell — no npm dep).
 * No-op if already small / on failure (best effort). Keeps shots always agent-readable. */
export function downscaleInPlace(file, maxDim = MAX_DIM) {
  const ps = `
$ErrorActionPreference='Stop'; Add-Type -AssemblyName System.Drawing
$img=[System.Drawing.Image]::FromFile('${file.replace(/'/g, "''")}')
$w=$img.Width; $h=$img.Height; $m=[Math]::Max($w,$h)
if($m -le ${maxDim}){ $img.Dispose(); exit 0 }
$s=${maxDim}/$m; $nw=[int]($w*$s); $nh=[int]($h*$s)
$bmp=New-Object System.Drawing.Bitmap $nw,$nh
$g=[System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($img,0,0,$nw,$nh); $img.Dispose()
$bmp.Save('${file.replace(/'/g, "''")}',[System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()`;
  try { spawnSync('powershell', ['-NoProfile', '-Command', ps], { stdio: 'ignore' }); } catch { /* best effort */ }
}

/** Keep only the newest `keep` PNGs (for THIS project's prefix) in the screenshots dir; delete older
 *  ones (throwaway scratch). Prefix-scoped so a shared/reused screenshots dir isn't cross-pruned. */
export function pruneShots(keep = KEEP) {
  try {
    const pngs = readdirSync(SHOTS_DIR)
      .filter((f) => f.endsWith('.png') && (!PREFIX || f.startsWith(PREFIX)))
      .map((f) => ({ f, m: statSync(join(SHOTS_DIR, f)).mtimeMs }))
      .sort((a, b) => b.m - a.m);
    for (const { f } of pngs.slice(keep)) { try { rmSync(join(SHOTS_DIR, f)); } catch { /* ignore */ } }
  } catch { /* dir may not exist yet */ }
}

/** Post-process a freshly-saved capture: downscale it under the read limit + prune old shots. Call this
 * after every capture so an oversized image never enters the agent's context. */
export function finalizeShot(file, { maxDim = MAX_DIM, keep = KEEP } = {}) {
  downscaleInPlace(file, maxDim);
  pruneShots(keep);
}

export function stamp() {
  const d = new Date();
  const p = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}`;
}

/** Absolute path under devtools/screenshots/ as <prefix>-<timestamp>-<label>.png (dir auto-created). */
export function shotPath(label, ts = stamp()) {
  mkdirSync(SHOTS_DIR, { recursive: true });
  const safe = String(label).replace(/[^a-z0-9_-]+/gi, '-');
  return join(SHOTS_DIR, `${PREFIX}${ts}-${safe}.png`);
}
