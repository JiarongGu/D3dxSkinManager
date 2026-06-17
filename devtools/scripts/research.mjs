#!/usr/bin/env node
// research.mjs — wrapper for the self-contained devtools/research puppeteer tool, so web research is
// ONE allow-listed `node devtools/dev.mjs research <cmd> …` call (no `cd`, auto-installs on first use).
//   node devtools/dev.mjs research search "<query>" [--max N]
//   node devtools/dev.mjs research scrape <url> [--selector css] [--json]
// stdout = JSON (parse it), stderr = logs. See devtools/research/README.md.

import { existsSync } from 'node:fs';
import { execSync, spawnSync } from 'node:child_process';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const dir = resolve(dirname(fileURLToPath(import.meta.url)), '..', 'research');
const argv = process.argv.slice(2);
const cmd = argv[0];
if (!['search', 'scrape'].includes(cmd)) {
  console.error('usage: node devtools/dev.mjs research <search|scrape> …');
  process.exit(2);
}
if (!existsSync(resolve(dir, 'node_modules'))) {
  console.error('[research] first run — installing deps (downloads Chromium ~280MB)…');
  execSync('npm install', { cwd: dir, stdio: 'inherit' });
}
const r = spawnSync('npx', ['tsx', `src/${cmd}.ts`, ...argv.slice(1)], { cwd: dir, stdio: 'inherit', shell: true });
process.exit(r.status ?? 0);
