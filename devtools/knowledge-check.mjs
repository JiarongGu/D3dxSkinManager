#!/usr/bin/env node
// devtools/knowledge-check.mjs
//
// Health check for the md-based rules/knowledge memory system (see
// .claude/rules/RULES_INDEX.md "Loading model"). Run it after moving,
// adding, or renaming any rule/knowledge file — or in CI — to catch the
// failure modes the split introduced:
//
//   [1] index completeness  — every .claude/knowledge/*.md + core rule has a
//                             RULES_INDEX row (an un-indexed knowledge rule is
//                             INVISIBLE to discovery, since its body isn't
//                             auto-loaded), and every row link resolves.
//   [2] link resolution     — every relative *.md link inside .claude/rules +
//                             .claude/knowledge points at a file that exists.
//   [3] stale-ref guard     — nothing anywhere still references a MOVED rule as
//                             `.claude/rules/<name>` (regression guard for the
//                             .claude/rules -> .claude/knowledge move).
//   [4] core-set drift      — .claude/rules holds ONLY the intended always-on
//                             core; a situational rule dropped there silently
//                             re-inflates every session's base context.
//
// Exit code is non-zero if any check FAILs. WARNs don't fail the run.
// Zero dependencies; run:  node devtools/knowledge-check.mjs

import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { execSync } from 'node:child_process';
import { join, dirname, resolve, relative } from 'node:path';

const ROOT = process.cwd();
const RULES_DIR = '.claude/rules';
const KNOW_DIR = '.claude/knowledge';
const INDEX = join(RULES_DIR, 'RULES_INDEX.md');

// The always-loaded core. Workflow rules carry a bare-link RULES_INDEX row;
// the two meta files (index + template) do not.
const CORE_WORKFLOW = [
  'skills-workflow', 'no-global-memory', 'scripts-live-in-repo',
  'risky-change-tests-first',
];
const CORE_META = ['RULES_INDEX', 'TEMPLATE'];
const CORE_ALL = [...CORE_WORKFLOW, ...CORE_META];

const C = { red: '\x1b[31m', yel: '\x1b[33m', grn: '\x1b[32m', dim: '\x1b[2m', off: '\x1b[0m' };
let fails = 0, warns = 0;
const fail = (m) => { console.log(`  ${C.red}FAIL${C.off} ${m}`); fails++; };
const warn = (m) => { console.log(`  ${C.yel}WARN${C.off} ${m}`); warns++; };
const ok = (m) => console.log(`  ${C.grn}ok${C.off}   ${m}`);
const head = (n, t) => console.log(`\n${C.dim}[${n}]${C.off} ${t}`);

const baseNames = (dir) => existsSync(dir)
  ? readdirSync(dir).filter((f) => f.endsWith('.md')).map((f) => f.replace(/\.md$/, ''))
  : [];

const knowledge = baseNames(KNOW_DIR);
const rules = baseNames(RULES_DIR);

if (!existsSync(INDEX)) { console.error(`RULES_INDEX not found at ${INDEX}`); process.exit(2); }
const indexText = readFileSync(INDEX, 'utf8');

// ---------------------------------------------------------------- [1] index
head(1, 'RULES_INDEX completeness + row links');
// table-row links only: lines like `| [name.md](target) | ...`
const rowLinks = [...indexText.matchAll(/^\|\s*\[[^\]]+\]\(([^)]+)\)/gm)].map((m) => m[1]);
const rowBases = rowLinks.map((l) => l.replace(/^\.\.\/knowledge\//, '').replace(/\.md$/, ''));
for (const k of knowledge) {
  if (!rowBases.includes(k)) fail(`knowledge rule '${k}' has NO row in RULES_INDEX — invisible to discovery`);
}
for (const c of CORE_WORKFLOW) {
  if (!rowBases.includes(c)) fail(`core rule '${c}' has NO row in RULES_INDEX`);
}
for (const l of rowLinks) {
  // row links are written relative to .claude/rules/
  if (!existsSync(join(RULES_DIR, l))) fail(`RULES_INDEX row link does not resolve: ${l}`);
}
// a row pointing at a file that no longer exists anywhere
const knownBases = new Set([...knowledge, ...rules]);
for (const b of rowBases) {
  if (!knownBases.has(b)) fail(`RULES_INDEX row references '${b}' but no such rule file exists`);
}
if (fails === 0) ok(`${rowLinks.length} rows — every knowledge + core rule indexed, all links resolve`);

// ------------------------------------------------------- [2] link resolution
head(2, 'relative *.md link resolution (rules + knowledge)');
let linkBad = 0;
const linkRe = /\]\(([^)#]+\.md)(?:#[^)]*)?\)/g;
for (const dir of [RULES_DIR, KNOW_DIR]) {
  for (const b of baseNames(dir)) {
    const file = join(dir, `${b}.md`);
    const text = readFileSync(file, 'utf8');
    for (const m of text.matchAll(linkRe)) {
      const tgt = m[1];
      if (/^https?:/.test(tgt)) continue;
      const abs = resolve(dirname(file), tgt);
      // skip links that point outside the repo (sibling repos)
      if (relative(ROOT, abs).startsWith('..')) continue;
      if (!existsSync(abs)) { fail(`${file} -> broken link ${tgt}`); linkBad++; }
    }
  }
}
if (linkBad === 0) ok('every in-repo relative .md link resolves');

// ------------------------------------------------------ [3] stale-ref guard
head(3, 'no MOVED rule referenced as `.claude/rules/<name>` anywhere');
const tracked = execSync('git ls-files', { cwd: ROOT, encoding: 'utf8' }).split('\n').filter(Boolean);
const movedRe = new RegExp(`\\.claude/rules/(${knowledge.join('|')})(?![\\w-])`);
// CLAUDE.md + settings.local.json are HUMAN-OWNED (the human fixes CLAUDE's
// inline refs); docs/changelogs/ is archived history — none are ours to rewrite.
const SKIP = ['CLAUDE.md', '.claude/settings.local.json'];
const SKIP_PREFIX = ['docs/changelogs/'];
let stale = 0;
for (const f of tracked) {
  if (SKIP.includes(f)) continue;
  if (SKIP_PREFIX.some((p) => f.startsWith(p))) continue;
  if (!/\.(md|mdx|ts|tsx|js|jsx|mjs|cjs|json|scss|css|html|py|cs|ya?ml|txt)$/.test(f)) continue;
  let text; try { text = readFileSync(join(ROOT, f), 'utf8'); } catch { continue; }
  const lines = text.split('\n');
  lines.forEach((ln, i) => {
    if (movedRe.test(ln)) { fail(`${f}:${i + 1} still points at .claude/rules/ for a moved rule`); stale++; }
  });
}
if (stale === 0) ok('no stale `.claude/rules/<moved>` references');

// ------------------------------------------------------ [4] core-set drift
head(4, 'always-loaded core is exactly the intended set');
const extra = rules.filter((r) => !CORE_ALL.includes(r));
const missing = CORE_ALL.filter((c) => !rules.includes(c));
for (const e of extra) warn(`'.claude/rules/${e}.md' is NOT a known core rule — situational rules belong in .claude/knowledge/ (this re-inflates every session)`);
for (const m of missing) fail(`expected core rule '.claude/rules/${m}.md' is missing`);
if (extra.length === 0 && missing.length === 0) ok(`${rules.length} core files, exactly as expected`);

// ----------------------------------------------------------------- summary
console.log(`\n${fails ? C.red : C.grn}${fails} FAIL${C.off}, ${warns ? C.yel : ''}${warns} WARN${C.off}`);
process.exit(fails ? 1 : 0);
