#!/usr/bin/env node
// dev.mjs — THE universal entry for D3dxSkinManager's devtools. One command dispatches to every tool:
//
//   node devtools/dev.mjs <command> [...args]
//
//   app   <kill|build|tsc|test|start|restart|rebuild|wait> [port] [--flags]   lifecycle + checks
//   cdp   <open|key|eval|reload|nav|menu|probe|ipc|events|iplog|shot|grab|wait> [arg] [port]   drive + capture over CDP
//   shot  [label]                                                            occlusion-immune window capture (WGC)
//   input <click|rclick|move|drag> <x> <y> [x2 y2]                           native mouse input (PostMessage)
//   research <search|scrape> …                                              web research (puppeteer + stealth)
//   check [port]                                                             desktop health verdict
//   review [port]                                                            sweep every tab (regression)
//   crop  <png> <x> <y> <w> <h>                                             crop a capture
//
// stdout/stderr pass straight through. Allow-listed as `Bash(node devtools/dev.mjs:*)` → prompt-free.
// The toolkit is meant to self-enhance: add a tool → add a row to NODE below + devtools/README.md.
// Adapted from the SiblingApp devtools toolkit.

import { spawnSync } from 'node:child_process';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const NODE = {
  app: 'app-dev.mjs',
  cdp: 'drive-cdp.mjs',
  shot: 'shot-wgc.mjs',
  input: 'win-input.mjs',
  research: 'research.mjs',
  crop: 'crop.mjs',
  check: 'check-desktop.mjs',
  review: 'review-desktop.mjs',
};

const [cmd, ...rest] = process.argv.slice(2);

if (!cmd || cmd === 'help' || cmd === '--help' || cmd === '-h') {
  process.stdout.write(
    'D3dxSkinManager devtools — node devtools/dev.mjs <command> [...args]\n\n' +
      Object.keys(NODE).map((c) => `  ${c}`).join('\n') +
      '\n\nSee devtools/README.md for full usage.\n',
  );
  process.exit(cmd ? 0 : 1);
}

let r;
if (NODE[cmd]) r = spawnSync('node', [resolve(here, 'scripts', NODE[cmd]), ...rest], { stdio: 'inherit' });
else { process.stderr.write(`dev: unknown command "${cmd}" — run \`node devtools/dev.mjs help\`\n`); process.exit(2); }

process.exit(r.status ?? 0);
