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

### Step 3 — Use the right skill

| What you're building | Skill to run |
|----------------------|-------------|
| New backend service | `/backend-service Name Module Deps Methods` |
| New IPC facade | `/backend-facade Name Module Services` |
| New frontend IPC service | `/ipc-service Name Module Methods` |
| New React component | `/react-component Name type features` |
| Add error + i18n | `/error-with-i18n CODE params "en msg" "cn msg"` |
| Backend + frontend IPC pair | `/ipc-message-pair Module MessageType ...` |
| Batch SQL operation | `/batch-operation Module Op EntityType Params` |
| Register a service | `/service-registration Module Interface Impl Lifecycle` |
| Find existing patterns | `/pattern-finder PatternType Module` |

Full skill reference (with parameters and examples) → [docs/AI_GUIDE.md](docs/AI_GUIDE.md)

### Step 4 — Only after Steps 1–3: research or planning

- **Explore agent** — understand existing code (`Thoroughness: medium`)
- **Plan agent** — plan a feature (load `DESIGN_DECISIONS.md` in the prompt)

### Step 5 — After finishing

Write tests (section 3), build succeeds, ask before committing (section 1).

### Step 6 — Evolve the system

After any non-trivial feature, run `/post-feature` to audit what changed and update docs/skills.
This keeps the documentation system current so future sessions benefit from what was built.
Skip only for trivial fixes (typo, single-line CSS, config-only changes).
