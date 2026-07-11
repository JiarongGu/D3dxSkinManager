#!/usr/bin/env node
// hooks.mjs — install/uninstall the repo's git hooks (currently the pre-commit rules-system gate in
// .githooks/pre-commit). Points git at the committed hooks via `core.hooksPath = .githooks` so the gate
// runs locally on commit — replacing the old knowledge-check CI workflow. Zero-dep.
//
//   node devtools/dev.mjs hooks <install|uninstall|status>

import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const HOOKS_DIR = '.githooks';

function git(args) {
  const r = spawnSync('git', args, { cwd: repo, encoding: 'utf8' });
  return { status: r.status ?? 1, out: (r.stdout || '').trim(), err: (r.stderr || '').trim() };
}

const sub = process.argv[2] || 'status';

if (sub === 'install') {
  if (!existsSync(resolve(repo, HOOKS_DIR, 'pre-commit'))) {
    process.stderr.write(`hooks: ${HOOKS_DIR}/pre-commit not found\n`);
    process.exit(1);
  }
  const r = git(['config', 'core.hooksPath', HOOKS_DIR]);
  if (r.status !== 0) {
    process.stderr.write(`hooks: failed to set core.hooksPath: ${r.err}\n`);
    process.exit(1);
  }
  process.stdout.write(`hooks: installed — core.hooksPath = ${HOOKS_DIR} (pre-commit rules gate active)\n`);
} else if (sub === 'uninstall') {
  git(['config', '--unset', 'core.hooksPath']);
  process.stdout.write('hooks: uninstalled — core.hooksPath cleared (back to default .git/hooks)\n');
} else {
  const r = git(['config', '--get', 'core.hooksPath']);
  const active = r.out === HOOKS_DIR;
  process.stdout.write(
    `hooks: core.hooksPath = ${r.out || '(unset, default .git/hooks)'} — rules gate ` +
      `${active ? 'ACTIVE' : 'INACTIVE (run: node devtools/dev.mjs hooks install)'}\n`,
  );
}
