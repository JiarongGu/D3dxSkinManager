# CLAUDE.md — Mandatory Rules

> Auto-loaded every session. Keep this file short — details belong in docs/.

---

## 0. Per-Task Gate

Before writing code for any task:

1. **Check the skills table** in `docs/AI_GUIDE.md` — if a code-gen skill applies, use it
2. **Find existing patterns** — grep the codebase for how similar code is already done
3. **Never hand-write what a skill generates** (service, facade, IPC, component, error, registration)

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

### Discovery Tools (use when needed, not mandatory on every task)

- `/doc-loader "task" scope` — routes to relevant docs
- `/pattern-finder PatternType Module` — gives Glob/Grep commands for patterns

### After Finishing

1. Write tests (section 3)
2. Build succeeds
3. Run `/post-feature` for non-trivial changes (new IPC, component, store field, multi-file)
4. Ask user: "Ready to commit?"
