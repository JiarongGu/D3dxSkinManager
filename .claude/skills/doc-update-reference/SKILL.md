---
name: doc-update-reference
description: Use after adding new files, constants, IPC messages, or skills to update REFERENCE.md and KEYWORDS_INDEX.md so they can be found via doc-loader.
---

# Reference Documentation Updater

Update reference documentation that provides quick lookup information like file paths, constants, commands, and keyword mappings.

**Purpose**: Reference documents act as quick-lookup tables. When new modules, files, constants, or patterns are added, these references must be updated to maintain their usefulness.

## Arguments

**Format**: `/doc-update-reference <DocumentName> <EntryType> <Details>`

**Example**:
```
/doc-update-reference REFERENCE new-path "Modules/Mod/Validators/ - FluentValidation validators"
/doc-update-reference KEYWORDS_INDEX new-keyword "validation → backend-validator skill, Modules/Mod/Validators/"
/doc-update-reference REFERENCE new-constant "ModuleNames.VALIDATION - Validation module identifier"
```

**Parameters**:
- `DocumentName` - Reference document to update (REFERENCE, KEYWORDS_INDEX)
- `EntryType` - Type of entry (new-path, new-constant, new-keyword, new-command, update-entry)
- `Details` - Entry information to add

## What This Skill Does

1. **Identifies target document**:
   - REFERENCE.md - File paths, constants, commands reference
   - KEYWORDS_INDEX.md - Keyword → file/skill mapping

2. **Adds new entries**:
   - Maintains alphabetical or categorical order
   - Follows document formatting conventions
   - Includes descriptions and usage hints

3. **Updates existing entries**:
   - Replaces outdated information
   - Marks deprecated entries
   - Adds migration notes

4. **Maintains structure**:
   - Preserves section organization
   - Updates last-updated date
   - Ensures consistent formatting

## Target Documents

### REFERENCE.md

**Structure**:
```markdown
# D3dxSkinManager Reference

**Last Updated:** 2026-04-11

## File Structure

### Backend (C#)
- `Modules/{Module}/Services/` - Business logic services
- `Modules/{Module}/Repositories/` - Data access layer
- `Modules/{Module}/Validators/` - FluentValidation validators (NEW)
- `Modules/{Module}/Facades/` - IPC facades (thin layer)

### Frontend (TypeScript)
- `shared/services/ipc/{module}Service.ts` - IPC service clients
- `features/{feature}/components/` - React components

## Constants Reference

### Module Names
- `ModuleNames.MOD` - Mod management module
- `ModuleNames.PROFILE` - Profile management module
- `ModuleNames.VALIDATION` - Validation module (NEW)

### Event Names
- `ModEvents.CACHE_CHANGED` - Cache invalidation event
- `ValidationEvents.VALIDATION_FAILED` - Validation failure event (NEW)

## IPC Message Types

### Mod Module
- `GET_ACTIVE_MODS` - Retrieve active mods list
- `BATCH_DELETE_MODS` - Delete multiple mods
- `VALIDATE_MOD` - Validate mod structure (NEW)

## Command Reference

### Skills
- `/backend-service` - Generate C# service + interface + DI
- `/backend-validator` - Generate FluentValidation validator (NEW)
- `/service-registration` - Register service in DI container
```

**Update Patterns**:

1. **New File Path**:
   - Add to appropriate section (Backend/Frontend)
   - Include description
   - Mark as NEW if recent

2. **New Constant**:
   - Add to appropriate constants section
   - Include usage context
   - Maintain alphabetical order

3. **New IPC Message**:
   - Add to module section
   - Include payload structure
   - Reference backend handler

4. **New Command**:
   - Add to skills section
   - Include brief description
   - Link to skill documentation

### KEYWORDS_INDEX.md

**Structure**:
```markdown
# Keywords Index

**Last Updated:** 2026-04-11

Quick keyword lookup for finding relevant skills, files, or documentation.

## A

**Authentication**:
- Files: `Modules/Auth/Services/AuthService.cs`
- Docs: `docs/ai-assistant/REFERENCE.md` (Authentication section)

## B

**Batch Operations**:
- Skill: `/batch-operation`
- Files: `Modules/*/Facades/*Facade.cs` (BatchDelete, BatchUpdate methods)
- Docs: `.claude/skills/batch-operation/SKILL.md`

## C

**Caching**:
- Pattern: `docs/core/ADVANCED_PATTERNS.md` (IMemoryCache section)
- Files: `Modules/*/Services/*QueryService.cs`
- Events: `*Events.CACHE_CHANGED`

**Cache Invalidation**:
- Skill: `/file-watcher`
- Pattern: `docs/core/ADVANCED_PATTERNS.md` (Cache invalidation section)

## V

**Validation** (NEW):
- Skill: `/backend-validator`
- Files: `Modules/*/Validators/*Validator.cs`
- Docs: `.claude/skills/backend-validator/SKILL.md`
```

**Update Patterns**:

1. **New Keyword Entry**:
   - Add under appropriate letter section
   - Include all relevant references (skills, files, docs)
   - Use consistent formatting

2. **Add Reference to Existing Keyword**:
   - Append to existing entry
   - Maintain categorization (Skill/Files/Docs/Pattern)

3. **Related Keywords**:
   - Add cross-references (See also: X, Y)

## Update Patterns

### Pattern 1: New Module Added

**REFERENCE.md Updates**:
```markdown
### Module Names
- `ModuleNames.NEWMODULE` - New module description (NEW)

### New Module
- `GET_NEWMODULE_DATA` - Retrieve data
- `CREATE_NEWMODULE_ITEM` - Create item
```

**KEYWORDS_INDEX.md Updates**:
```markdown
## N

**New Module** (NEW):
- Module: `Modules/NewModule/`
- Service: `INewModuleService`
- Events: `NewModuleEvents.*`
```

### Pattern 2: New Skill Added

**REFERENCE.md Updates**:
```markdown
### Skills
- `/new-skill` - Skill description (NEW)
```

**KEYWORDS_INDEX.md Updates**:
```markdown
## K

**Keyword Related to Skill** (NEW):
- Skill: `/new-skill`
- Docs: `.claude/skills/new-skill/SKILL.md`
```

### Pattern 3: File Path Added

**REFERENCE.md Updates**:
```markdown
### Backend (C#)
- `Modules/{Module}/NewFolder/` - Folder purpose (NEW)
```

**KEYWORDS_INDEX.md Updates**:
```markdown
## K

**Keyword**:
- Files: `Modules/*/NewFolder/*` (NEW)
```

### Pattern 4: Constant Added

**REFERENCE.md Updates**:
```markdown
### New Constants Section (if needed)
- `ConstantName.VALUE` - Constant description (NEW)
```

**KEYWORDS_INDEX.md Updates**:
```markdown
## K

**Keyword**:
- Constant: `ConstantName.VALUE` (NEW)
```

## Formatting Rules

### REFERENCE.md Conventions

1. **Sections**: Use `##` for main sections, `###` for subsections
2. **Lists**: Use `-` for bullet points
3. **Paths**: Use backticks for paths (e.g., `Modules/Mod/Services/`)
4. **Constants**: Use backticks for constants (e.g., `ModuleNames.MOD`)
5. **NEW markers**: Add `(NEW)` suffix for recent additions (remove after 1 month)
6. **Last Updated**: Update date at top when making changes

### KEYWORDS_INDEX.md Conventions

1. **Alphabetical**: Organize by first letter (## A, ## B, etc.)
2. **Bold keywords**: Use `**Keyword**:` format
3. **Categories**: Skill, Files, Docs, Pattern, Events, Constant, Module
4. **Indentation**: Use `-` for category items
5. **Cross-references**: Use "See also: X, Y" for related keywords
6. **NEW markers**: Add `(NEW)` suffix for recent additions

## Important Rules

- ✅ Always update "Last Updated" date
- ✅ Maintain alphabetical order within sections
- ✅ Use consistent formatting (backticks for code)
- ✅ Add NEW markers for recent additions
- ✅ Include descriptions, not just names
- ✅ Update both documents when adding new concepts
- ✅ Use cross-references in KEYWORDS_INDEX
- ❌ Don't break existing links
- ❌ Don't skip alphabetical sections
- ❌ Don't add entries without descriptions
- ❌ Don't forget to remove OLD NEW markers (after 1 month)

## Integration with Other Skills

**After creating new skill**:
```bash
# 1. Create skill
/backend-validator ModValidator Name,Description required

# 2. Update reference docs (this skill)
/doc-update-reference REFERENCE new-command "/backend-validator - Generate FluentValidation validator"
/doc-update-reference KEYWORDS_INDEX new-keyword "validation → /backend-validator, Modules/*/Validators/"
```

**After adding new module**:
```bash
# 1. Update REFERENCE.md with module constants and paths
/doc-update-reference REFERENCE new-constant "ModuleNames.VALIDATION - Validation module"
/doc-update-reference REFERENCE new-path "Modules/Validation/Services/ - Validation services"

# 2. Update KEYWORDS_INDEX.md with module keywords
/doc-update-reference KEYWORDS_INDEX new-keyword "validation module → Modules/Validation/"
```

## Cleanup Strategy

**Monthly cleanup** (remove stale NEW markers):
```bash
# Use doc-cleanup skill to remove (NEW) markers older than 1 month
/doc-cleanup REFERENCE remove-new-markers 30days
/doc-cleanup KEYWORDS_INDEX remove-new-markers 30days
```

## Validation Checklist

After updating, verify:

- [ ] "Last Updated" date changed
- [ ] Alphabetical order maintained
- [ ] Formatting is consistent (backticks for code)
- [ ] Descriptions included (not just names)
- [ ] NEW markers added for new entries
- [ ] Cross-references added to KEYWORDS_INDEX
- [ ] No broken paths or references
- [ ] Both documents updated if adding new concept

## Reference Examples

**Good Entry (REFERENCE.md)**:
```markdown
- `Modules/Mod/Validators/` - FluentValidation validators for mod entities (NEW)
```

**Bad Entry (REFERENCE.md)**:
```markdown
- Validators folder  ❌ (No backticks, no full path, no description)
```

**Good Entry (KEYWORDS_INDEX.md)**:
```markdown
**Validation** (NEW):
- Skill: `/backend-validator`
- Files: `Modules/*/Validators/*Validator.cs`
- Docs: `.claude/skills/backend-validator/SKILL.md`
- See also: FluentValidation, Error Handling
```

**Bad Entry (KEYWORDS_INDEX.md)**:
```markdown
Validation: backend-validator  ❌ (No bold, no categories, no full info)
```

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial doc-update-reference skill

**How to update this skill**:
1. If REFERENCE.md structure changes, update section patterns
2. If KEYWORDS_INDEX.md format changes, update entry templates
3. If new reference types emerge, add to EntryType options
4. Update formatting rules as conventions evolve
