#!/usr/bin/env node
/**
 * downscale.mjs — shrink a PNG so the agent's image-read tool accepts it.
 *
 * Why: WGC window captures are ~2226px wide; the image-read API rejects images
 * over ~2000px. This produces a downscaled copy (default max width 1300px) next
 * to the source as `small-<name>.png` using GDI+ (System.Drawing) via PowerShell —
 * no extra npm dependency, no /tmp scratch (see scripts-live-in-repo.md).
 *
 * Usage:
 *   node devtools/downscale.mjs <path-to.png> [maxWidth]
 *   node devtools/downscale.mjs --latest [maxWidth]   # newest png in screenshots/
 */
import { spawnSync } from 'node:child_process';
import { readdirSync, statSync, existsSync } from 'node:fs';
import { dirname, join, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const SHOTS = join(dirname(fileURLToPath(import.meta.url)), 'screenshots');

function latestShot() {
  const pngs = readdirSync(SHOTS)
    .filter((f) => f.endsWith('.png') && !f.startsWith('small-'))
    .map((f) => ({ f, m: statSync(join(SHOTS, f)).mtimeMs }))
    .sort((a, b) => b.m - a.m);
  if (!pngs.length) throw new Error('no screenshots found');
  return join(SHOTS, pngs[0].f);
}

const args = process.argv.slice(2);
let src = args[0];
let maxW = 1300;
if (src === '--latest') {
  src = latestShot();
  if (args[1]) maxW = parseInt(args[1], 10);
} else if (args[1]) {
  maxW = parseInt(args[1], 10);
}
if (!src || !existsSync(src)) {
  console.error(`downscale: file not found: ${src}`);
  process.exit(1);
}
const out = join(dirname(src), `small-${basename(src).replace(/^small-/, '')}`);

const ps = `
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Image]::FromFile('${src.replace(/'/g, "''")}')
$maxW = ${maxW}
if ($src.Width -le $maxW) { $w = $src.Width; $h = $src.Height } else { $w = $maxW; $h = [int]($src.Height * $maxW / $src.Width) }
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($src, 0, 0, $w, $h)
$bmp.Save('${out.replace(/'/g, "''")}', [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose(); $src.Dispose()
Write-Output ('downscale: ' + $w + 'x' + $h + ' -> ${out.replace(/\\/g, '/').replace(/'/g, "''")}')
`;
const r = spawnSync('powershell', ['-NoProfile', '-NonInteractive', '-Command', ps], { encoding: 'utf8' });
process.stdout.write(r.stdout || '');
if (r.status !== 0) { process.stderr.write(r.stderr || ''); process.exit(r.status || 1); }
console.log(out);
