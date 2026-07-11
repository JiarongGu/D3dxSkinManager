#!/usr/bin/env node
// build-manifest.mjs — generate the auto-update manifest for a release payload.
//
//   node devtools/dev.mjs manifest <dir> <version> [outFile]
//
// Walks <dir> recursively and emits a manifest.json listing every auto-updatable file with its
// relative path (forward-slash), byte size, and sha256. The launcher diffs a release's manifest
// against the locally-installed one to compute added / updated / removed files (see
// docs/LAUNCHER_ARCHITECTURE.md). The C++ launcher itself is EXCLUDED — it never auto-updates.
//
// Output is deterministic (files sorted by path) so two builds of identical content produce
// byte-identical manifests. Zero-dep (Node crypto/fs).

import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

// Files that are part of the install but must NEVER be auto-updated (the launcher updates everything
// else and restarts; it cannot replace itself while running). Matched case-insensitively by basename.
// The launcher is now the top-level `d3dxskinmanager.exe` (the runtime moved to
// `lib/D3dxSkinManager.App.exe`, which IS listed — it is the app). The legacy `d3dxskinmanager
// launcher.exe` stays excluded so a transitional payload never lists it either.
const EXCLUDE_BASENAMES = new Set([
  'd3dxskinmanager.exe',
  'd3dxskinmanager launcher.exe',
  'manifest.json',
]);

function walk(dir, base, out) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(full, base, out);
    } else if (entry.isFile()) {
      if (EXCLUDE_BASENAMES.has(entry.name.toLowerCase())) continue;
      const buf = readFileSync(full);
      out.push({
        path: relative(base, full).split(sep).join('/'),
        size: buf.length,
        sha256: createHash('sha256').update(buf).digest('hex'),
      });
    }
  }
}

const [dir, version, outFile] = process.argv.slice(2);

if (!dir || !version) {
  process.stderr.write('usage: node devtools/dev.mjs manifest <dir> <version> [outFile]\n');
  process.exit(1);
}

let stat;
try {
  stat = statSync(dir);
} catch {
  process.stderr.write(`manifest: directory not found: ${dir}\n`);
  process.exit(1);
}
if (!stat.isDirectory()) {
  process.stderr.write(`manifest: not a directory: ${dir}\n`);
  process.exit(1);
}

const files = [];
walk(dir, dir, files);
files.sort((a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0));

const manifest = {
  version,
  generatedAt: new Date().toISOString(),
  files,
};

const out = outFile || join(dir, 'manifest.json');
writeFileSync(out, JSON.stringify(manifest, null, 2) + '\n', 'utf8');

const totalBytes = files.reduce((n, f) => n + f.size, 0);
process.stdout.write(
  `manifest: v${version} — ${files.length} files, ${(totalBytes / 1048576).toFixed(2)} MB → ${out}\n`,
);
