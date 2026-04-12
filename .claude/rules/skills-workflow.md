# Skills Workflow — Strict Execution Protocol

This is a 5-step SEQUENTIAL protocol. Each step MUST complete before the next begins. Skipping or reordering steps produces non-conforming code.

---

## Step 1: Invoke ALL skills (FIRST tool calls of every task)

> **CRITICAL — THIS IS THE #1 FAILURE MODE. READ CAREFULLY.**
>
> You WILL be tempted to invoke only "relevant" skills (e.g., just `doc-loader` + `pattern-finder`). **DO NOT DO THIS.** Invoke EVERY skill. No exceptions. No judgment calls about relevance. The word "ALL" means ALL.

Invoke EVERY skill from the system-reminder's available skills list via `Skill()` tool — **all in one parallel batch** before ANY code exploration, Agent spawning, Grep, Glob, or Read.

**How to determine which skills to invoke:**
1. Read the system-reminder's "available skills" section at the start of the conversation
2. Exclude ONLY runtime-only skills that don't load templates (`loop`, `schedule`)
3. Count the remaining skills — you should have 20+ Skill() calls
4. Invoke ALL of them via `Skill()` tool calls in one parallel batch

**Self-check before proceeding:** Count your `Skill()` calls. If you have fewer than 20, you skipped skills. Go back and add them.

Pass task-appropriate arguments to discovery skills (`doc-loader`, `pattern-finder`). Other skills need no arguments unless the task warrants them.

**Why:** Loading all skills upfront in one batch ensures templates are in context before any code is written. This prevents non-conforming code and eliminates extra round trips.

---

## Step 2: Read EVERY routed doc (BLOCKING — do not skip)

doc-loader outputs a list of docs to read. **Read ALL of them** using the Read tool — not just AI_GUIDE.md.

After reading, write a brief summary line per doc confirming what you read. Example:
> Read AI_GUIDE.md (skills table, BEM rules), FRONTEND.md (IPC patterns, store conventions)

**If you did not read every listed doc, STOP and read them now.** Do not proceed to step 3.

---

## Step 3: Run pattern-finder search commands

pattern-finder outputs Glob/Grep commands. **Run ALL of them.** These show existing code patterns you must follow.

---

## Step 4: List ALL skills with match/no-match (BLOCKING — must be visible in response)

Print a checklist of EVERY available skill from the system-reminder. For each:
- State the skill name
- State match or no-match
- If match: state what it will generate

Example:
> - `/backend-service` — no match (not creating a new service)
> - `/react-component` — no match (modifying existing component)
> - `/error-with-i18n` — MATCH (new error for export validation)

**This list must appear in your response text.** If it's missing, you skipped this step.

---

## Step 5: NOW begin code exploration and implementation

Only after steps 1–4 are complete may you:
- Read source files
- Spawn agents
- Write or edit code

---

## Common failure modes (things the model actually does wrong)

1. **🚨 #1 FAILURE: Only invokes doc-loader + pattern-finder, not all skills** — Step 1 requires ALL skills (20+) in one parallel batch. "I'll only invoke the relevant ones" is WRONG. Invoke ALL of them. Every. Single. One.
2. **Calls doc-loader but doesn't read the docs it routes to** — Step 2 exists to prevent this
3. **Reads AI_GUIDE.md but skips scope-specific docs** (FRONTEND.md, BACKEND.md, etc.) — Read ALL listed docs
4. **Never lists skills or states match/no-match** — Step 4 requires visible output
5. **Jumps straight to Grep/Read after invoking skills** — Steps 2–4 must happen first
6. **Mentions a skill name in text but doesn't call the Skill tool** — "Invoke" means `Skill()` tool call, not text
7. **Uses global memory for project rules** — Workflow corrections go in `.claude/rules/`, NEVER in `~/.claude/projects/*/memory/`

## Skill matching beyond coding tasks

On every user message, scan available skills. If any skill's trigger matches the request (e.g., `/caveman` for "be brief"), invoke it IMMEDIATELY before any other response. This applies to communication, audit, and discovery skills equally.
