#!/usr/bin/env node
// devtools/new-rule.mjs
//
// Scaffold a new rule for the md-based memory system so the "evolve the
// system" step can't forget the RULES_INDEX row — a rule with no index row is
// invisible to discovery (its body isn't auto-loaded; it's found only via the
// always-loaded index). See .claude/rules/RULES_INDEX.md "Loading model".
//
//   node devtools/dev.mjs knowledge new <kebab-name> [--core]   (preferred)
//   node devtools/new-rule.mjs <kebab-name> [--core]            (direct)
//
// Default target is .claude/knowledge/<name>.md (on-demand — the right home
// for a situational rule). --core puts it in .claude/rules/ (always-loaded);
// use ONLY for a genuinely universal-workflow rule needed on every task.
//
// It copies .claude/rules/TEMPLATE.md, titles it from the name, drops the
// template's "Usage notes" section, and appends a placeholder RULES_INDEX row.
// Then: write the body, fill in the row's "Applies When" / "Enforces", and run
// `node devtools/knowledge-check.mjs`.

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const argv = process.argv.slice(2);
const core = argv.includes('--core');
const name = argv.find((a) => !a.startsWith('--'));

const die = (m) => { console.error(`\x1b[31m${m}\x1b[0m`); process.exit(1); };

if (!name) die('usage: node devtools/new-rule.mjs <kebab-name> [--core]');
if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(name)) die(`'${name}' is not kebab-case (a-z0-9, hyphen-separated)`);

const dir = core ? '.claude/rules' : '.claude/knowledge';
const target = join(dir, `${name}.md`);
if (existsSync(target)) die(`${target} already exists`);

const TEMPLATE = '.claude/rules/TEMPLATE.md';
if (!existsSync(TEMPLATE)) die(`${TEMPLATE} not found`);

// title from kebab: foo-bar-baz -> "Foo Bar Baz"
const title = name.split('-').map((w) => w[0].toUpperCase() + w.slice(1)).join(' ');

let body = readFileSync(TEMPLATE, 'utf8');
body = body.replace(/\n---\n\n\*\*Usage notes[\s\S]*$/, '\n'); // drop the template's usage-notes tail
body = body.replace(/^# \{Rule Title[^}]*\}/m, `# ${title}`); // title the doc
writeFileSync(target, body);

// append a placeholder RULES_INDEX row (author fills Applies When / Enforces)
const INDEX = '.claude/rules/RULES_INDEX.md';
const link = core ? `${name}.md` : `../knowledge/${name}.md`;
const row = `| [${name}.md](${link}) | TODO: applies when | TODO: enforces |`;
let idx = readFileSync(INDEX, 'utf8');
if (idx.includes(`(${link})`)) {
  console.log(`(RULES_INDEX already has a row for ${name} — not duplicating)`);
} else if (!/\n\n## How to Use/.test(idx)) {
  console.log('\x1b[33mWARN\x1b[0m could not find the "## How to Use" anchor in RULES_INDEX — add the row manually:');
  console.log(`  ${row}`);
} else {
  idx = idx.replace(/\n\n## How to Use/, `\n${row}\n\n## How to Use`);
  writeFileSync(INDEX, idx);
}

console.log(`\x1b[32mcreated\x1b[0m ${target}`);
console.log(`\x1b[32mindexed\x1b[0m added a RULES_INDEX row (placeholder)`);
console.log('\nnext:');
console.log(`  1. write the rule body in ${target}`);
console.log('  2. fill in its RULES_INDEX row (Applies When + a one-line Enforces)');
console.log('  3. node devtools/dev.mjs knowledge check');
