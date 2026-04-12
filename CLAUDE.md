# CLAUDE.md — Mandatory Rules

> Auto-loaded every session. Keep this file short — details belong in docs/.

---

## 0. Per-Task Gate (BLOCKING — do this before ANY code exploration)

Follow the full 5-step protocol in `.claude/rules/skills-workflow.md`. Summary:

1. **Invoke ALL skills** from the system-reminder list via `Skill()` in parallel (first tool calls)
2. **Read EVERY doc** that doc-loader routes to — not just AI_GUIDE.md. Confirm in response text.
3. **Run ALL search commands** from pattern-finder
4. **Print a skill checklist** — list EVERY available skill with match/no-match (must be visible in response)
5. **Only then** explore code or write anything

**If you skip steps 2–4 you WILL generate non-conforming code.**
Never hand-write what a skill generates (service, facade, IPC, component, error, registration).
Skip this gate only when doing a direct continuation of the same task in the same scope.

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
Full rules → [docs/ai-assistant/TESTING_GUIDE.md](docs/ai-assistant/TESTING_GUIDE.md)

---

## 4. Work Style

**Skills → Agents → RAG → Manual** (in that order)

### Code-Gen Skills (use these, don't write by hand)

| Building | Skill |
|----------|-------|
| Backend service | `/backend-service Name Module Deps Methods` |
| IPC facade | `/backend-facade Name Module Services` |
| Frontend IPC service | `/ipc-service Name Module Methods` |
| React component | `/react-component Name type features` |
| Error + i18n | `/error-with-i18n CODE params "en msg" "cn msg"` |
| Backend + frontend IPC | `/ipc-message-pair Module MessageType ...` |
| Batch SQL operation | `/batch-operation Module Op EntityType Params` |
| DI registration | `/service-registration Module Interface Impl Lifecycle` |

Manual code is ONLY for unique business logic inside a skill-generated structure.

### Discovery Tools (mandatory first step — see Section 0)

- `/doc-loader "task" scope` — routes to relevant docs + identifies which code-gen skills to invoke
- `/pattern-finder PatternType Module` — gives Glob/Grep commands for existing patterns

### After Finishing

1. Write tests (section 3)
2. Build succeeds
3. Run `/post-feature` for non-trivial changes (new IPC, component, store field, multi-file)
4. **Evolve the system** — if you discovered a multi-file wiring chain (3+ files edited in sequence), create `.claude/rules/{pattern}.md` so the next session doesn't re-discover it. Update `docs/keywords/FRONTEND.md` or `BACKEND.md` with new extension points.
5. Ask user: "Ready to commit?"

---

## 5. Rules & Memory

- **Project rules** → `.claude/rules/*.md` (repo-committed, shared across sessions and users)
- **Global memory** → `~/.claude/projects/*/memory/` (personal/user-specific only)

Save workflow feedback, conventions, and corrections to `.claude/rules/`, NOT global memory.
Global memory is reserved for user-specific preferences (role, communication style).
