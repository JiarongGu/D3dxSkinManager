# D3dxSkinManager Skills System

**Version:** 1.1
**Last Updated:** 2026-04-12

This directory contains Claude Code skills that enforce consistent code generation patterns across the D3dxSkinManager project.

> **Quick access**: The most commonly used skills are listed in `CLAUDE.md` section 4 (auto-loaded every session). Load this file when you need full usage syntax, parameters, or examples for a specific skill.

## What Are Skills?

Skills are reusable code generation templates that:
- ✅ Enforce project patterns perfectly (never forget DI, events, i18n)
- ✅ Generate complete, working code from simple commands
- ✅ Evolve with your project (update patterns in one place)
- ✅ Are version-controlled (everyone uses same patterns)
- ✅ Work with documentation (skills reference docs/WORKFLOWS.md)

## Available Skills (18 Total)

### 📦 Core Code Generation (6 skills)

| Skill | Usage | What It Generates |
|-------|-------|-------------------|
| **backend-service** | `/backend-service ServiceName Module Dependencies Methods` | C# service + interface + DI + events |
| **backend-facade** | `/backend-facade FacadeName Module Services` | Thin IPC facade (no logic, just delegation) |
| **ipc-service** | `/ipc-service ServiceName Module Methods` | TypeScript IPC service + singleton export |
| **error-with-i18n** | `/error-with-i18n CODE params "en msg" "cn msg"` | OperationException + en.json + cn.json |
| **react-component** | `/react-component Name Type Features` | React component + CSS + proper hooks |
| **event-handler** | `/event-handler HandlerName Module SourceEvents Target` | Event consolidation handler |

### 🔗 Integration & Registration (4 skills)

| Skill | Usage | What It Generates |
|-------|-------|-------------------|
| **service-registration** | `/service-registration Module Interface Implementation Lifecycle` | Adds service registration to Module extensions |
| **ipc-message-pair** | `/ipc-message-pair Module MessageType ...` | Backend facade handler + Frontend service method (paired) |
| **batch-operation** | `/batch-operation Module Operation EntityType Parameters` | SQL batch + Facade handler + Frontend method (complete triple) |
| **file-watcher** | `/file-watcher WatcherName Module DirectoryPath NotifyFilters Events` | FileSystemWatcher service with event emission |

### 📚 RAG System (2 skills)

| Skill | Usage | What It Does |
|-------|-------|--------------|
| **doc-loader** | `/doc-loader "task description" scope` | Loads relevant documentation based on task |
| **pattern-finder** | `/pattern-finder PatternType Module?` | Finds existing code patterns in codebase |

### 📝 Documentation Maintenance (6 skills)

| Skill | Usage | What It Does |
|-------|-------|--------------|
| **doc-update-guide** | `/doc-update-guide DocumentName ChangeType Details` | Updates guide docs (AI_GUIDE.md) with versioning |
| **doc-update-reference** | `/doc-update-reference DocumentName EntryType Details` | Updates reference docs (REFERENCE.md, KEYWORDS_INDEX.md) with new paths/constants |
| **doc-update-technical** | `/doc-update-technical DocumentName UpdateType Details` | Updates technical docs (ADVANCED_PATTERNS.md, DESIGN_DECISIONS.md) with patterns/decisions |
| **doc-monitor** | `/doc-monitor CheckType Scope` | Monitors docs for redundancy, broken links, consistency issues |
| **doc-cleanup** | `/doc-cleanup Operation Target Details` | Cleans up redundant docs, removes old markers, archives deprecated content |
| **doc-optimize** | `/doc-optimize DocumentName OptimizationType Details` | Optimizes docs for RAG systems (extract sections, condense, split files) |

## Quick Start

### 1. Generate a Backend Service

```bash
/backend-service TextureValidationService Mod IFileHelper,IHashHelper ValidateTextureAsync,GetSupportedFormatsAsync
```

Generates:
- `Modules/Mod/Services/ITextureValidationService.cs` (interface)
- `Modules/Mod/Services/TextureValidationService.cs` (implementation with DI, events, error handling)
- Updates `Modules/Mod/ModServiceExtensions.cs` (registration)

### 2. Generate an Error with i18n

```bash
/error-with-i18n TEXTURE_INVALID_FORMAT fileName,supportedFormats "Invalid texture format: {{fileName}}. Supported: {{supportedFormats}}" "无效的纹理格式：{{fileName}}。支持的格式：{{supportedFormats}}"
```

Generates:
- C# `throw new OperationException(...)` code
- Adds `errors.TEXTURE_INVALID_FORMAT` to `Languages/en.json`
- Adds `errors.TEXTURE_INVALID_FORMAT` to `Languages/cn.json`

### 3. Generate a React Component

```bash
/react-component ModDetailsPanel panel ipc,state,events
```

Generates:
- `features/mods/components/ModDetailsPanel.tsx` (component with hooks)
- `features/mods/components/ModDetailsPanel.css` (BEM styles)
- Updates parent `index.ts` (export)

## How Skills Work

### Invocation Methods

**1. Manual Invocation** (Direct control):
```bash
/skill-name arguments
```

**2. Automatic Invocation** (Claude detects intent):
```
You: "Create a texture validation service"
Claude: [Automatically invokes backend-service skill]
```

### Skill Structure

Each skill is a folder containing:
```
.claude/skills/
├── backend-service/
│   └── SKILL.md          # Skill definition
├── error-with-i18n/
│   └── SKILL.md
└── README.md             # This file
```

### SKILL.md Format

```markdown
---
name: skill-name
description: What the skill does (used for auto-invocation)
disable-model-invocation: false  # Allow auto-invocation
---

# Skill Documentation

## Arguments
Format and examples

## What This Skill Generates
Detailed list

## Pattern to Follow
Code templates

## Steps to Execute
Exact steps Claude follows

## Important Rules
Do's and don'ts

## Reference Examples
Links to existing code

## Evolution Note
How to update this skill
```

## Evolution System

Skills are designed to evolve with your project. Here's how:

### When to Update Skills

Update skills when:
- ✅ Architecture patterns change (e.g., new DI pattern)
- ✅ Better patterns emerge (e.g., improved error handling)
- ✅ New features added (e.g., new React hooks)
- ✅ Team decisions change conventions (e.g., CSS methodology)
- ✅ Common mistakes are discovered (add to "Don't" list)

### How to Update Skills

**1. Update the SKILL.md file**:
```markdown
## Pattern to Follow

// OLD PATTERN (commented out for reference)
// public class Service { ... }

// NEW PATTERN (v2.0 - 2026-04-15: Added caching)
public class Service {
    private readonly IMemoryCache _cache;  // NEW: Add caching
    // ... rest of pattern
}
```

**2. Update the "Evolution Note" section**:
```markdown
## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial version
- v1.1 (2026-04-15): Added caching support
- v2.0 (2026-05-01): Changed to async-first pattern

**How to update this skill**:
1. Update "Pattern to Follow" section with new pattern
2. Add migration notes for old code (if breaking change)
3. Update reference examples to point to new patterns
4. Add new rules to "Important Rules" section
5. Update version history above
```

**3. Test the updated skill**:
```bash
# Generate sample code with updated skill
/backend-service TestService Mod IDependency TestMethodAsync

# Verify it follows new pattern
# If correct, commit the SKILL.md update
```

### Versioning Skills

Skills use simple versioning in their Evolution Note:

```markdown
**Version History**:
- v1.0 (2026-04-11): Initial backend-service skill
- v1.1 (2026-04-15): Added IMemoryCache pattern
- v1.2 (2026-04-20): Updated logging levels (Verbose for loops)
- v2.0 (2026-05-01): BREAKING: Changed to async-first pattern
```

**Semantic Versioning**:
- **v1.0 → v1.1**: Minor update (additive, non-breaking)
- **v1.x → v2.0**: Major update (breaking change to pattern)

### Migration When Patterns Change

When making breaking changes:

**1. Add migration section to skill**:
```markdown
## Migration from v1.x to v2.0

**Breaking Change**: Services now require IMemoryCache

**How to update existing code**:
1. Add IMemoryCache to constructor
2. Store cache key in private field
3. Replace direct repository calls with GetOrCreateAsync

**Example**:
```csharp
// OLD (v1.x)
public ModService(IModRepository repository) { ... }

// NEW (v2.0)
public ModService(IModRepository repository, IMemoryCache cache) { ... }
```
```

**2. Document in AI_GUIDE.md**:
```markdown
**Recent Additions (v3.8)**:
- Updated backend-service skill to v2.0 (requires IMemoryCache)
- Breaking change: All services now use caching pattern
```

### Keeping Skills in Sync with Docs

Skills and docs work together:

**Skills → Docs**:
- Skills reference `docs/WORKFLOWS.md` for detailed patterns
- Skills include "Reference Examples" section pointing to existing code

**Docs → Skills**:
- `docs/AI_GUIDE.md` lists all available skills
- `docs/WORKFLOWS.md` documents patterns that skills enforce

**Update Process**:
```
1. Architecture decision made
   ↓
2. Update docs/WORKFLOWS.md (document new pattern)
   ↓
3. Update relevant skills (reference new WORKFLOWS pattern)
   ↓
4. Update docs/AI_GUIDE.md (list updated skill versions)
   ↓
5. Commit all changes together (atomic update)
```

## Benefits of Skill System

### Consistency
- ✅ **Perfect Pattern Adherence**: Skills always generate correct code
- ✅ **No Forgotten Steps**: Can't forget DI, events, i18n translations
- ✅ **Uniform Code Style**: All generated code follows exact same pattern

### Efficiency
- ⚡ **Fast Generation**: One command vs 30+ minutes manual coding
- ⚡ **No Copy-Paste Errors**: Generated from template, not copied
- ⚡ **Reduced Rework**: Catches architecture violations before coding

### Maintainability
- 📝 **Single Source of Truth**: Update pattern once in skill
- 📝 **Version Controlled**: Skill changes tracked in git
- 📝 **Self-Documenting**: Skills include their own documentation

### Collaboration
- 👥 **Team Consistency**: Everyone uses same patterns
- 👥 **Onboarding**: New devs use skills, learn patterns
- 👥 **Code Review**: Less pattern violations to catch

## Skill Development Workflow

### Creating a New Skill

1. **Identify Repetitive Pattern**:
   - Code you write repeatedly (e.g., repositories, validators)
   - Pattern with many steps that are often forgotten
   - Pattern that has common mistakes

2. **Document the Pattern**:
   - Add to `docs/WORKFLOWS.md` first
   - Include examples from existing code
   - List all requirements and edge cases

3. **Create Skill**:
   ```bash
   mkdir .claude/skills/new-skill
   touch .claude/skills/new-skill/SKILL.md
   ```

4. **Write SKILL.md**:
   - Add YAML frontmatter (name, description)
   - Document arguments and usage
   - Include complete pattern
   - Add step-by-step execution guide
   - List important rules
   - Add reference examples
   - Include evolution note

5. **Test Skill**:
   ```bash
   /new-skill arguments
   # Verify generated code
   ```

6. **Update AI_GUIDE.md**:
   - Add skill to "Available Skills" table
   - Add usage examples
   - Update workflows to mention skill

7. **Update This README**:
   - Add to "Available Skills" table
   - Add quick start example

### Skill Template

Use this template for new skills:

```markdown
---
name: skill-name
description: Brief description for auto-invocation (< 250 chars)
disable-model-invocation: false
---

# Skill Title

Brief explanation of what this skill does.

## Arguments

**Format**: `/skill-name <Arg1> <Arg2>`

**Example**:
```
/skill-name value1 value2
```

**Parameters**:
- `Arg1` - Description
- `Arg2` - Description

## What This Skill Generates

1. **File 1** - Description
2. **File 2** - Description
3. **Updates** - What gets updated

## Pattern to Follow

```language
// Complete pattern here
```

## Steps to Execute

1. **Step 1**: Details
2. **Step 2**: Details
3. **Step 3**: Details

## Important Rules

- ✅ Do this
- ✅ Do that
- ❌ Don't do this
- ❌ Don't do that

## Reference Examples

- `path/to/example1.ts` - Description
- `path/to/example2.cs` - Description

## Evolution Note

**Version History**:
- v1.0 (YYYY-MM-DD): Initial version

**How to update this skill**:
1. Update pattern in "Pattern to Follow"
2. Add migration notes if breaking
3. Update reference examples
4. Update version history
```

## Best Practices

### DO:
- ✅ Keep skills focused (one pattern per skill)
- ✅ Update skills when patterns evolve
- ✅ Version skills with semantic versioning
- ✅ Reference existing code examples
- ✅ Include complete patterns (no half-examples)
- ✅ Add migration notes for breaking changes
- ✅ Test skills after updating
- ✅ Commit skill updates with code changes

### DON'T:
- ❌ Create skills for one-time operations
- ❌ Make skills too generic (loses pattern enforcement)
- ❌ Forget to update AI_GUIDE.md when adding skills
- ❌ Update skills without testing
- ❌ Break existing code with skill changes (add migration notes)

## Troubleshooting

### Skill Not Found

**Problem**: `/skill-name` shows "skill not found"

**Solution**:
1. Check skill folder name matches YAML `name` field
2. Verify SKILL.md file exists in folder
3. Restart Claude Code to refresh skill cache

### Skill Generates Wrong Pattern

**Problem**: Generated code doesn't match expected pattern

**Solution**:
1. Check SKILL.md "Pattern to Follow" section
2. Verify arguments are correct format
3. Update skill if pattern has evolved
4. Check "Reference Examples" for current pattern

### Skill Auto-Invokes When It Shouldn't

**Problem**: Skill runs automatically when not wanted

**Solution**:
1. Set `disable-model-invocation: true` in YAML frontmatter
2. Use more specific description (limit scope)
3. Manually invoke with `/skill-name` to override

## Related Documentation

- **[docs/AI_GUIDE.md](../../docs/AI_GUIDE.md)** - Main AI assistant guide (references skills)
- **[docs/WORKFLOWS.md](../../docs/ai-assistant/WORKFLOWS.md)** - Detailed implementation patterns
- **[docs/DESIGN_DECISIONS.md](../../docs/core/DESIGN_DECISIONS.md)** - Architecture constraints
- **[Claude Code Skills Docs](https://docs.claude.com/en/docs/claude-code/skills)** - Official documentation

## Contributing

When adding or updating skills:

1. **Test thoroughly** - Generate sample code and verify it works
2. **Document changes** - Update version history and AI_GUIDE.md
3. **Get review** - Have team review pattern changes
4. **Commit together** - Skill updates + docs updates in one commit
5. **Announce changes** - Let team know about new/updated skills

## Future Skills (Ideas)

Skills to consider creating:

- **repository-method** - Generate repository CRUD methods
- **validator** - Generate fluent validation classes
- **migration** - Generate database migration files
- **test-suite** - Generate xUnit test class with common patterns
- **hook** - Generate custom React hook
- **context** - Generate React context + provider
- **middleware** - Generate ASP.NET middleware

Have an idea for a skill? Add to this list or discuss with team!

---

**Remember**: Skills are living documents. Keep them updated as patterns evolve!
