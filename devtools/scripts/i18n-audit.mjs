#!/usr/bin/env node
// i18n-audit.mjs — mechanical i18n completeness check (added 2026-07-10).
//
//   node devtools/dev.mjs i18n
//
// Checks, in order:
//   1. Both language files parse as JSON.
//   2. Key-set diff: keys present in one language but missing from the other.
//   3. Keys REFERENCED by code but missing from BOTH files:
//      - frontend: t('...') / t("...") literals in D3dxSkinManager.Client/src
//      - backend:  titleKey/detailKey string literals ("process.*") in D3dxSkinManager/Modules
//      - backend:  OperationException("CODE"...) codes → errors.CODE must exist
// Dynamic keys (template literals / concatenation) can't be checked mechanically and are skipped.
// Exit code 1 when anything is missing — usable as a gate.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const EN = join(repo, 'D3dxSkinManager', 'Languages', 'en.json');
const CN = join(repo, 'D3dxSkinManager', 'Languages', 'cn.json');

function loadLang(path) {
  const parsed = JSON.parse(readFileSync(path, 'utf8')); // throws on invalid JSON = check 1
  // File shape: { code, name, translations: { "a.b.c": "text", ... } } — the translation table is
  // FLAT dotted keys under `translations` (i18n.ts feeds it to i18next as-is).
  const table = parsed.translations ?? parsed;
  const keys = new Set();
  const walk = (obj, prefix) => {
    for (const [k, v] of Object.entries(obj)) {
      const full = prefix ? `${prefix}.${k}` : k;
      if (v && typeof v === 'object') walk(v, full);
      else keys.add(full);
    }
  };
  walk(table, '');
  return keys;
}

function* walkFiles(dir, exts) {
  for (const entry of readdirSync(dir)) {
    if (['node_modules', 'bin', 'obj', 'dist', '__tests__'].includes(entry)) continue;
    const p = join(dir, entry);
    const st = statSync(p);
    if (st.isDirectory()) yield* walkFiles(p, exts);
    else if (exts.some((e) => p.endsWith(e))) yield p;
  }
}

function collectReferencedKeys() {
  const refs = new Map(); // key → first file seen
  const add = (key, file) => { if (!refs.has(key)) refs.set(key, file); };

  // Frontend t('...') literals. Skips dynamic keys (`${...}`, concatenation).
  const tCall = /(?<![A-Za-z0-9_$])t\(\s*(['"])((?:(?!\1).)+?)\1/g;
  for (const f of walkFiles(join(repo, 'D3dxSkinManager.Client', 'src'), ['.ts', '.tsx'])) {
    const src = readFileSync(f, 'utf8');
    for (const m of src.matchAll(tCall)) {
      const key = m[2];
      if (/^[a-zA-Z0-9_.-]+$/.test(key) && key.includes('.')) add(key, f);
    }
  }

  // Backend localized process titles/stages + error codes.
  const procKey = /(?:titleKey|detailKey):\s*"((?:process|statusBar)\.[^"]+)"/g;
  const opEx = /OperationException\(\s*"([A-Z0-9_]+)"/g;
  for (const f of walkFiles(join(repo, 'D3dxSkinManager', 'Modules'), ['.cs'])) {
    const src = readFileSync(f, 'utf8');
    for (const m of src.matchAll(procKey)) add(m[1], f);
    for (const m of src.matchAll(opEx)) add(`errors.${m[1]}`, f);
  }
  return refs;
}

const en = loadLang(EN);
const cn = loadLang(CN);
console.log(`en.json: ${en.size} keys | cn.json: ${cn.size} keys`);

const enOnly = [...en].filter((k) => !cn.has(k));
const cnOnly = [...cn].filter((k) => !en.has(k));
if (enOnly.length) console.log(`\nMISSING FROM cn.json (${enOnly.length}):\n  ` + enOnly.join('\n  '));
if (cnOnly.length) console.log(`\nMISSING FROM en.json (${cnOnly.length}):\n  ` + cnOnly.join('\n  '));

const refs = collectReferencedKeys();
const missing = [...refs.entries()].filter(([k]) => !en.has(k) && !cn.has(k));
if (missing.length) {
  console.log(`\nREFERENCED BY CODE, IN NEITHER LANGUAGE (${missing.length}):`);
  for (const [k, f] of missing) console.log(`  ${k}  ← ${f.replace(repo + '\\', '')}`);
}

const bad = enOnly.length + cnOnly.length + missing.length;
console.log(bad === 0 ? '\ni18n audit: CLEAN' : `\ni18n audit: ${bad} issue(s)`);
process.exit(bad === 0 ? 0 : 1);
