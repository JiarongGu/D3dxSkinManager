# CLAUDE.md — Mandatory Rules

> Auto-loaded every session. Keep this file short — details belong in docs/.

---

## 0. Per-Task Gate (BLOCKING — do this before ANY code exploration)

For every coding task, your **first two actions** must be:

1. **Invoke `/doc-loader`** with the task description and scope — it routes you to the right docs and tells you which code-gen skill to use
2. **Invoke `/pattern-finder`** with the pattern type — it gives you concrete Grep/Glob commands to find how similar code already exists

Only after these complete: read code, explore, or write anything.

**Never hand-write what a skill generates** (service, facade, IPC, component, error, registration).
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

- `/doc-loader "task" scope` — routes to relevant docs, suggests which code-gen skill to use
- `/pattern-finder PatternType Module` — gives Glob/Grep commands for existing patterns

### After Finishing

1. Write tests (section 3)
2. Build succeeds
3. Run `/post-feature` for non-trivial changes (new IPC, component, store field, multi-file)
4. Ask user: "Ready to commit?"
