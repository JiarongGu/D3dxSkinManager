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

// Files matched case-insensitively by basename that must NOT appear in the manifest.
//
// The launcher `d3dxskinmanager.exe` IS listed (deliberately — do NOT exclude it). This is REQUIRED for
// the old→new topology migration: the OLD released launcher's apply deletes every file in `oldManifest`
// but not `newManifest`. In the old topology `d3dxskinmanager.exe` was the APP (listed). If the new
// manifest omitted it, the old launcher would delete the freshly-copied NEW launcher ("the launcher
// removed itself"). Listing it keeps it. The launcher still never self-updates — the new updater's
// robocopy `/XF` self-excludes the running launcher, so a staged newer launcher is simply never applied
// while running. Only the never-shipped legacy `d3dxskinmanager launcher.exe` and `manifest.json` are
// excluded.
const EXCLUDE_BASENAMES = new Set([
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
