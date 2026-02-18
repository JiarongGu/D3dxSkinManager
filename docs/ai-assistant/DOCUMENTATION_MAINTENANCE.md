# Documentation Maintenance Guide for AI Assistants

> **🤖 FOR AI ASSISTANTS:** ⭐⭐⭐ **CRITICAL** - How to maintain documentation across sessions

**Purpose:** Ensure documentation stays current and useful by updating it as you work.

**Last Updated:** 2026-02-17

---

## 🚨 Critical Principle

**If you learned it, document it. Future AI sessions depend on you!**

Documentation is not a one-time task. It's a living system that evolves with the codebase. As an AI assistant, you are responsible for keeping it current.

---

## Table of Contents

1. [Documentation Update Triggers](#documentation-update-triggers)
2. [Which Files to Update](#which-files-to-update)
3. [Update Procedures](#update-procedures)
4. [Token Optimization](#token-optimization)
5. [Quality Standards](#quality-standards)
6. [Common Maintenance Scenarios](#common-maintenance-scenarios)

---

## Documentation Update Triggers

### Automatic Triggers (ALWAYS Update)

| Trigger | Update Required | Files to Update |
|---------|----------------|-----------------|
| Created new class/service | ✅ Required | `KEYWORDS_INDEX.md`, `CHANGELOG.md` |
| Added new feature | ✅ Required | `CHANGELOG.md`, create `features/FEATURE_NAME.md` |
| Fixed a bug | ✅ Required | `CHANGELOG.md`, possibly `TROUBLESHOOTING.md` |
| Created new file | ✅ Required | `KEYWORDS_INDEX.md` |
| Added new method/function | ✅ Required | `KEYWORDS_INDEX.md` (if important) |
| Changed project structure | ✅ Required | `KEYWORDS_INDEX.md`, `core/PROJECT_STRUCTURE.md` |
| Discovered new pattern | ✅ Required | `GUIDELINES.md` |
| Solved issue after >5 min | ✅ Required | `TROUBLESHOOTING.md` |

### Situational Triggers (Update If Applicable)

| Situation | Consider Updating | Files |
|-----------|------------------|-------|
| Added new dependency | If significant | `CHANGELOG.md`, `core/PROJECT_OVERVIEW.md` |
| Changed workflow | If permanent | `WORKFLOWS.md` |
| Found better approach | If repeatable | `GUIDELINES.md` |
| User provided feedback | If affects future work | `AI_GUIDE.md`, relevant docs |

---

## Which Files to Update

### Quick Decision Tree

```
Did you create/modify code?
├─ Yes → Update CHANGELOG.md
│   ├─ New file/class? → Update KEYWORDS_INDEX.md
│   ├─ New feature? → Create features/FEATURE_NAME.md
│   └─ Fixed bug? → Update TROUBLESHOOTING.md (if applicable)
│
└─ Did you solve a problem?
    ├─ Took >5 min to find info? → Update KEYWORDS_INDEX.md
    ├─ Encountered error? → Update TROUBLESHOOTING.md
    └─ Found pattern? → Update GUIDELINES.md
```

### File Responsibilities

#### `CHANGELOG.md` - Change History
**Update When:**
- Adding new features
- Fixing bugs
- Changing functionality
- Adding dependencies
- Refactoring major components

**Don't Update For:**
- Documentation-only changes (update "Last Updated" in affected doc instead)
- Typo fixes
- Comment changes

**Format:**
```markdown
## [Unreleased]

### Added
- Feature description [#issue-number]
  - Implementation detail
  - File: `path/to/file.cs:123`

### Fixed
- Bug description
  - Root cause
  - Solution applied
  - File: `path/to/file.cs:456`
```

---

#### `KEYWORDS_INDEX.md` - Quick Lookup
**Update When:**
- Creating new class, interface, or service
- Adding new important method
- Creating new file
- Adding new concept/term
- Spent >5 min searching for something

**Don't Update For:**
- Private methods
- Implementation details
- Temporary code

**Format:**
```markdown
### Services

- **ServiceName** → `path/to/ServiceName.cs`
  - ImportantMethod → `:45`
  - AnotherMethod → `:78`
```

**Critical:** Include line numbers for quick navigation!

---

#### `TROUBLESHOOTING.md` - Known Issues
**Update When:**
- Encountering error for first time
- Finding solution to tricky problem
- Debugging took >5 minutes
- User reports issue

**Don't Update For:**
- One-off mistakes
- Obvious errors (typos)
- User-specific environment issues

**Format:**
```markdown
### Error: Short Description

**Symptom:**
Exact error message or behavior

**Root Cause:** Why it happened

**Solution:**
```code
// Fix here
```

**File:** `path/to/file.cs:123`

**History:** Fixed on YYYY-MM-DD
```

---

#### `GUIDELINES.md` - Coding Patterns
**Update When:**
- Discovering repeatable pattern
- Finding better way to do something
- User corrects your approach
- Preventing common mistake

**Don't Update For:**
- One-off solutions
- Project-specific hacks
- Temporary workarounds

**Format:**
```markdown
### Category

#### ✅ DO: Pattern Name
```language
// Good example
```

#### ❌ DON'T: Anti-pattern Name
```language
// Bad example
```

**Reason:** Explanation
```

---

#### `WORKFLOWS.md` - Procedures
**Update When:**
- Creating new step-by-step procedure
- Workflow changes permanently
- Adding new common task

**Don't Update For:**
- Temporary process changes
- Experimental workflows

**Format:**
```markdown
### Task Name

**When:** Situation description

**Steps:**
1. First step with command
   ```bash
   command here
   ```
2. Second step
3. Third step

**Verification:**
```bash
# How to verify it worked
```

**Files Affected:**
- `path/to/file.cs` - Description
```

---

#### Feature Documentation (`features/FEATURE_NAME.md`)
**Create When:**
- Implementing new feature (>100 LOC)
- Feature has multiple components
- Feature will be referenced often

**Format:**
```markdown
# Feature Name

**Purpose:** One sentence description
**Status:** ✅ Complete / ⏳ In Progress / 📋 Planned
**Location:** `path/to/main/file.cs:123`

## Overview

Brief description

## Usage

Code examples

## Implementation Details

Technical details

## Files

- `file1.cs:123` - Description
- `file2.tsx:45` - Description

## Related

- [Other Feature](OTHER_FEATURE.md)
- [Core Doc](../core/ARCHITECTURE.md#section)
```

---

## Update Procedures

### Standard Update Process

1. **Make Code Changes**
   - Write/modify code
   - Test changes
   - Verify build succeeds

2. **Update Documentation (Before Commit)**
   - Update `CHANGELOG.md` (required for all changes)
   - Update `KEYWORDS_INDEX.md` (if new files/classes)
   - Create/update feature docs (if applicable)
   - Update `TROUBLESHOOTING.md` (if solved issue)
   - Update `GUIDELINES.md` (if found pattern)
   - Update "Last Updated" dates in modified docs

3. **Verify Documentation**
   - Links work
   - File paths correct
   - Line numbers accurate
   - Format consistent

4. **Ask for Commit Approval**
   - Include both code AND doc changes in same commit

---

### Quick Updates (During Session)

For minor updates during active coding:

```markdown
**Quick Update Checklist:**
- [ ] CHANGELOG.md entry drafted
- [ ] KEYWORDS_INDEX.md updated (if new files)
- [ ] "Last Updated" date changed
```

Keep updates minimal but consistent.

---

### Major Updates (New Features)

For significant features:

1. **Plan Documentation First**
   - Decide what docs needed
   - Create outline
   - Identify affected files

2. **Implement Feature**
   - Write code
   - Test

3. **Create Feature Documentation**
   - Create `features/FEATURE_NAME.md`
   - Include examples
   - Link to implementation files

4. **Update Supporting Docs**
   - `CHANGELOG.md` - Document addition
   - `KEYWORDS_INDEX.md` - Add lookup entries
   - `README.md` - Update feature list (if applicable)
   - `AI_GUIDE.md` - Update if affects AI workflows

5. **Cross-Reference**
   - Link from feature doc to core docs
   - Link from core docs to feature doc
   - Update indexes

---

## Token Optimization

### Efficient Documentation

**Goal:** Maximize usefulness while minimizing token usage

#### DO:
- ✅ Use concise language
- ✅ Include code examples (worth 1000 words)
- ✅ Link to files instead of repeating content
- ✅ Use tables for structured data
- ✅ Keep line numbers updated

#### DON'T:
- ❌ Repeat information across files
- ❌ Include unnecessary explanations
- ❌ Write long prose paragraphs
- ❌ Duplicate code samples
- ❌ Over-document obvious code

---

### Cross-Referencing Strategy

Instead of duplicating content, link to canonical source:

```markdown
<!-- ❌ Bad - Duplicates content -->
## How to Add Service

1. Create interface in Services/
2. Create implementation
3. Register in Program.cs
... (repeated from WORKFLOWS.md)

<!-- ✅ Good - Links to source -->
## How to Add Service

See [Adding Backend Service](ai-assistant/WORKFLOWS.md#adding-backend-service) for detailed steps.
```

---

### KEYWORDS_INDEX.md Optimization

This file is critical for token efficiency:

**Purpose:** Enable O(1) lookup without loading large docs

**Best Practices:**
- Include exact file paths
- Include line numbers
- Group by category
- Keep entries one-line
- Update immediately when files change

**Example:**
```markdown
<!-- ✅ Good - Fast lookup -->
- **ModService** → `D3dxSkinManager/Services/ModService.cs:14`
  - GetAllModsAsync → `:47`
  - LoadModAsync → `:66`

<!-- ❌ Bad - Requires explanation -->
- **ModService** - This is the main service for handling mod operations.
  It's located in the Services folder. See the file for details.
```

---

## Quality Standards

### Documentation Quality Checklist

Before finalizing any documentation update:

#### Content Quality
- [ ] Information is accurate
- [ ] Code examples work
- [ ] File paths are correct
- [ ] Line numbers are current
- [ ] Links are valid

#### Format Quality
- [ ] Follows established format
- [ ] Markdown renders correctly
- [ ] Code blocks have language tags
- [ ] Headers use consistent hierarchy
- [ ] Tables are aligned

#### Maintenance Quality
- [ ] "Last Updated" date changed
- [ ] Related docs cross-referenced
- [ ] Removed outdated information
- [ ] Added to appropriate indexes

---

### Common Quality Issues

#### Issue: Outdated Line Numbers

**Problem:** Line numbers in docs don't match actual files

**Prevention:**
```markdown
<!-- ✅ Good - Specify range or function -->
- LoadModAsync → `ModService.cs:66-85`
- In the LoadModAsync method

<!-- ❌ Bad - Single line number (fragile) -->
- LoadModAsync → `ModService.cs:66`
```

**Fix:** Update line numbers whenever editing referenced files

---

#### Issue: Broken Links

**Problem:** Links to files that moved or don't exist

**Prevention:**
- Use relative paths: `../core/ARCHITECTURE.md`
- Verify links after creating
- Check links after file renames

**Detection:**
```bash
# Check for broken markdown links (manual)
grep -r "\[.*\](.*\.md)" docs/
```

---

#### Issue: Duplicate Information

**Problem:** Same info in multiple files, gets out of sync

**Prevention:**
- Identify canonical source for each topic
- Link to canonical source instead of duplicating
- Use "See [File](link) for details" pattern

**Example:**
- Commands → REFERENCE.md (canonical)
- Architecture → core/ARCHITECTURE.md (canonical)
- All other files link to these

---

## Common Maintenance Scenarios

### Scenario 1: Added New Service Class

**Actions Required:**
1. Update `KEYWORDS_INDEX.md`:
   ```markdown
   ### Services
   - **NewService** → `D3dxSkinManager/Services/NewService.cs`
     - MainMethod → `:23`
     - OtherMethod → `:45`
   ```

2. Update `CHANGELOG.md`:
   ```markdown
   ### Added
   - NewService for handling XYZ functionality
     - Implemented MainMethod and OtherMethod
     - Uses SQLite for persistence
     - File: `D3dxSkinManager/Services/NewService.cs`
   ```

3. Create feature doc (if complex):
   - Create `docs/features/NEW_SERVICE.md`
   - Document usage and examples

4. Update "Last Updated" in all modified docs

---

### Scenario 2: Fixed Bug After 10-Minute Debug Session

**Actions Required:**
1. Update `TROUBLESHOOTING.md`:
   ```markdown
   ### Error: Description of Error

   **Symptom:** What user sees
   **Root Cause:** Why it happened
   **Solution:** How to fix
   **File:** `path/to/file.cs:123`
   **History:** Fixed on 2026-02-17
   ```

2. Update `CHANGELOG.md`:
   ```markdown
   ### Fixed
   - Bug where XYZ caused error
     - Root cause: ABC
     - Solution: DEF
     - File: `path/to/file.cs:123`
   ```

3. Update "Last Updated" dates

---

### Scenario 3: Refactored Major Component

**Actions Required:**
1. Update `KEYWORDS_INDEX.md`:
   - Update file paths if changed
   - Update line numbers
   - Add new classes/methods

2. Update `CHANGELOG.md`:
   ```markdown
   ### Changed
   - Refactored ComponentName for better performance
     - Moved logic from A to B
     - Split large method into smaller methods
     - Files: `file1.cs`, `file2.cs`
   ```

3. Update feature docs:
   - Update code examples
   - Update implementation details
   - Update file references

4. Update `core/ARCHITECTURE.md` if structure changed

5. Update "Last Updated" dates

---

### Scenario 4: Learned Something After 5+ Minute Search

**Actions Required:**
1. Update `KEYWORDS_INDEX.md`:
   - Add entry for what you searched for
   - Link to file/line where found
   - Add to appropriate category

2. Consider updating relevant guide:
   - If it's a pattern → `GUIDELINES.md`
   - If it's a command → `REFERENCE.md`
   - If it's a concept → `core/` docs

**Example:**
You spent 5 minutes finding where mods are loaded. Add to KEYWORDS_INDEX.md:
```markdown
- **Mod Loading** → `ModService.cs:66` (LoadModAsync method)
```

---

### Scenario 5: User Corrected Your Approach

**Actions Required:**
1. Update `GUIDELINES.md`:
   - Add DO/DON'T example
   - Explain why correct way is better

2. Update `AI_GUIDE.md` if it affects general AI behavior:
   - Add to "7 Critical Rules" if critical
   - Add to relevant section

3. Update `WORKFLOWS.md` if it changes procedures

**Example:**
User said "don't commit without asking." Add to GUIDELINES.md:
```markdown
### ❌ NEVER DO: Commit Without Permission

**Wrong Approach:** Creating commits automatically

**Correct Approach:** Always ask user first:
```bash
# Ask user
"Ready to commit?"

# Wait for approval

# Then commit
git commit -m "message"
```
```

---

## Session-End Checklist

Before ending your session:

### Code Changes
- [ ] All code changes tested
- [ ] Build succeeds
- [ ] No TypeScript errors
- [ ] No C# warnings

### Documentation Updates
- [ ] `CHANGELOG.md` updated for all changes
- [ ] `KEYWORDS_INDEX.md` updated for new files
- [ ] Feature docs created/updated if needed
- [ ] `TROUBLESHOOTING.md` updated if solved issues
- [ ] `GUIDELINES.md` updated if found patterns
- [ ] All "Last Updated" dates changed

### Quality Check
- [ ] File paths correct
- [ ] Line numbers accurate
- [ ] Links work
- [ ] Code examples tested
- [ ] Format consistent

### Commit
- [ ] User approval received
- [ ] Correct git branch
- [ ] Commit message descriptive
- [ ] Both code AND docs committed together

---

## Special Cases

### When to Update AI_GUIDE.md

Update `AI_GUIDE.md` only for:
- New critical rules discovered
- Major workflow changes
- New documentation files added
- RAG strategy improvements

**Don't update for:**
- Minor changes
- Feature-specific info (use feature docs)
- Temporary processes

---

### When to Create New Documentation Files

Create new doc file when:
- Existing files getting too large (>500 lines)
- New category of information
- New feature significant enough (>200 LOC)

**Process:**
1. Create file
2. Update `docs/README.md` index
3. Update `AI_GUIDE.md` documentation map
4. Update `KEYWORDS_INDEX.md`
5. Add cross-references

---

### When to Remove Documentation

Remove docs when:
- Feature removed from codebase
- Information outdated and no longer relevant
- Superseded by better documentation

**Process:**
1. Remove file
2. Update all links pointing to it
3. Add removal note to `CHANGELOG.md`
4. Remove from indexes

---

## Tips for Efficient Maintenance

### 1. Update as You Go

Don't wait until end of session. Update docs immediately after changes:
- Just created class? → Update KEYWORDS_INDEX.md now
- Just fixed bug? → Update TROUBLESHOOTING.md now
- Just found pattern? → Update GUIDELINES.md now

---

### 2. Use Templates

Copy format from existing entries:
- CHANGELOG.md → Copy last entry format
- TROUBLESHOOTING.md → Copy error template
- Feature docs → Copy existing feature doc structure

---

### 3. Keep Changes Minimal

Only update what changed:
- Changed one method? → Update that line number
- Added one class? → Add one entry
- Fixed one bug? → One troubleshooting entry

Don't rewrite entire files unless necessary.

---

### 4. Verify Before Committing

Quick verification:
```bash
# Check markdown renders (if available)
markdown-cli docs/CHANGELOG.md

# Check for broken relative links
grep -r "](../" docs/

# Check for TODO markers
grep -r "TODO" docs/
```

---

## Maintenance Metrics

Track these to ensure docs stay healthy:

### Quality Indicators

**Good:**
- ✅ CHANGELOG.md updated in every commit
- ✅ KEYWORDS_INDEX.md entries match actual files
- ✅ No broken links
- ✅ Line numbers accurate
- ✅ "Last Updated" dates recent

**Needs Improvement:**
- ❌ CHANGELOG.md missing entries
- ❌ KEYWORDS_INDEX.md has wrong line numbers
- ❌ Broken links in docs
- ❌ "Last Updated" dates >1 month old for active files
- ❌ TODO markers left in docs

---

## Emergency Documentation Fixes

If docs are severely out of sync:

### 1. Audit Phase
```bash
# Find all code files
find D3dxSkinManager -name "*.cs"
find D3dxSkinManager.Client/src -name "*.ts" -o -name "*.tsx"

# Compare to KEYWORDS_INDEX.md
# List missing files
```

### 2. Update Phase
- Update KEYWORDS_INDEX.md with missing files
- Update line numbers for existing entries
- Fix broken links

### 3. Verification Phase
- Verify all links work
- Verify all file paths exist
- Verify line numbers point to correct code

### 4. Prevention
- Commit to updating docs with every code change
- Review this guide regularly

---

## Remember

**Documentation is not optional. It's critical for:**
- Future AI sessions (they start fresh each time)
- Human developers (onboarding, reference)
- You (finding information quickly)
- Project success (maintainability)

**Update docs BEFORE asking user for commit approval.**

**If you learned it, document it. Future sessions depend on you!**

---

*This guide is maintained by AI assistants. Follow it strictly!*

*Last updated: 2026-02-17*
