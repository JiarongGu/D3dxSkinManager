---
name: doc-loader
description: Use at the start of any task to find and load the right project docs automatically. Analyzes task keywords to select relevant docs from KEYWORDS_INDEX.
disable-model-invocation: false
---

# Documentation Loader (RAG System)

Intelligently load relevant documentation for a coding task without overloading context.

**Purpose**: Replace manual "Read docs/X.md" with smart document selection that loads only what's needed.

## Arguments

**Format**: `/doc-loader <Task> <Scope>`

**Example**:
```
/doc-loader "implementing mod validation service" backend
```

**Parameters**:
- `Task` - Brief description of what you're trying to do (quoted)
- `Scope` - Area of focus: `backend`, `frontend`, `ipc`, `testing`, `architecture`, `all`

## What This Skill Does

Based on your task description and scope, this skill:

1. **Always loads `docs/AI_GUIDE.md` first** — entry point for skills, workflow, and architecture
2. **Always loads `docs/KEYWORDS_INDEX.md`** — routing hub to find code and docs fast
3. **Selects additional docs** based on task keywords and scope
4. **Provides summary** of key patterns and which skill to use next

## Loading Order (Always)

**Step 1 — Mandatory entry point (every task):**
1. `docs/AI_GUIDE.md` — skills table, workflow patterns, architecture rules
2. `docs/KEYWORDS_INDEX.md` — routing hub: finds components, services, files fast

**Step 2 — Scope-specific docs (based on scope argument):**

### Backend Tasks
Keywords: service, repository, facade, database, migration, entity

**Loads**:
1. `docs/core/DESIGN_DECISIONS.md` - Architecture constraints
2. `docs/keywords/BACKEND.md` - Backend reference

### Frontend Tasks
Keywords: component, react, hook, context, state, ui, css

**Loads**:
1. `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md` - React best practices (closures, useStableRef)
2. `docs/keywords/FRONTEND.md` - Frontend component/hook reference

### IPC Tasks
Keywords: ipc, message, event, facade, communication

**Loads**:
1. `docs/core/DESIGN_DECISIONS.md` - IPC architecture constraints
2. `docs/keywords/BACKEND.md` - Backend facade reference
3. `docs/keywords/FRONTEND.md` - Frontend service reference

### Testing Tasks
Keywords: test, mock, assert, verify, unit, integration

**Loads**:
1. `docs/ai-assistant/TESTING_GUIDE.md` - Complete testing guide

### Architecture Tasks
Keywords: architecture, design, decision, pattern, structure

**Loads**:
1. `docs/core/DESIGN_DECISIONS.md` - All architecture decisions
2. `docs/architecture/CURRENT_ARCHITECTURE.md` - System overview
3. `docs/architecture/MODULE_ARCHITECTURE.md` - Module structure

### Troubleshooting
Keywords: error, bug, fix, issue, problem, not working

**Loads**:
1. `docs/ai-assistant/TROUBLESHOOTING.md` - Common issues

## Task Keyword Mapping

| Task Contains | Additional Doc | Why |
|---------------|----------------|-----|
| "service" | `docs/core/DESIGN_DECISIONS.md` | Service architecture rules |
| "component" | `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md` | React hook patterns |
| "ipc", "message" | `docs/core/DESIGN_DECISIONS.md` | IPC architecture |
| "test" | `docs/ai-assistant/TESTING_GUIDE.md` | Testing patterns |
| "error", "i18n" | `docs/core/DESIGN_DECISIONS.md` | Error handling rules |
| "event" | `docs/core/DESIGN_DECISIONS.md` | Event emission rules |
| "cache" | `docs/core/ADVANCED_PATTERNS.md` | Caching patterns |
| "database", "migration" | `docs/architecture/DATABASE_MIGRATION_ARCHITECTURE.md` | Migration system |
| "facade" | `docs/core/DESIGN_DECISIONS.md` | Facade rules |
| "batch" | `docs/keywords/BACKEND.md` | Batch SQL patterns |

## Output Format

```markdown
## Documents Loaded for: "{Task}"

### Primary Documents (High Priority)
1. ✅ docs/core/DESIGN_DECISIONS.md
   - Relevant sections: [list sections found]
   - Key constraints: [list key rules]

2. ✅ docs/ai-assistant/WORKFLOWS.md
   - Relevant patterns: [list pattern names]
   - Code examples: [reference line numbers]

### Secondary Documents (Context)
3. docs/keywords/BACKEND.md
   - Quick reference for [specific topics]

### Key Patterns Found

**Pattern: Backend Service**
- Location: WORKFLOWS.md:150-200
- Requirements: DI + Events + Error Handling
- Example: ModLifecycleService

**Pattern: Error Handling**
- Location: WORKFLOWS.md:400-450
- Requirements: OperationException + i18n (en + cn)
- Example: MOD_DELETE_FAILED

### Relevant Skills

Based on this task, consider using:
- `/backend-service` - For generating service structure
- `/error-with-i18n` - For error handling
- `/service-registration` - For DI registration

### Next Steps

1. Review loaded patterns above
2. Use suggested skills for code generation
3. Implement unique business logic manually
```

## Smart Loading Rules

**Rule 1: Load Minimal Docs**
- Don't load entire files - extract relevant sections only
- Summarize patterns, don't repeat full documentation
- Point to line numbers for reference, don't copy code

**Rule 2: Prioritize by Relevance**
- Task keywords determine which docs load first
- Architecture docs loaded for all non-trivial tasks
- Pattern docs loaded based on scope

**Rule 3: Suggest Skills**
- If task matches a skill, suggest using it
- Show which skills apply to current task
- Don't regenerate what skills already handle

**Rule 4: Context-Aware**
- If "mod" mentioned → Load Mod module patterns
- If "profile" mentioned → Load Profile module patterns
- If specific module → Prioritize that module's docs

## Example Usages

### Example 1: Backend Service
```
/doc-loader "create a texture validation service for the Mod module" backend
```

Always loads:
- `docs/AI_GUIDE.md` (entry point — skills table, workflow)
- `docs/KEYWORDS_INDEX.md` (routing hub)

Then also loads:
- `docs/core/DESIGN_DECISIONS.md` (service architecture rules)
- `docs/keywords/BACKEND.md` (Mod module patterns)

Suggests:
- `/backend-service` skill
- `/error-with-i18n` for validation errors

### Example 2: Frontend Component
```
/doc-loader "build a mod details panel component with AG Grid" frontend
```

Always loads:
- `docs/AI_GUIDE.md` (entry point — skills table, workflow)
- `docs/KEYWORDS_INDEX.md` (routing hub)

Then also loads:
- `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md` (hooks, closure patterns)
- `docs/keywords/FRONTEND.md` (component/hook reference)

Suggests:
- `/react-component` skill

### Example 3: IPC Integration
```
/doc-loader "add IPC endpoint for batch delete operation" ipc
```

Always loads:
- `docs/AI_GUIDE.md` (entry point — skills table, workflow)
- `docs/KEYWORDS_INDEX.md` (routing hub)

Then also loads:
- `docs/core/DESIGN_DECISIONS.md` (IPC architecture rules)
- `docs/keywords/BACKEND.md` + `docs/keywords/FRONTEND.md`

Suggests:
- `/ipc-message-pair` skill
- `/batch-operation` skill (if available)

### Example 4: Testing
```
/doc-loader "write tests for ModLifecycleService" testing
```

Always loads:
- `docs/AI_GUIDE.md` (entry point — skills table, workflow)
- `docs/KEYWORDS_INDEX.md` (routing hub)

Then also loads:
- `docs/ai-assistant/TESTING_GUIDE.md` (complete testing guide)

Suggests:
- Review InMemoryDatabaseTestBase pattern
- Check migration files for schema

## Integration with Explore/Plan Agents

**When to use doc-loader vs agents**:

| Situation | Use | Why |
|-----------|-----|-----|
| **Know what to build** | `/doc-loader` | Faster, just need patterns |
| **Exploring codebase** | Explore agent | Need to find existing code |
| **Planning feature** | Plan agent | Need architecture verification + plan |
| **Quick reference** | `/doc-loader` | Just need pattern reminder |

**Workflow**:
```
1. Plan agent → Creates implementation plan
2. /doc-loader → Loads patterns for implementation
3. Skills → Generate code from patterns
4. Manual → Unique business logic
```

## Important Rules

- ✅ Load only relevant sections, not entire files
- ✅ Summarize patterns, don't copy full text
- ✅ Suggest applicable skills
- ✅ Provide line number references for deep dives
- ✅ Adapt to task keywords intelligently
- ❌ Don't load docs that don't match task
- ❌ Don't overwhelm with full file contents
- ❌ Don't suggest skills for tasks that need custom code

## Evolution Note

**Version History**:
- v1.1 (2026-04-12): Always load AI_GUIDE.md + KEYWORDS_INDEX.md first; removed broken WORKFLOWS.md reference
- v1.0 (2026-04-11): Initial RAG skill

**How to update this skill**:
1. Add new keyword mappings as new patterns emerge
2. Update document paths if docs reorganize
3. Add new scope types if needed (e.g., "devops", "deployment")
4. Refine selection logic based on usage patterns
5. Add new skills to suggestion logic as they're created
