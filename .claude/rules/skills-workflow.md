# Skills Workflow — Lean Execution Protocol

This is a 5-step SEQUENTIAL protocol. Each step MUST complete before the next begins. Skipping or reordering steps produces non-conforming code.

---

## Step 1: Invoke CORE skills (FIRST tool calls of every task)

Invoke these **4 core skills** via `Skill()` in one parallel batch — before ANY code exploration, Agent spawning, Grep, Glob, or Read:

| Core Skill | Arguments | Purpose |
|------------|-----------|---------|
| `/doc-loader` | `"task description" scope` | Routes to relevant docs |
| `/skill-loader` | `"task description"` | Routes to relevant code-gen skills |
| `/pattern-finder` | `PatternType Module` | Finds existing code patterns |
| `/caveman` | Token-optimized communication |

**Then immediately invoke every skill that `skill-loader` returns in its INVOKE list.** These are the code-gen templates that must be in context before writing code.

**Why lean instead of all:** Loading all 20+ skills wastes ~30-50K tokens on templates that don't apply. The skill-loader routes to only what's needed — same safety, much less context bloat.

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

## Step 4: Print skill match summary (BLOCKING — must be visible in response)

Print the INVOKE / SKIP lists from skill-loader's output. This serves as a visible checklist confirming which templates were loaded.

**This list must appear in your response text.** If it's missing, you skipped this step.

---

## Step 5: NOW begin code exploration and implementation

Only after steps 1–4 are complete may you:
- Read source files
- Spawn agents
- Write or edit code

---

## Common failure modes

1. **Skips skill-loader and only invokes doc-loader** — Step 1 requires all 4 core skills. skill-loader determines which code-gen skills to load.
2. **Calls doc-loader but doesn't read the docs it routes to** — Step 2 exists to prevent this
3. **Reads AI_GUIDE.md but skips scope-specific docs** (FRONTEND.md, BACKEND.md, etc.) — Read ALL listed docs
4. **Never prints skill match summary** — Step 4 requires visible output
5. **Jumps straight to Grep/Read after invoking core skills** — Steps 2–4 must happen first
6. **Mentions a skill name in text but doesn't call the Skill tool** — "Invoke" means `Skill()` tool call, not text
7. **Uses global memory for project rules** — Workflow corrections go in `.claude/rules/`, NEVER in `~/.claude/projects/*/memory/`
8. **Ignores skill-loader INVOKE list** — If skill-loader says INVOKE, you MUST call `Skill()` for those skills
9. **Skips skills on follow-up requests within the same session** — When the user asks for a NEW sub-task (e.g., "now fix the cleanup tool"), re-invoke skills if the scope changed. "Direct continuation" means the SAME files/scope, not just the same conversation.
10. **Hand-writes code that a skill generates** — If `/react-component`, `/ipc-service`, `/error-with-i18n`, etc. match the task, use them. Common violation: building new shared components, IPC methods, or error handling by hand instead of invoking the relevant skill.
11. **Ignores `.claude/rules/*.md` during implementation** — Rules contain hard-won patterns from past sessions (enum serialization, UI design rules, wiring chains). They override generic knowledge. ALWAYS scan rule filenames against the current task.

## Skill matching beyond coding tasks

On every user message, scan available skills. If any skill's trigger matches the request (e.g., `/caveman` for "be brief"), invoke it IMMEDIATELY before any other response. This applies to communication, audit, and discovery skills equally.

## When to skip this gate

- **ONLY skip** when ALL of these are true:
  1. Same files/scope as the previous request (not just same conversation)
  2. Templates from the previous skill-loader are still relevant
  3. No new component, service, or IPC method is being created

- Pure conversation (questions, explanations, no code changes)
- Single-line CSS/config tweaks with no new patterns

## When to RE-INVOKE (even mid-conversation)

**If the user's follow-up changes scope, RE-INVOKE skill-loader.** Examples:
- "now fix the cleanup tool" — different module, re-invoke
- "create a shared component" — new component, `/react-component` skill applies
- "add i18n keys" — `/error-with-i18n` skill may apply
- "polish the UI" — `/pattern-finder` needed to find existing patterns first

**The cost of invoking skill-loader is ~2K tokens. The cost of hand-writing what a skill generates is bugs + multiple correction iterations.**
