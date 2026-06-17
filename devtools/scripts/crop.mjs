#!/usr/bin/env node
// crop.mjs — crop/scale a screenshot for detailed inspection, in ONE allow-listed call (no ad-hoc
// `ls -t | head` + `ffmpeg` compound that prompts). Crops the LATEST screenshot by default.
//
//   node devtools/dev.mjs crop "<W>x<H>+<X>+<Y>" [--scale <W>] [--src <file|latest>] [--label <name>]
//   node devtools/dev.mjs crop full [--scale 1600]        # whole image, just scaled down to read
//
// Output → devtools/screenshots/<ts>-<label>.png (printed). Read that PNG. Needs ffmpeg (allow-listed).

import { spawnSync } from 'node:child_process';
import { readdirSync, statSync, existsSync } from 'node:fs';
import { resolve, dirname, join, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';

const shots = resolve(dirname(fileURLToPath(import.meta.url)), '..', 'screenshots');
const argv = process.argv.slice(2);
const region = argv[0] || 'full';
const opt = (n, d) => { const i = argv.indexOf(n); return i >= 0 ? argv[i + 1] : d; };
const scale = Number(opt('--scale', '1600'));
const label = opt('--label', 'crop');
let src = opt('--src', 'latest');

if (!isAbsolute(src) && !existsSync(resolve(shots, src))) {
  // 'latest' or a substring (e.g. "audit-library") → newest matching PNG in screenshots/.
  const needle = src === 'latest' ? '' : src;
  const pngs = readdirSync(shots)
    .filter((f) => f.endsWith('.png') && f.includes(needle))
    .map((f) => join(shots, f))
    .sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs);
  if (!pngs.length) { console.error(`crop: no screenshot matching "${src}"`); process.exit(2); }
  src = pngs[0];
} else if (!isAbsolute(src)) {
  src = resolve(shots, src);
}
if (!existsSync(src)) { console.error(`crop: source not found: ${src}`); process.exit(2); }

// region "WxH+X+Y" → ffmpeg crop=W:H:X:Y; "full" → no crop.
const filters = [];
const m = region.match(/^(\d+)x(\d+)\+(\d+)\+(\d+)$/);
if (m) filters.push(`crop=${m[1]}:${m[2]}:${m[3]}:${m[4]}`);
else if (region !== 'full') { console.error('crop: region must be "WxH+X+Y" or "full"'); process.exit(2); }
if (scale > 0) filters.push(`scale=${scale}:-1`);

const d = new Date(); const p = (n) => String(n).padStart(2, '0');
const ts = `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}`;
const out = join(shots, `${ts}-${label}.png`);
const r = spawnSync('ffmpeg', ['-y', '-loglevel', 'error', '-i', src, '-vf', filters.join(','), out], { stdio: 'inherit' });
if (r.status === 0) console.log(`crop: ${out}`);
process.exit(r.status ?? 0);
