---
name: doc-update-guide
description: Update guide-type documentation (AI_GUIDE.md, CODE_GENERATION.md) with new features, patterns, or system changes
---

# Guide Documentation Updater

Update guide-type documentation files that explain how to use the system, how components work together, and provide workflows.

**Purpose**: Guide documents (AI_GUIDE.md, CODE_GENERATION.md) require frequent updates when new skills, features, or patterns are added. This skill ensures consistent updates with proper versioning and changelog maintenance.

## Arguments

**Format**: `/doc-update-guide <DocumentName> <ChangeType> <Details>`

**Example**:
```
/doc-update-guide AI_GUIDE new-skill "Added backend-validator skill for FluentValidation generation"
/doc-update-guide CODE_GENERATION new-pattern "Added repository method generation pattern"
/doc-update-guide AI_GUIDE update-workflow "Updated service creation workflow to include cache invalidation"
```

**Parameters**:
- `DocumentName` - Guide document to update (AI_GUIDE, CODE_GENERATION)
- `ChangeType` - Type of change (new-skill, new-pattern, update-workflow, new-feature, deprecation)
- `Details` - Description of what's being added/changed/deprecated

## What This Skill Does

1. **Identifies target document**:
   - AI_GUIDE.md - Main assistant guide
   - CODE_GENERATION.md - Code generation system guide

2. **Updates version number**:
   - Increments minor version (e.g., v3.7 → v3.8)
   - Major version only for breaking changes

3. **Adds changelog entry**:
   - Prepends new entry to changelog section
   - Includes date, version, and change description

4. **Updates relevant sections**:
   - New skills → Add to skills table + usage examples
   - New patterns → Add to patterns section + decision tree
   - New features → Add to features list + integration guide
   - Deprecations → Add deprecation notice + migration guide

5. **Maintains consistency**:
   - Updates cross-references if needed
   - Ensures table of contents is current
   - Validates markdown formatting

## Target Documents

### AI_GUIDE.md

**Sections to Update**:

1. **Version & Changelog** (top of file):
```markdown
**Version:** 3.8
**Last Updated:** 2026-04-11

## Recent Updates

**v3.8 (2026-04-11)**:
- Added backend-validator skill for FluentValidation generation
- Updated service creation workflow to include validation
```

2. **Skills Table** (when adding new skill):
```markdown
| **backend-validator** | `/backend-validator EntityName Properties` | FluentValidation class + rules |
```

3. **Workflow Sections** (when updating workflows):
```markdown
### Creating a Backend Service

1. Generate service: `/backend-service ServiceName Module Dependencies Methods`
2. **NEW**: Add validator: `/backend-validator EntityName Properties`
3. Register service: `/service-registration Module IServiceName ServiceName singleton`
```

4. **Decision Trees** (when adding patterns):
```markdown
- Need validation? → Use `/backend-validator`
```

### CODE_GENERATION.md

**Sections to Update**:

1. **Version & Date** (top of file)

2. **System Components** (when adding new skills):
```markdown
#### Validation Skills
- **backend-validator** - FluentValidation class generation
```

3. **Decision Trees** (when adding patterns):
```markdown
graph TD
    A[Need CRUD?] --> B[Repository]
    B --> C[Need validation?]
    C -->|Yes| D[/backend-validator]
```

4. **Integration Examples** (when adding workflows):
```markdown
Complete CRUD with validation:
1. `/backend-service` → Service layer
2. `/backend-validator` → Validation layer
3. `/service-registration` → DI registration
```

## Update Patterns

### Pattern 1: New Skill Added

**Steps**:
1. Increment version number
2. Add changelog entry
3. Add to skills table in AI_GUIDE.md
4. Add to skills list in CODE_GENERATION.md
5. Add usage example to relevant workflow
6. Update decision tree if applicable

**Example**:
```markdown
<!-- AI_GUIDE.md -->
**Version:** 3.8

**v3.8 (2026-04-11)**:
- Added backend-validator skill for FluentValidation generation

| **backend-validator** | `/backend-validator EntityName Properties` | FluentValidation class + rules |
```

### Pattern 2: Skill Deprecated

**Steps**:
1. Increment version number
2. Add changelog entry with deprecation notice
3. Add ~~strikethrough~~ to skill name in table
4. Add deprecation note with replacement
5. Update workflows to use new skill

**Example**:
```markdown
**v3.8 (2026-04-11)**:
- **DEPRECATED**: old-skill (use new-skill instead)

| ~~**old-skill**~~ | **DEPRECATED** - Use `new-skill` instead |
```

### Pattern 3: Workflow Updated

**Steps**:
1. Increment version number
2. Add changelog entry
3. Update workflow section with new steps
4. Add "NEW" or "UPDATED" markers for changed steps
5. Update related decision trees

**Example**:
```markdown
### Creating a Backend Service

1. Generate service: `/backend-service ServiceName Module Dependencies Methods`
2. **NEW**: Add caching: Inject `IMemoryCache` and use `GetOrCreateAsync` pattern
3. Register service: `/service-registration Module IServiceName ServiceName singleton`
```

### Pattern 4: New Feature Added

**Steps**:
1. Increment version number
2. Add changelog entry
3. Add feature to features list
4. Create new section if needed
5. Add integration examples
6. Update keywords index

**Example**:
```markdown
**v3.8 (2026-04-11)**:
- Added automatic cache invalidation system with file watchers

## New Feature: Cache Invalidation

Use `/file-watcher` to create FileSystemWatcher services that automatically invalidate caches when files change.
```

## Version Numbering Rules

**Semantic Versioning for Docs**:
- **Major (v3.x → v4.x)**: Breaking changes to patterns, complete system rewrites
- **Minor (v3.7 → v3.8)**: New skills, new features, workflow updates
- **Patch (v3.7.1 → v3.7.2)**: Bug fixes, typo corrections (rarely used)

**Increment minor version for**:
- New skill added
- New pattern introduced
- Workflow significantly updated
- New system feature

**Increment major version for**:
- Complete pattern overhaul (e.g., DI pattern changes)
- Deprecated skills removed
- Breaking changes to existing workflows

## Important Rules

- ✅ Always increment version number
- ✅ Always add changelog entry at top (newest first)
- ✅ Include date in changelog (YYYY-MM-DD format)
- ✅ Mark new/changed content with "NEW" or "UPDATED" badges
- ✅ Update cross-references when renaming skills
- ✅ Maintain alphabetical order in tables
- ✅ Use consistent markdown formatting
- ❌ Don't delete changelog history (keep all entries)
- ❌ Don't skip version numbers
- ❌ Don't update docs without changing version
- ❌ Don't add content without changelog entry

## Integration with Other Skills

**After creating new skills**:
```bash
# 1. Create skill
/backend-validator ModValidator Name,Description,Path required,maxlength

# 2. Update documentation (this skill)
/doc-update-guide AI_GUIDE new-skill "Added backend-validator skill"
```

**After updating patterns**:
```bash
# 1. Update ADVANCED_PATTERNS.md using doc-update-technical
/doc-update-technical ADVANCED_PATTERNS new-pattern "Added async validation pattern"

# 2. Update guides to reference new pattern
/doc-update-guide AI_GUIDE new-pattern "Added async validation pattern to service creation workflow"
```

## Validation Checklist

After updating, verify:

- [ ] Version number incremented correctly
- [ ] Changelog entry added at top with date
- [ ] New content added to appropriate section
- [ ] Cross-references updated if names changed
- [ ] Examples include correct syntax
- [ ] Tables maintain alignment
- [ ] Markdown formatting is valid
- [ ] Links are not broken
- [ ] Keywords index updated (if major addition)

## Reference Examples

**AI_GUIDE.md structure**:
- Version at top
- Changelog section (recent updates first)
- Skills table (alphabetical)
- Workflows (step-by-step)
- Decision trees (when to use what)
- Best practices

**CODE_GENERATION.md structure**:
- Version at top
- System overview
- Component breakdown (docs + skills + agents)
- Decision trees
- Integration examples
- Evolution process

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial doc-update-guide skill

**How to update this skill**:
1. If guide document structure changes, update section detection logic
2. If versioning strategy changes, update version number rules
3. If new change types emerge, add to ChangeType parameter options
4. Update reference examples when guide formats evolve
