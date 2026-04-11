---
name: doc-optimize
description: Optimize documentation for RAG systems and LLM context windows by extracting large sections, condensing content, and improving structure
---

# Documentation Optimizer

Optimize documentation files for RAG (Retrieval-Augmented Generation) systems and LLM context windows.

**Purpose**: Large documentation files (>30KB) become inefficient for RAG loading and LLM processing. This skill analyzes docs and performs targeted optimizations: extracting sections, condensing content, improving structure, and creating focused subdocuments.

## Arguments

**Format**: `/doc-optimize <DocumentName> <OptimizationType> <Details>`

**Example**:
```
/doc-optimize AI_GUIDE extract-changelog "Move changelog to separate CHANGELOG file"
/doc-optimize AI_GUIDE extract-section "Batch Edit Feature Pattern → docs/features/BATCH_EDIT.md"
/doc-optimize BACKEND condense-examples "Replace full code blocks with references to example files"
/doc-optimize all analyze "Generate optimization report for all docs >30KB"
```

**Parameters**:
- `DocumentName` - Document to optimize (AI_GUIDE, BACKEND, FRONTEND, all)
- `OptimizationType` - Type of optimization (extract-changelog, extract-section, condense-examples, split-file, analyze)
- `Details` - Optimization-specific details (target file, section name, threshold)

## What This Skill Does

1. **Analyzes documentation size**:
   - Identifies files >30KB (inefficient for RAG)
   - Reports section sizes and redundancy
   - Suggests optimization strategies

2. **Extracts large sections**:
   - Moves version history to CHANGELOG files
   - Extracts feature-specific docs to features/
   - Creates focused subdocuments

3. **Condenses content**:
   - Replaces full code blocks with references
   - Summarizes verbose explanations
   - Removes redundant examples

4. **Improves structure**:
   - Splits large files into focused modules
   - Creates cross-references between docs
   - Improves table of contents

5. **RAG-optimizes**:
   - Ensures docs are <30KB (optimal RAG size)
   - Adds clear section markers for keyword search
   - Improves doc metadata for better retrieval

## Optimization Types

### 1. Analyze Operation

**Purpose**: Generate optimization report for documentation.

**Format**: `/doc-optimize <DocumentName|all> analyze <SizeThreshold>`

**Example**:
```bash
/doc-optimize all analyze 30KB
/doc-optimize AI_GUIDE analyze 20KB
```

**What it does**:
1. Scans files for size (human-readable)
2. Identifies sections >5KB
3. Detects redundant content
4. Generates optimization report

**Report Output**:
```markdown
# Documentation Optimization Report
**Generated:** 2026-04-11
**Threshold:** 30KB

---

## Files Exceeding Threshold

### 1. AI_GUIDE.md (56KB) - **NEEDS OPTIMIZATION**

**Size Breakdown**:
- Changelog section (lines 7-46): 3KB
- Batch Edit Feature section (lines 1491-1641): 12KB
- Core Patterns section (lines 605-1159): 35KB
- Quick references: 6KB

**Optimization Opportunities**:
1. **Extract changelog** → `docs/CHANGELOG-AI_GUIDE.md` (saves 3KB)
2. **Extract Batch Edit** → `docs/features/BATCH_EDIT_PATTERN.md` (saves 12KB)
3. **Condense Core Patterns** → Link to skills instead of full examples (saves 15KB)

**Projected Size After Optimization**: 26KB (-30KB, 54% reduction)

---

### 2. BACKEND.md (36KB) - **NEEDS OPTIMIZATION**

**Size Breakdown**:
- Full service examples: 18KB
- Repository patterns: 10KB
- DI patterns: 8KB

**Optimization Opportunities**:
1. **Replace examples** → Link to `.claude/skills/backend-service/SKILL.md` (saves 10KB)
2. **Condense DI section** → Summary + link to DESIGN_DECISIONS.md (saves 3KB)

**Projected Size After Optimization**: 23KB (-13KB, 36% reduction)

---

## Summary

**Total Files**: 50
**Files >30KB**: 5 (10%)
**Total Size**: 850KB
**Optimizable Size**: 180KB (21% of total)
**Projected Size After Optimization**: 670KB (-180KB, 21% reduction)

**High Priority** (>50KB):
- AI_GUIDE.md (56KB) → 26KB
- CHANGELOG.md (92KB) → Keep as-is (historical record)

**Medium Priority** (30-50KB):
- BACKEND.md (36KB) → 23KB
- FRONTEND.md (42KB) → 28KB

**Recommendations**:
1. Extract feature-specific sections to `docs/features/`
2. Replace full code examples with skill references
3. Move version history to separate CHANGELOG files
4. Create focused subdocuments for large topics
```

### 2. Extract Changelog Operation

**Purpose**: Move version history to separate CHANGELOG file.

**Format**: `/doc-optimize <DocumentName> extract-changelog <Reason>`

**Example**:
```bash
/doc-optimize AI_GUIDE extract-changelog "Version history is 3KB and growing"
```

**What it does**:
1. Extracts changelog section (e.g., lines 7-46)
2. Creates `docs/CHANGELOG-{DocumentName}.md`
3. Replaces with summary + link
4. Updates last modified date

**Before** (AI_GUIDE.md):
```markdown
# AI Assistant Guide

**Version:** 3.7
**Last Updated:** 2026-04-11

**Recent Additions (v3.7):**
- Agent-first development workflow
- Agent & Skill System
- ... (40 lines of changelog)

**Previous (v3.6):**
- Testing principles section
- ... (more history)
```

**After** (AI_GUIDE.md):
```markdown
# AI Assistant Guide

**Version:** 3.7
**Last Updated:** 2026-04-11

**Recent Changes**: See [CHANGELOG-AI_GUIDE.md](CHANGELOG-AI_GUIDE.md) for complete version history.

**Key Features (v3.7)**:
- Agent-first development workflow (Explore/Plan agents)
- Skills system for code generation (17 skills available)
- RAG document loading automation
```

**Created** (CHANGELOG-AI_GUIDE.md):
```markdown
# AI_GUIDE.md Changelog

Complete version history for the AI Assistant Guide.

## v3.7 (2026-04-11)

### Added
- Agent-first development workflow with Explore and Plan agents
- Agent & Skill System for consistent code generation
- Automated RAG document loading via agents
- Custom skill framework for backend/frontend pattern generation
- Updated workflow to enforce agent-assisted development

### Changed
- Workflow prioritizes agents over manual RAG loading
- Token optimization strategy leverages agent caching

## v3.6 (2026-04-10)

### Added
- Testing principles section with emphasis on verifying design intent
...
```

**Size Savings**: 3KB in main guide

### 3. Extract Section Operation

**Purpose**: Move large feature-specific or topic-specific section to separate file.

**Format**: `/doc-optimize <DocumentName> extract-section "<SectionName> → <TargetFile>"`

**Example**:
```bash
/doc-optimize AI_GUIDE extract-section "Batch Edit Feature Pattern → docs/features/BATCH_EDIT_PATTERN.md"
/doc-optimize BACKEND extract-section "Repository Patterns → docs/patterns/REPOSITORY.md"
```

**What it does**:
1. Identifies section by heading
2. Extracts entire section to target file
3. Replaces with summary + link
4. Adds cross-references

**Before** (AI_GUIDE.md - 150 lines):
```markdown
## 🔧 Batch Edit Feature Pattern (v3.3)

### Overview
Spreadsheet-style batch metadata editor using AG Grid...

### Key Components

**BatchEditModsScreen** (`BatchEditModsScreen.tsx`)
...
[140 more lines of detailed implementation]
```

**After** (AI_GUIDE.md - 10 lines):
```markdown
## 🔧 Batch Edit Feature

Spreadsheet-style batch metadata editor with find/replace functionality.

**Key Features**:
- AG Grid with custom theming
- VSCode-style find/replace panel
- Inline text highlighting

**Implementation Details**: See [docs/features/BATCH_EDIT_PATTERN.md](features/BATCH_EDIT_PATTERN.md)
```

**Created** (docs/features/BATCH_EDIT_PATTERN.md):
```markdown
# Batch Edit Feature Pattern

**Related**: [AI_GUIDE.md](../AI_GUIDE.md) - Batch Edit Feature section
**Version**: v3.3
**Created**: 2026-03-15

## Overview

Spreadsheet-style batch metadata editor using AG Grid with VSCode-style find/replace panel.

## Key Components

**BatchEditModsScreen** (`BatchEditModsScreen.tsx`)
...
[Full 140-line implementation details]
```

**Size Savings**: 140 lines (12KB) in main guide

### 4. Condense Examples Operation

**Purpose**: Replace full code blocks with references to skill files or example code.

**Format**: `/doc-optimize <DocumentName> condense-examples <Strategy>`

**Example**:
```bash
/doc-optimize AI_GUIDE condense-examples "Link to skills instead of full examples"
/doc-optimize BACKEND condense-examples "Link to existing code examples"
```

**What it does**:
1. Identifies full code block examples (>20 lines)
2. Checks if pattern exists in skill or codebase
3. Replaces with summary + link
4. Keeps only key snippets

**Before** (AI_GUIDE.md):
```markdown
### Backend Service (with Event Emission)
```csharp
// 1. Interface
public interface IModLifecycleService {
    Task<ModLoadResult> LoadAsync(string id);
    Task<bool> UnloadAsync(string id);
}

// 2. Implementation with DI + Event Emission
public class ModLifecycleService : IModLifecycleService {
    private readonly IModRepository _repository;
    private readonly IModArchiveService _archiveService;
    private readonly IModCacheService _cacheService;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;

    public ModLifecycleService(
        IModRepository repository,
        IModArchiveService archiveService,
        IModCacheService cacheService,
        IProfileEventBus eventBus,
        ILogHelper logger) {
        _repository = repository;
        _archiveService = archiveService;
        _cacheService = cacheService;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<ModLoadResult> LoadAsync(string id) {
        // Business logic here...
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.LOADED, new { Id = id });
        return new ModLoadResult { Success = success };
    }
}

// 3. Register in {Module}ServiceExtensions.cs
services.AddSingleton<IModLifecycleService, ModLifecycleService>();
```

**After** (AI_GUIDE.md):
```markdown
### Backend Service (with Event Emission)

Services perform business logic and emit events.

**Pattern**: Interface + Implementation + DI + Events

**Key Requirements**:
- Inject `IProfileEventBus` for event emission
- Inject dependencies via constructor
- Emit events after successful operations
- Register in `{Module}ServiceExtensions.cs`

**Generate with skill**: `/backend-service ServiceName Module Dependencies Methods`

**Full pattern**: See [.claude/skills/backend-service/SKILL.md](../.claude/skills/backend-service/SKILL.md)

**Example**: [Modules/Mod/Services/ModLifecycleService.cs](../../Modules/Mod/Services/ModLifecycleService.cs)
```

**Size Savings**: 35 lines → 15 lines (saves 20 lines per example)

### 5. Split File Operation

**Purpose**: Split large document into focused subdocuments.

**Format**: `/doc-optimize <DocumentName> split-file <SplitStrategy>`

**Example**:
```bash
/doc-optimize BACKEND split-file "services,facades,repositories"
/doc-optimize AI_GUIDE split-file "core,workflows,patterns"
```

**What it does**:
1. Analyzes document structure
2. Identifies logical split points
3. Creates subdocuments for each topic
4. Creates index document with links
5. Maintains cross-references

**Before** (BACKEND.md - 36KB, single file):
```markdown
# Backend Development Guide

## Services
[12KB of service patterns]

## Facades
[8KB of facade patterns]

## Repositories
[10KB of repository patterns]

## Testing
[6KB of testing patterns]
```

**After** (docs/backend/ directory structure):
```
docs/backend/
├── README.md (3KB - overview + links)
├── SERVICES.md (12KB - service patterns)
├── FACADES.md (8KB - facade patterns)
├── REPOSITORIES.md (10KB - repository patterns)
└── TESTING.md (6KB - testing patterns)
```

**README.md** (new index):
```markdown
# Backend Development Guide

Quick links to backend development documentation.

## Topics

### Services
Business logic layer with DI and event emission.

**Read**: [SERVICES.md](SERVICES.md)

**Skills**: `/backend-service`, `/service-registration`

**Examples**: `Modules/*/Services/*Service.cs`

### Facades
Thin IPC layer (no business logic).

**Read**: [FACADES.md](FACADES.md)

**Skill**: `/backend-facade`

**Examples**: `Modules/*/Facades/*Facade.cs`

### Repositories
Data access layer with async patterns.

**Read**: [REPOSITORIES.md](REPOSITORIES.md)

**Examples**: `Modules/*/Repositories/*Repository.cs`

### Testing
Unit and integration testing patterns.

**Read**: [TESTING.md](TESTING.md)

**Examples**: `D3dxSkinManager.Tests/**/*Tests.cs`
```

**Benefits**:
- Each file <15KB (RAG-friendly)
- Focused topics (easier to navigate)
- Better keyword search (specific files)
- Parallel loading possible (load only needed topic)

## RAG Optimization Best Practices

### Optimal Document Size

**Recommended Sizes**:
- **Guides**: 20-30KB (single focused topic)
- **References**: 10-20KB (lookup tables, no prose)
- **Technical Patterns**: 10-15KB (focused on one pattern)
- **Changelogs**: No limit (historical record)

**Why 30KB threshold?**:
- Most LLM context windows are 128K-200K tokens
- 30KB ≈ 7500 tokens (allows 4-5 docs to load comfortably)
- RAG systems work best with focused, single-topic documents
- Smaller docs = better keyword matching accuracy

### Section Markers for Keyword Search

**Good section structure**:
```markdown
## 🔍 Topic Name

**Purpose**: One-sentence summary

**When to use**: Clear use case

**Pattern**: Code or steps

**Examples**: Links to code

**Related**: Cross-references
```

**Why this works**:
- Keywords in headings (easy grep)
- Purpose statement (quick relevance check)
- Pattern section (focused on implementation)
- Examples external (reduces size)

### Cross-Reference Strategy

**Link instead of duplicate**:
```markdown
<!-- ❌ BAD: Duplicate pattern in multiple docs -->
## Service Pattern (in AI_GUIDE.md)
[Full 50-line pattern]

## Service Pattern (in BACKEND.md)
[Same 50-line pattern - REDUNDANT]

<!-- ✅ GOOD: Single source + links -->
## Service Pattern (in .claude/skills/backend-service/SKILL.md)
[Full pattern with examples]

## Creating Services (in AI_GUIDE.md)
Use `/backend-service` skill.
**Pattern**: See [backend-service skill](../.claude/skills/backend-service/SKILL.md)

## Service Development (in BACKEND.md)
**Pattern**: See [backend-service skill](../.claude/skills/backend-service/SKILL.md)
**Skill**: `/backend-service ServiceName Module Dependencies Methods`
```

## Important Rules

- ✅ Always backup before optimization
- ✅ Preserve content (extract, don't delete)
- ✅ Create cross-references after extraction
- ✅ Update KEYWORDS_INDEX.md with new file locations
- ✅ Test RAG loading after optimization
- ✅ Target <30KB for guides, <20KB for references
- ✅ Keep changelogs intact (historical value)
- ❌ Don't delete content (extract instead)
- ❌ Don't break existing links
- ❌ Don't split mid-topic (maintain cohesion)
- ❌ Don't over-condense (preserve clarity)

## Integration with Other Skills

**Typical optimization workflow**:
```bash
# 1. Analyze documentation (this skill)
/doc-optimize all analyze 30KB

# 2. Review report findings
# (Identifies AI_GUIDE.md is 56KB, needs optimization)

# 3. Extract large sections (this skill)
/doc-optimize AI_GUIDE extract-changelog "Move version history"
/doc-optimize AI_GUIDE extract-section "Batch Edit → docs/features/BATCH_EDIT_PATTERN.md"

# 4. Condense examples (this skill)
/doc-optimize AI_GUIDE condense-examples "Link to skills"

# 5. Update references
/doc-update-reference KEYWORDS_INDEX new-path "docs/features/BATCH_EDIT_PATTERN.md - Batch edit implementation"

# 6. Monitor results
/doc-monitor all all
# Should show improved health score and smaller file sizes
```

**After creating new features**:
```bash
# 1. Document feature in guide
# (AI_GUIDE.md grows to 65KB)

# 2. Extract to focused doc (this skill)
/doc-optimize AI_GUIDE extract-section "New Feature → docs/features/NEW_FEATURE.md"

# 3. Update references
/doc-update-guide AI_GUIDE update-section "Added link to NEW_FEATURE.md"
/doc-update-reference KEYWORDS_INDEX new-keyword "new feature → docs/features/NEW_FEATURE.md"
```

## Validation Checklist

After optimization:

- [ ] All extracted content preserved in new files
- [ ] Links updated in original document
- [ ] Cross-references added to new files
- [ ] KEYWORDS_INDEX.md updated with new paths
- [ ] Original document size reduced to <30KB (or target)
- [ ] No broken links (verify with `/doc-monitor broken-links`)
- [ ] Table of contents updated if structure changed
- [ ] Related documents updated with new links

## Example Optimization: AI_GUIDE.md

**Current State**:
- Size: 56KB (1674 lines)
- Issues: Too large for efficient RAG loading
- Sections to extract: Changelog (3KB), Batch Edit (12KB), Core Patterns (redundant with skills)

**Optimization Plan**:

**Step 1: Extract Changelog**
```bash
/doc-optimize AI_GUIDE extract-changelog "Version history growing rapidly"
```
Result: AI_GUIDE.md: 56KB → 53KB

**Step 2: Extract Feature Patterns**
```bash
/doc-optimize AI_GUIDE extract-section "Batch Edit Feature Pattern → docs/features/BATCH_EDIT_PATTERN.md"
```
Result: AI_GUIDE.md: 53KB → 41KB

**Step 3: Condense Code Examples**
```bash
/doc-optimize AI_GUIDE condense-examples "Link to skills instead of full code"
```
Result: AI_GUIDE.md: 41KB → 26KB

**Final Result**:
- AI_GUIDE.md: 56KB → 26KB (54% reduction)
- Created: CHANGELOG-AI_GUIDE.md (3KB)
- Created: docs/features/BATCH_EDIT_PATTERN.md (12KB)
- Total docs: +2 files, but better RAG efficiency

**Benefits**:
- AI_GUIDE.md now <30KB (RAG-optimal)
- Feature patterns in focused docs (easier to find)
- Version history separated (historical reference)
- Core guide focuses on workflows (not implementation details)

## Reference Examples

**Good Document Structure** (< 30KB):
```markdown
# Guide Title

**Purpose**: One-sentence summary
**Audience**: Who should read this
**Related**: Links to related docs

## Quick Start

[Focused introduction]

## Core Concepts

[Essential concepts only]

## Patterns

[Link to skills or detailed pattern docs]

## Examples

[Link to code examples]

## Related Documentation

- [Detailed Patterns](patterns/PATTERN_NAME.md)
- [Skills](.claude/skills/skill-name/SKILL.md)
```

**Bad Document Structure** (>50KB):
```markdown
# Guide Title

## Version History
[50 versions worth of changelog]

## Introduction
[Verbose introduction]

## All Patterns
[Every pattern with full code examples - 200 lines each]

## All Examples
[Duplicate examples from patterns section]

## Troubleshooting
[100+ troubleshooting entries]
```

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial doc-optimize skill

**How to update this skill**:
1. If size thresholds change, update recommendations
2. If new optimization strategies emerge, add to operations
3. If RAG best practices evolve, update guidelines
4. Update examples as documentation patterns improve
