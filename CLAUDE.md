# CLAUDE.md — Mandatory Rules

> Auto-loaded every session. Keep this file short — details belong in docs/.

---

## 1. Git Commits

**NEVER commit without explicit user approval.**
Always ask "Ready to commit?" and wait for a clear "yes".

---

## 2. Architecture (non-negotiable)

```
Backend  → ALL heavy operations, data processing, file I/O
Frontend → UI only, NO data processing
Facades  → Thin delegation only — no business logic, no events
Services → Business logic + event emission
```

**Module boundaries** — never access another module's repository directly.
Always call through that module's facade.

**Error handling** — throw `OperationException("ERROR_CODE", params)`.
Add message to BOTH `Languages/en.json` AND `Languages/cn.json`.

**Events** — services emit events (inject `IProfileEventBus`).
Facades never emit events.

**Frontend data** — use `undefined` for absent data, never `null`.
`null` is only for React render returns (`if (!data) return null`).

---

## 3. Testing (non-negotiable)

**After every bug fix or new feature, write tests.**

Before writing any test:
```
/doc-loader "write tests for <what you changed>" testing
```

Full rules, coverage matrix, pitfalls:
→ [docs/ai-assistant/TESTING_GUIDE.md](docs/ai-assistant/TESTING_GUIDE.md)

---

## 4. Work Style

**Skills → Agents → RAG → Manual** (in that order)

### MANDATORY GATE — Before ANY code generation

**NEVER** read files, run Glob/Grep, or launch Explore/Plan agents before completing both steps below.

**Step 1 — Load the entry point (every session, every task):**

```
Read docs/AI_GUIDE.md
```

`docs/AI_GUIDE.md` is the authoritative entry point. It contains the full mandatory rules, the complete skills table (18 skills with usage syntax), architecture patterns, and session workflow. Load it before generating any code.

**Step 2 — Load task-specific docs:**

```
/doc-loader "describe what you're doing" scope
```

Scope: `backend` | `frontend` | `ipc` | `testing` | `architecture`

doc-loader loads `docs/AI_GUIDE.md` + `docs/KEYWORDS_INDEX.md` + scope-specific docs and tells you which skill to use next. (If you already read AI_GUIDE.md in Step 1, doc-loader still adds the scope-specific docs.)

### Step 3 — ALWAYS run `/pattern-finder` before writing code

```
/pattern-finder PatternType Module
```

This finds existing patterns in the codebase to follow. **Do not skip this.**
Even for bug fixes — the fix should match existing patterns, not invent new ones.

### Step 4 — BLOCKING: Use code generation skills (not manual writing)

**NEVER manually write code that a skill can generate. This is a BLOCKING requirement.**

Before writing ANY new file, ask: "Does a skill exist for this?" If yes, run the skill FIRST,
then customize the output with business logic. Even for "unique" features — the individual
pieces (service, IPC, component, error) are standard patterns that skills handle.

| What you're building | Skill to run | STOP if you skip this |
|----------------------|-------------|----------------------|
| New backend service | `/backend-service Name Module Deps Methods` | DI, events, logging will be wrong |
| New IPC facade | `/backend-facade Name Module Services` | Routing pattern will be inconsistent |
| New frontend IPC service | `/ipc-service Name Module Methods` | Type safety will be missing |
| New React component | `/react-component Name type features` | BEM CSS, hooks pattern will be wrong |
| Add error + i18n | `/error-with-i18n CODE params "en msg" "cn msg"` | Error handling pattern will be inconsistent |
| Backend + frontend IPC pair | `/ipc-message-pair Module MessageType ...` | Backend/frontend will be out of sync |
| Batch SQL operation | `/batch-operation Module Op EntityType Params` | SQL pattern will be wrong |
| Register a service | `/service-registration Module Interface Impl Lifecycle` | DI registration will be incomplete |

**VIOLATION EXAMPLE (Import/Export feature 2026-04-12):** All 5 applicable skills were skipped.
Result: multiple UI polish rounds, inconsistent error handling, manual DI wiring bugs.
The "unique" feature still needed standard service + IPC + component + error + registration.

**Manual code is ONLY for:** unique business logic INSIDE a skill-generated structure,
or one-off fixes where no skill applies. If you're writing a new file by hand, STOP and
check the skills table first.

Full skill reference (with parameters and examples) → [docs/AI_GUIDE.md](docs/AI_GUIDE.md)

### Step 5 — Only after Steps 1–4: research or planning

- **Explore agent** — understand existing code (`Thoroughness: medium`)
- **Plan agent** — plan a feature (load `DESIGN_DECISIONS.md` in the prompt)

### Step 6 — After finishing

Write tests (section 3), build succeeds, ask before committing (section 1).

### Step 7 — MANDATORY: Evolve the system (before committing)

**DO NOT ask "Ready to commit?" until Step 7 is done.**

After any non-trivial feature or bug fix, run `/post-feature` to audit what changed.
This detects new components, IPC messages, store state, patterns, and triggers the right
`/doc-update-*` skills so future sessions have accurate internal knowledge.

**What counts as non-trivial:** New component, new hook, new IPC endpoint, new store field,
new drag-drop/interaction pattern, new CSS pattern, architecture decision, multi-file change.

**Skip ONLY for:** Single-line typo fix, CSS color tweak, config-only change.

If you skip Step 7, the docs/skills system drifts and future sessions lose context,
leading to duplicated work and re-discovered patterns. This is a persistent problem —
treat Step 7 with the same weight as testing.
