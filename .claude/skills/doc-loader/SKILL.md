---
name: doc-loader
description: Load relevant documentation based on current task using smart document selection
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

1. **Analyzes task keywords** to understand what you need
2. **Selects relevant docs** from docs/ directory
3. **Loads docs in priority order** (most relevant first)
4. **Provides summary** of key patterns found

## Document Selection Logic

### Backend Tasks
Keywords: service, repository, facade, database, migration, entity

**Loads**:
1. `docs/core/DESIGN_DECISIONS.md` - Architecture constraints
2. `docs/ai-assistant/WORKFLOWS.md` - Backend patterns section
3. `docs/keywords/BACKEND.md` - Backend reference (if exists)
4. Relevant module architecture docs

### Frontend Tasks
Keywords: component, react, hook, context, state, ui, css

**Loads**:
1. `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md` - React best practices
2. `docs/keywords/FRONTEND.md` - Frontend reference (if exists)
3. `docs/ai-assistant/WORKFLOWS.md` - Frontend patterns section

### IPC Tasks
Keywords: ipc, message, event, facade, communication

**Loads**:
1. `docs/ai-assistant/WORKFLOWS.md` - IPC integration section
2. `docs/core/DESIGN_DECISIONS.md` - IPC architecture
3. Both backend and frontend patterns

### Testing Tasks
Keywords: test, mock, assert, verify, unit, integration

**Loads**:
1. `docs/ai-assistant/TESTING_GUIDE.md` - Complete testing guide
2. `docs/ai-assistant/WORKFLOWS.md` - Testing section
3. Relevant module testing examples

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
2. Related pattern docs based on error context

## Task Keyword Mapping

| Task Contains | Primary Doc | Secondary Docs |
|---------------|-------------|----------------|
| "service" | WORKFLOWS.md (Backend Service) | DESIGN_DECISIONS.md |
| "component" | REACT_CLOSURE_PATTERNS.md | WORKFLOWS.md (Frontend) |
| "ipc", "message" | WORKFLOWS.md (IPC) | DESIGN_DECISIONS.md |
| "test" | TESTING_GUIDE.md | WORKFLOWS.md (Testing) |
| "error", "i18n" | WORKFLOWS.md (Error Handling) | Languages/*.json |
| "event" | WORKFLOWS.md (Events) | Event handler examples |
| "cache" | WORKFLOWS.md (Caching) | IMemoryCache examples |
| "database", "migration" | WORKFLOWS.md (Migration) | Migration files |
| "facade" | WORKFLOWS.md (Facade) | DESIGN_DECISIONS.md |
| "batch" | WORKFLOWS.md (Batch) | SQL patterns |

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

Loads:
- DESIGN_DECISIONS.md (service architecture)
- WORKFLOWS.md (Backend Service pattern)
- Mod module examples

Suggests:
- `/backend-service` skill
- `/error-with-i18n` for validation errors

### Example 2: Frontend Component
```
/doc-loader "build a mod details panel component with AG Grid" frontend
```

Loads:
- REACT_CLOSURE_PATTERNS.md (hooks, closure patterns)
- WORKFLOWS.md (React Component pattern)
- AG Grid usage examples from Batch Edit feature

Suggests:
- `/react-component` skill

### Example 3: IPC Integration
```
/doc-loader "add IPC endpoint for batch delete operation" ipc
```

Loads:
- WORKFLOWS.md (IPC Message Integration + Batch Operations)
- DESIGN_DECISIONS.md (IPC architecture)
- Existing batch operation examples

Suggests:
- `/ipc-message-pair` skill
- `/batch-operation` skill (if available)

### Example 4: Testing
```
/doc-loader "write tests for ModLifecycleService" testing
```

Loads:
- TESTING_GUIDE.md (complete guide)
- WORKFLOWS.md (Testing patterns)
- ModLifecycleService test examples (if exist)

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
- v1.0 (2026-04-11): Initial RAG skill

**How to update this skill**:
1. Add new keyword mappings as new patterns emerge
2. Update document paths if docs reorganize
3. Add new scope types if needed (e.g., "devops", "deployment")
4. Refine selection logic based on usage patterns
5. Add new skills to suggestion logic as they're created
