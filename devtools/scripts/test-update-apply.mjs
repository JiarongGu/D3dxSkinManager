#!/usr/bin/env node
// test-update-apply.mjs — end-to-end test of the C++ launcher's APPLY phase.
//
//   node devtools/dev.mjs test-update-apply
//
// Builds a sandbox "install" dir (manifest v2.4 + files + the real launcher exe) and a staged update
// (.update/staged with manifest v2.5: one file changed, one added, one dropped + ready.json), then runs
// the REAL launcher with `--apply-and-exit` (applies + exits, no app launch). Asserts the overlay,
// addition, removal, manifest refresh, and staging cleanup all happened. Scratch lives under devtools/
// and is deleted after (scripts-live-in-repo rule). Windows-only (robocopy/SHFileOperation).

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync, readFileSync, existsSync, rmSync, copyFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, '..', '..');
const launcherExe = join(repo, 'D3dxSkinManager.Launcher', 'bin', 'x64', 'Release', 'D3dxSkinManager Launcher.exe');
const sandbox = join(repo, 'devtools', '.update-sandbox');
const install = join(sandbox, 'install');
const staged = join(install, '.update', 'staged');

const sha = (s) => createHash('sha256').update(Buffer.from(s, 'utf8')).digest('hex');
const manifest = (version, files) => JSON.stringify({
  version,
  generatedAt: '',
  files: files.map(([path, content]) => ({ path, size: Buffer.byteLength(content), sha256: sha(content) })),
}, null, 2);

function write(base, rel, content) {
  const full = join(base, rel.replace(/\//g, require_sep()));
  mkdirSync(dirname(full), { recursive: true });
  writeFileSync(full, content);
}
function require_sep() { return process.platform === 'win32' ? '\\' : '/'; }

function fail(msg) {
  console.error(`test-update-apply: FAIL — ${msg}`);
  try { rmSync(sandbox, { recursive: true, force: true }); } catch {}
  process.exit(1);
}

if (process.platform !== 'win32') fail('Windows-only (launcher uses robocopy).');
if (!existsSync(launcherExe)) fail(`launcher not built: ${launcherExe} (build the Launcher.vcxproj first)`);

// Fresh sandbox.
rmSync(sandbox, { recursive: true, force: true });
mkdirSync(install, { recursive: true });

// --- installed (v2.4): app exe (A), data/en.json (X), data/old.json (OLD = will be removed) ---
write(install, 'D3dxSkinManager.exe', 'APP-A');
write(install, 'data/en.json', 'EN-X');
write(install, 'data/old.json', 'OLD');
write(install, 'manifest.json', manifest('2.4', [
  ['D3dxSkinManager.exe', 'APP-A'],
  ['data/en.json', 'EN-X'],
  ['data/old.json', 'OLD'],
]));
// The real launcher copied in (apply excludes it from the overlay).
copyFileSync(launcherExe, join(install, 'D3dxSkinManager Launcher.exe'));

// --- staged (v2.5): app exe (A2 changed), data/en.json (X2 changed), data/new.json (NEW added) ---
write(staged, 'D3dxSkinManager.exe', 'APP-A2');
write(staged, 'data/en.json', 'EN-X2');
write(staged, 'data/new.json', 'NEW');
write(staged, 'manifest.json', manifest('2.5', [
  ['D3dxSkinManager.exe', 'APP-A2'],
  ['data/en.json', 'EN-X2'],
  ['data/new.json', 'NEW'],
]));
write(install, '.update/ready.json', JSON.stringify({ version: '2.5' }));

// --- run the REAL launcher apply ---
try {
  execFileSync(join(install, 'D3dxSkinManager Launcher.exe'), ['--apply-and-exit'], { timeout: 60000 });
} catch (e) {
  fail(`launcher --apply-and-exit failed: ${e.message}`);
}

// --- assertions ---
const read = (rel) => readFileSync(join(install, rel.replace(/\//g, require_sep())), 'utf8');
const checks = [];
const expect = (name, cond) => checks.push([name, cond]);

expect('app exe updated (A2)', existsSync(join(install, 'D3dxSkinManager.exe')) && read('D3dxSkinManager.exe') === 'APP-A2');
expect('en.json updated (X2)', read('data/en.json') === 'EN-X2');
expect('new.json added', existsSync(join(install, 'data', 'new.json')) && read('data/new.json') === 'NEW');
expect('old.json removed', !existsSync(join(install, 'data', 'old.json')));
expect('manifest refreshed to 2.5', JSON.parse(read('manifest.json')).version === '2.5');
expect('staging cleared', !existsSync(join(install, '.update')));

const failed = checks.filter(([, ok]) => !ok);
for (const [name, ok] of checks) console.log(`  ${ok ? 'OK ' : 'XX '} ${name}`);

rmSync(sandbox, { recursive: true, force: true });

if (failed.length) fail(`${failed.length}/${checks.length} assertions failed`);
console.log(`test-update-apply: PASS — ${checks.length} assertions, real launcher apply verified`);
