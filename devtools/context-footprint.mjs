#!/usr/bin/env node
// devtools/context-footprint.mjs
//
// Reports the REPO-CONTROLLABLE part of every session's base context — the
// files the harness auto-loads as "project instructions": CLAUDE.md + the
// always-on .claude/rules/*.md core. (The rest of session cost — system
// prompt, tool defs, skill list, MCP — is fixed harness overhead this can't
// see or change.)
//
// Use it to keep the always-loaded core lean: it's the ONE budget a repo edit
// can move. .claude/knowledge/*.md are shown separately — they are on-demand
// (NOT in context until Read), so they don't count against the budget.
//
// Token estimate is bytes / CHARS_PER_TOKEN (~3.6 for this dense
// markdown/table content); treat it as an order-of-magnitude figure.
// Zero dependencies; run:  node devtools/context-footprint.mjs

import { readFileSync, readdirSync, existsSync, statSync } from 'node:fs';
import { join } from 'node:path';

const CHARS_PER_TOKEN = 3.6;
const BUDGET_BYTES = 64 * 1024; // soft ceiling for the always-loaded core

const C = { yel: '\x1b[33m', grn: '\x1b[32m', dim: '\x1b[2m', bold: '\x1b[1m', off: '\x1b[0m' };
const kb = (b) => (b / 1024).toFixed(1) + ' KB';
const tok = (b) => '~' + Math.round(b / CHARS_PER_TOKEN / 100) / 10 + 'K tok';
const size = (f) => (existsSync(f) ? statSync(f).size : 0);
const mdFiles = (d) => (existsSync(d) ? readdirSync(d).filter((f) => f.endsWith('.md')).map((f) => join(d, f)) : []);

// ---- always-loaded set: CLAUDE.md + .claude/rules/*.md ----
const loaded = ['CLAUDE.md', ...mdFiles('.claude/rules')];
const rows = loaded.map((f) => ({ f, b: size(f) })).sort((a, b) => b.b - a.b);
const loadedTotal = rows.reduce((s, r) => s + r.b, 0);

console.log(`\n${C.bold}Always-loaded (repo-controllable session base)${C.off}`);
for (const r of rows) {
  const bar = '#'.repeat(Math.round((r.b / rows[0].b) * 24));
  console.log(`  ${kb(r.b).padStart(9)}  ${C.dim}${bar}${C.off} ${r.f}`);
}
console.log(`  ${C.bold}${kb(loadedTotal).padStart(9)}${C.off}  TOTAL  =  ${tok(loadedTotal)}`);

// ---- on-demand knowledge (NOT loaded; shown for contrast) ----
const knowFiles = mdFiles('.claude/knowledge');
const knowTotal = knowFiles.reduce((s, f) => s + size(f), 0);
console.log(`\n${C.dim}On-demand .claude/knowledge/ (NOT auto-loaded): ${knowFiles.length} files, ${kb(knowTotal)} (${tok(knowTotal)})${C.off}`);
console.log(`${C.dim}If these were auto-loaded like before the split, the base would be ${kb(loadedTotal + knowTotal)} (${tok(loadedTotal + knowTotal)}).${C.off}`);

// ---- budget check ----
console.log('');
if (loadedTotal > BUDGET_BYTES) {
  console.log(`${C.yel}OVER BUDGET${C.off} — always-loaded ${kb(loadedTotal)} > ceiling ${kb(BUDGET_BYTES)}. Trim the core or move a rule to .claude/knowledge/.`);
  process.exit(1);
}
console.log(`${C.grn}within budget${C.off} — always-loaded ${kb(loadedTotal)} <= ceiling ${kb(BUDGET_BYTES)}.`);
