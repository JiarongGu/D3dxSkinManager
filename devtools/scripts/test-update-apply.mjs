#!/usr/bin/env node
// test-update-apply.mjs — end-to-end test of the C++ launcher's APPLY phase, for the libs/ topology.
//
//   node devtools/dev.mjs test-update-apply
//
// Runs the REAL launcher (built as D3dxSkinManager.exe) with `--apply-and-exit` against sandbox installs
// under devtools/, in TWO scenarios:
//
//   1. NEW self-update — install is the new topology (launcher = D3dxSkinManager.exe at root, runtime =
//      libs/D3dxSkinManager.App.exe). Staged payload updates the runtime + a data file, adds one, drops
//      one, and carries a newer D3dxSkinManager.exe launcher. Asserts the runtime/data overlay + removal,
//      and that the RUNNING launcher is NOT overwritten (robocopy /XF self-exclude).
//
//   2. MIGRATION (old→new) — install is the OLD topology (runtime = D3dxSkinManager.exe at root, launcher
//      = "D3dxSkinManager Launcher.exe"). Staged payload is the NEW topology (D3dxSkinManager.exe is now
//      the launcher; runtime moved to libs/D3dxSkinManager.App.exe). Running the OLD launcher must: copy
//      the new launcher over the old runtime's D3dxSkinManager.exe, add libs/, AND — the crux — NOT delete
//      D3dxSkinManager.exe via the manifest-diff removal (it flips from app→launcher across the manifests;
//      the removal guard on both launcher names prevents deleting the just-landed new launcher).
//
// Scratch lives under devtools/ and is deleted after (scripts-live-in-repo rule). Windows-only.

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync, readFileSync, existsSync, rmSync, copyFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, '..', '..');
// The launcher's build output is D3dxSkinManager.exe now (Launcher.vcxproj TargetName).
const launcherExe = join(repo, 'D3dxSkinManager.Launcher', 'bin', 'x64', 'Release', 'D3dxSkinManager.exe');
const sandboxRoot = join(repo, 'devtools', '.update-sandbox');

const sep = () => (process.platform === 'win32' ? '\\' : '/');
const sha = (s) => createHash('sha256').update(Buffer.from(s, 'utf8')).digest('hex');
const manifest = (version, files) => JSON.stringify({
  version,
  generatedAt: '',
  files: files.map(([path, content]) => ({ path, size: Buffer.byteLength(content), sha256: sha(content) })),
}, null, 2);

function write(base, rel, content) {
  const full = join(base, rel.replace(/\//g, sep()));
  mkdirSync(dirname(full), { recursive: true });
  writeFileSync(full, content);
}

function fail(msg) {
  console.error(`test-update-apply: FAIL — ${msg}`);
  try { rmSync(sandboxRoot, { recursive: true, force: true }); } catch {}
  process.exit(1);
}

if (process.platform !== 'win32') fail('Windows-only (launcher uses robocopy).');
if (!existsSync(launcherExe)) fail(`launcher not built: ${launcherExe} (build Launcher.vcxproj — its output is now D3dxSkinManager.exe)`);

// Run one scenario. `runningLauncherName` = the file name we run the launcher AS (its own name drives the
// robocopy /XF self-exclude). `setup(install)` writes the sandbox. `checks(read, exists)` returns
// [label, bool] assertions.
function scenario(title, runningLauncherName, setup, checks) {
  const install = join(sandboxRoot, title.replace(/\W+/g, '-'), 'install');
  rmSync(dirname(install), { recursive: true, force: true });
  mkdirSync(install, { recursive: true });

  setup(install);
  // Drop the REAL launcher in under the name it runs as (its self-name matters for /XF).
  copyFileSync(launcherExe, join(install, runningLauncherName));

  try {
    execFileSync(join(install, runningLauncherName), ['--apply-and-exit'], { timeout: 60000 });
  } catch (e) {
    fail(`[${title}] launcher --apply-and-exit failed: ${e.message}`);
  }

  const read = (rel) => readFileSync(join(install, rel.replace(/\//g, sep())), 'utf8');
  const exists = (rel) => existsSync(join(install, rel.replace(/\//g, sep())));
  const results = checks(read, exists);

  console.log(`\n[${title}]`);
  let failed = 0;
  for (const [name, ok] of results) {
    console.log(`  ${ok ? 'OK ' : 'XX '} ${name}`);
    if (!ok) failed++;
  }
  if (failed) fail(`[${title}] ${failed}/${results.length} assertions failed`);
  return results.length;
}

let total = 0;

// ── Scenario 1: NEW self-update ──────────────────────────────────────────────────────────────────────
// The launcher D3dxSkinManager.exe is LISTED in the manifest now (required for migration), so a staged
// newer launcher is present but must be skipped: robocopy /XF self-excludes the RUNNING launcher.
total += scenario('new self-update', 'D3dxSkinManager.exe',
  (install) => {
    // Installed (v2.4): launcher (listed), runtime in libs\ (A), data/en.json (X), data/old.json (removed).
    write(install, 'libs/D3dxSkinManager.App.exe', 'APP-A');
    write(install, 'data/en.json', 'EN-X');
    write(install, 'data/old.json', 'OLD');
    write(install, 'manifest.json', manifest('2.4', [
      ['D3dxSkinManager.exe', 'LAUNCHER'],
      ['libs/D3dxSkinManager.App.exe', 'APP-A'],
      ['data/en.json', 'EN-X'],
      ['data/old.json', 'OLD'],
    ]));
    // Staged (v2.5): runtime A2, en.json X2, new.json added, + a newer launcher (must be /XF-skipped).
    write(install, '.update/staged/libs/D3dxSkinManager.App.exe', 'APP-A2');
    write(install, '.update/staged/data/en.json', 'EN-X2');
    write(install, '.update/staged/data/new.json', 'NEW');
    write(install, '.update/staged/D3dxSkinManager.exe', 'NEW-LAUNCHER');
    write(install, '.update/staged/manifest.json', manifest('2.5', [
      ['D3dxSkinManager.exe', 'NEW-LAUNCHER'],
      ['libs/D3dxSkinManager.App.exe', 'APP-A2'],
      ['data/en.json', 'EN-X2'],
      ['data/new.json', 'NEW'],
    ]));
    write(install, '.update/ready.json', JSON.stringify({ version: '2.5' }));
  },
  (read, exists) => [
    ['runtime updated (libs/App.exe = A2)', exists('libs/D3dxSkinManager.App.exe') && read('libs/D3dxSkinManager.App.exe') === 'APP-A2'],
    ['en.json updated (X2)', read('data/en.json') === 'EN-X2'],
    ['new.json added', exists('data/new.json') && read('data/new.json') === 'NEW'],
    ['old.json removed', !exists('data/old.json')],
    // The running launcher must NOT be overwritten by the staged NEW-LAUNCHER (robocopy /XF self-exclude);
    // if it were, robocopy would fail on the locked running exe.
    ['running launcher NOT overwritten', exists('D3dxSkinManager.exe') && read('D3dxSkinManager.exe') !== 'NEW-LAUNCHER'],
    ['manifest refreshed to 2.5', JSON.parse(read('manifest.json')).version === '2.5'],
    ['staging cleared', !exists('.update')],
  ]);

// ── Scenario 2: MIGRATION (old topology → new topology) ───────────────────────────────────────────────
// The crux: an OLD install auto-updating. The staged v4.0 manifest LISTS D3dxSkinManager.exe (the new
// launcher), which is what keeps the old launcher's removal step from deleting the just-copied new
// launcher (D3dxSkinManager.exe was the APP in the old manifest → app→launcher role flip). Runs the OLD
// launcher name so /XF excludes IT (not the staged new D3dxSkinManager.exe, which therefore copies).
total += scenario('migration old-to-new', 'D3dxSkinManager Launcher.exe',
  (install) => {
    // Installed OLD topology (v3.6): D3dxSkinManager.exe IS the app (listed); old launcher runs the apply.
    write(install, 'D3dxSkinManager.exe', 'APP-A');
    write(install, 'data/en.json', 'EN-X');
    write(install, 'manifest.json', manifest('3.6', [
      ['D3dxSkinManager.exe', 'APP-A'],
      ['data/en.json', 'EN-X'],
    ]));
    // Staged NEW topology (v4.0): D3dxSkinManager.exe is now the LAUNCHER (LISTED); runtime → libs\.
    write(install, '.update/staged/D3dxSkinManager.exe', 'NEW-LAUNCHER');
    write(install, '.update/staged/libs/D3dxSkinManager.App.exe', 'APP-B');
    write(install, '.update/staged/data/en.json', 'EN-X2');
    write(install, '.update/staged/manifest.json', manifest('4.0', [
      ['D3dxSkinManager.exe', 'NEW-LAUNCHER'],
      ['libs/D3dxSkinManager.App.exe', 'APP-B'],
      ['data/en.json', 'EN-X2'],
    ]));
    write(install, '.update/ready.json', JSON.stringify({ version: '4.0' }));
  },
  (read, exists) => [
    // The crux: the new launcher lands at D3dxSkinManager.exe (copied — /XF only excluded the running OLD
    // launcher) AND is NOT deleted by the removal step (it is listed in the new manifest).
    ['new launcher landed (D3dxSkinManager.exe = NEW-LAUNCHER)', exists('D3dxSkinManager.exe') && read('D3dxSkinManager.exe') === 'NEW-LAUNCHER'],
    ['runtime added (libs/App.exe = B)', exists('libs/D3dxSkinManager.App.exe') && read('libs/D3dxSkinManager.App.exe') === 'APP-B'],
    ['en.json updated (X2)', read('data/en.json') === 'EN-X2'],
    // The apply leaves the old launcher; the NEW launcher removes it on its next NORMAL boot (RemoveLegacyLauncher).
    ['old launcher orphan left (swept by the new launcher on normal boot)', exists('D3dxSkinManager Launcher.exe')],
    ['manifest refreshed to 4.0', JSON.parse(read('manifest.json')).version === '4.0'],
    ['staging cleared', !exists('.update')],
  ]);

rmSync(sandboxRoot, { recursive: true, force: true });
console.log(`\ntest-update-apply: PASS — ${total} assertions across 2 scenarios, real launcher apply verified`);
