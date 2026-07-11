# Helper scripts live in the repo, never in /tmp — and the dev loop stays prompt-free

Any script, scratch program, or tool you write to develop/test this app — capture helpers, CDP
drivers, lifecycle commands, probes — goes **inside the repo** (`devtools/`), never in `/tmp` (or
`%TEMP%`). Adapted from a sibling project's conventions.

## Why
- **Allow-listing.** A repo-relative command (`node devtools/dev.mjs …`) is covered by one
  `.claude/settings.json` allow rule so it runs unattended. A `/tmp/<random>` path is unpredictable →
  prompts every time → the user must surface the console, which **covers the app** and breaks
  screen/window captures. In-repo scripts are what make the loop unattended.
- **Reuse + evolution.** In-repo tools are committed, discoverable, improvable next session; `/tmp`
  scratch is lost and rewritten every time.

## Rules
1. Helper scripts under `devtools/` (or the relevant module tooling dir) — real name + header comment
   explaining why they exist. Keep them **zero-dep** (Node 24 globals: `fetch`/`WebSocket`/`fs`).
2. Clean up any throwaway scratch you make. Build artifacts (`bin/`, `obj/`, `node_modules/`) and
   `devtools/screenshots/` are git-ignored; commit the **source**, not binaries/captures.

> **NEVER write agent scratch to `%TEMP%` / OS temp (user rule, repeated).** This covers not just
> scripts but **e2e test fixtures** too — a dummy fix-tool/`.bat`, a file to import, a sample archive,
> any throwaway you create to drive a test. Put them under `devtools/` (e.g. `devtools/fixtures/`,
> git-ignored) and delete them after. Production code already must use profile/global paths, not temp
> (see [use-project-paths.md](../knowledge/use-project-paths.md)); this rule is the **dev/test** counterpart.
> Also: any **test artifact you create IN the app** (a merged mod, an imported fix tool, a test mod)
> must be **deleted after the test** so the user's library is left exactly as it was.
3. Add an allow rule in `.claude/settings.json` for any new command so it stays prompt-free. The single
   `Bash(node devtools/dev.mjs:*)` rule already covers every tool routed through the dispatcher.

## Keep commands prompt-free (hard-won; these prompts break captures)
- **NEVER prefix a command with `cd <dir>;`.** The Bash tool's working dir is ALREADY the repo root. A
  `cd …; <anything>` trips a separate "changes directory → could run untrusted hooks" safety check that
  **overrides the allow-list and prompts** even for allow-listed `git`/`node`/`dotnet`. Run bare:
  `node devtools/dev.mjs app rebuild`, `git add …`, `dotnet build …`.
- **Fold shell steps into a `node devtools/*.mjs` action** rather than ad-hoc compounds. A
  `dotnet build | grep`, `cp`/`ls`/`stat`, or `sleep` compound can't be prefix-matched → prompts. That's
  why `app-dev.mjs` has `build`/`rebuild` and `drive-cdp.mjs` has `wait`.
- **Inspect code with the Grep / Read / Glob tools, NOT Bash `grep`/`sed`/`cat`/`ls`/`find`** — the Bash
  forms prompt and are slower. Reserve Bash for the allow-listed commands (`node devtools/*.mjs`,
  `dotnet build/test`, `git`, `npx tsc/vitest`).

## Self-enhance the toolkit
When a dev/test action recurs, is awkward, or prompts — turn it into a tool (add an action to the script
that owns the concern, or a new `scripts/<name>.mjs` / `devtools/<name>/` package; register it in
`dev.mjs`; document it in `devtools/README.md`). Prune superseded scripts. Treat tooling friction as a
bug in the toolkit and fix it there.
