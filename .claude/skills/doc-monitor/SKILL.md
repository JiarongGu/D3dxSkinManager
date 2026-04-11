---
name: doc-monitor
description: Use to audit documentation health. Detects broken file references, redundant docs, stale skill listings, and inconsistent versions across the docs folder.
---

# Documentation Monitor

Monitor documentation health by detecting redundancy, inconsistency, outdated content, and broken references.

**Purpose**: As the codebase and skills evolve, documentation can become outdated, redundant, or inconsistent. This skill performs health checks to identify issues before they become problems.

## Arguments

**Format**: `/doc-monitor <CheckType> <Scope>`

**Example**:
```
/doc-monitor redundancy all
/doc-monitor broken-links docs/
/doc-monitor outdated-skills .claude/skills/
/doc-monitor consistency AI_GUIDE,CODE_GENERATION
```

**Parameters**:
- `CheckType` - Type of check (redundancy, broken-links, outdated-skills, consistency, version-sync, all)
- `Scope` - Where to check (all, docs/, .claude/skills/, specific-file)

## What This Skill Does

1. **Detects redundancy**:
   - Finds duplicate content between docs
   - Identifies skills vs docs overlap
   - Reports redundancy percentage

2. **Finds broken references**:
   - Checks file path references
   - Validates skill name references
   - Checks cross-document links

3. **Identifies outdated content**:
   - Skills referencing deleted patterns
   - Docs referencing removed skills
   - Deprecated markers older than retention period

4. **Monitors consistency**:
   - Version numbers in sync
   - Last updated dates accurate
   - Skill listings complete

5. **Reports findings**:
   - Categorizes issues by severity
   - Provides fix recommendations
   - Outputs actionable report

## Check Types

### 1. Redundancy Check

**What it checks**:
- Content overlap between documentation files
- Skills vs docs redundancy (skills should automate, docs should explain WHY)
- Duplicate patterns in multiple places

**Output**:
```markdown
## Redundancy Report

### High Redundancy (>50%)
- `docs/ai-assistant/GUIDELINES.md` ↔ `docs/AI_GUIDE.md` (65% overlap)
  - Recommendation: Consolidate or remove GUIDELINES.md
  - Overlapping sections: Skills usage, Workflow patterns

### Medium Redundancy (20-50%)
- `docs/core/ADVANCED_PATTERNS.md` ↔ `.claude/skills/backend-service/SKILL.md` (30% overlap)
  - Recommendation: ADVANCED_PATTERNS should explain WHY, skill shows HOW
  - Overlapping: DI injection pattern

### Skills Coverage
- 12 skills created
- 85% of docs/ai-assistant/WORKFLOWS.md is now automated (GOOD - deprecated correctly)
- 0% of docs/core/ADVANCED_PATTERNS.md is automatable (GOOD - non-automatable patterns)
```

### 2. Broken Links Check

**What it checks**:
- File path references that don't exist
- Skill name references that don't exist
- Markdown links to missing files
- Cross-references to removed sections

**Output**:
```markdown
## Broken Links Report

### Missing Files (CRITICAL)
- `docs/AI_GUIDE.md:42` references `docs/ai-assistant/WORKFLOWS.md` (DELETED)
  - Fix: Update reference to `.claude/skills/README.md`

### Invalid Skill References (HIGH)
- `docs/CODE_GENERATION.md:105` references `/old-skill` (doesn't exist)
  - Fix: Update to `/new-skill` or remove

### Broken Internal Links (MEDIUM)
- `docs/REFERENCE.md:78` links to `#validation-section` (section removed)
  - Fix: Update link or restore section
```

### 3. Outdated Skills Check

**What it checks**:
- Skills referencing deleted files
- Skills with pattern examples that no longer exist
- Skills with outdated reference examples

**Output**:
```markdown
## Outdated Skills Report

### Skills Referencing Deleted Files (HIGH)
- `.claude/skills/backend-service/SKILL.md` references `docs/ai-assistant/WORKFLOWS.md` (DELETED)
  - Fix: Update to reference `docs/core/ADVANCED_PATTERNS.md`

### Skills with Missing Examples (MEDIUM)
- `.claude/skills/pattern-finder/SKILL.md` references `Modules/Old/Services/OldService.cs` (doesn't exist)
  - Fix: Update to current example or remove

### Skills Needing Version Updates (LOW)
- `.claude/skills/backend-facade/SKILL.md` last updated 2026-03-01 (>1 month old)
  - Check: Verify pattern is still current
```

### 4. Consistency Check

**What it checks**:
- Version numbers match across docs
- Last updated dates are accurate
- Skills listed in README match actual skills
- Cross-references are bidirectional

**Output**:
```markdown
## Consistency Report

### Version Mismatches (HIGH)
- `docs/AI_GUIDE.md` version: v3.7
- `docs/CODE_GENERATION.md` version: v1.2
  - Issue: Versions should be in sync (both reference same system)
  - Fix: Update CODE_GENERATION.md to v3.7 or explain versioning strategy

### Skill Listing Mismatches (MEDIUM)
- `.claude/skills/README.md` lists 12 skills
- `docs/AI_GUIDE.md` skills table has 11 entries
  - Missing: `/doc-monitor` skill
  - Fix: Add missing skill to AI_GUIDE.md

### Stale Dates (LOW)
- `docs/REFERENCE.md` "Last Updated: 2026-03-15" but file modified 2026-04-11
  - Fix: Update "Last Updated" date
```

### 5. Version Sync Check

**What it checks**:
- Skills evolution notes match actual code patterns
- Documentation versions reflect actual changes
- Changelog entries match actual updates

**Output**:
```markdown
## Version Sync Report

### Skill vs Code Mismatches (HIGH)
- `/backend-service` skill pattern uses IProfileEventBus
- Some services in `Modules/*/Services/` don't inject IProfileEventBus
  - Issue: Skill pattern doesn't match all code
  - Fix: Update old services or update skill pattern

### Missing Changelog Entries (MEDIUM)
- `docs/AI_GUIDE.md` version updated v3.6 → v3.7
- Changelog has no v3.7 entry
  - Fix: Add changelog entry for v3.7

### Skill Version History Gaps (LOW)
- `.claude/skills/backend-facade/SKILL.md` evolution note shows v1.0
- Pattern has evolved but version not updated
  - Fix: Add v1.1 entry if pattern changed
```

### 6. All Checks

Runs all checks above and generates comprehensive report.

## Severity Levels

**CRITICAL**: Broken functionality
- Broken file path references
- Missing required files
- Invalid skill names

**HIGH**: Significant issues
- High redundancy (>50%)
- Outdated skill patterns
- Version mismatches

**MEDIUM**: Moderate issues
- Medium redundancy (20-50%)
- Stale update dates
- Missing skill listings

**LOW**: Minor issues
- Low redundancy (<20%)
- Cosmetic inconsistencies
- Informational findings

## Report Format

```markdown
# Documentation Health Report
**Generated:** 2026-04-11
**Scope:** all
**Check Type:** all

---

## Summary

- **CRITICAL**: 2 issues
- **HIGH**: 5 issues
- **MEDIUM**: 8 issues
- **LOW**: 3 issues

---

## CRITICAL Issues

### 1. Broken File Reference
- **File**: `docs/AI_GUIDE.md:42`
- **Issue**: References deleted file `docs/ai-assistant/WORKFLOWS.md`
- **Fix**: Update to `.claude/skills/README.md`
- **Command**: `/doc-update-guide AI_GUIDE update-reference "Replace WORKFLOWS.md with skills/README.md"`

---

## HIGH Issues

### 1. High Redundancy Detected
- **Files**: `docs/ai-assistant/GUIDELINES.md` ↔ `docs/AI_GUIDE.md`
- **Redundancy**: 65%
- **Fix**: Consolidate or delete GUIDELINES.md
- **Command**: `/doc-cleanup GUIDELINES delete "Content moved to AI_GUIDE.md"`

---

## Recommendations

1. **Immediate Actions** (CRITICAL + HIGH):
   - Fix 2 broken references
   - Resolve 3 high redundancy cases
   - Update 2 outdated skill patterns

2. **Short-term Actions** (MEDIUM):
   - Update 5 stale dates
   - Add 3 missing skill listings
   - Fix 2 version mismatches

3. **Long-term Actions** (LOW):
   - Review skill versions quarterly
   - Update evolution notes as patterns change

---

## Health Score

**Overall**: 78/100 (Good)

- Content Accuracy: 85/100
- Reference Integrity: 70/100 (broken links)
- Consistency: 75/100
- Freshness: 80/100

**Grade**: B (Good health, some issues to address)
```

## Automation Suggestions

**Monthly scheduled checks**:
```bash
# Run comprehensive health check
/doc-monitor all all

# Generate report and review
# Address CRITICAL and HIGH issues immediately
# Plan MEDIUM and LOW issues for next sprint
```

**After major changes**:
```bash
# After adding skills
/doc-monitor consistency all

# After deleting docs
/doc-monitor broken-links all

# After pattern changes
/doc-monitor outdated-skills .claude/skills/
```

## Integration with Other Skills

**Typical workflow**:
```bash
# 1. Monitor for issues (this skill)
/doc-monitor all all

# 2. Fix high-priority issues
/doc-update-guide AI_GUIDE update-reference "Fix broken link"
/doc-cleanup REDUNDANT_DOC delete "Consolidated into AI_GUIDE"

# 3. Verify fixes
/doc-monitor all all
# Should show fewer issues
```

## Important Rules

- ✅ Run checks before major releases
- ✅ Address CRITICAL issues immediately
- ✅ Plan HIGH issues for current sprint
- ✅ Review MEDIUM issues monthly
- ✅ Batch LOW issues for quarterly cleanup
- ✅ Track health score over time
- ✅ Automate checks in CI/CD if possible
- ❌ Don't ignore CRITICAL issues
- ❌ Don't let redundancy exceed 50%
- ❌ Don't allow broken references to persist
- ❌ Don't skip consistency checks after changes

## Detection Algorithms

### Redundancy Detection

```python
# Pseudo-code for redundancy detection
def detect_redundancy(doc1, doc2):
    # Tokenize content
    tokens1 = tokenize(doc1)
    tokens2 = tokenize(doc2)

    # Calculate similarity (Jaccard index)
    intersection = set(tokens1) & set(tokens2)
    union = set(tokens1) | set(tokens2)
    similarity = len(intersection) / len(union) * 100

    # Categorize
    if similarity > 50:
        return "HIGH", similarity
    elif similarity > 20:
        return "MEDIUM", similarity
    else:
        return "LOW", similarity
```

### Broken Link Detection

```python
# Pseudo-code for broken link detection
def check_broken_links(doc_path):
    issues = []

    # Extract file references
    file_refs = extract_references(doc_path)

    for ref in file_refs:
        if not file_exists(ref.path):
            issues.append({
                "line": ref.line,
                "reference": ref.path,
                "severity": "CRITICAL"
            })

    return issues
```

### Consistency Detection

```python
# Pseudo-code for consistency check
def check_skill_listings():
    # Get actual skills
    actual_skills = list_skills(".claude/skills/")

    # Get listed skills in README
    readme_skills = extract_skills_from_readme()

    # Get listed skills in AI_GUIDE
    guide_skills = extract_skills_from_guide()

    # Find mismatches
    missing_in_readme = actual_skills - readme_skills
    missing_in_guide = actual_skills - guide_skills

    return {
        "missing_in_readme": missing_in_readme,
        "missing_in_guide": missing_in_guide
    }
```

## Validation Checklist

After running monitor:

- [ ] Report generated successfully
- [ ] All check types completed
- [ ] Severity levels assigned correctly
- [ ] Fix recommendations provided
- [ ] Health score calculated
- [ ] Issues categorized properly
- [ ] Commands suggested for fixes

## Reference Examples

**Good Health (Score: 90+)**:
- 0 CRITICAL issues
- 0-2 HIGH issues
- Few MEDIUM issues
- No broken references
- Low redundancy (<20%)

**Fair Health (Score: 70-89)**:
- 0 CRITICAL issues
- 3-5 HIGH issues
- Some redundancy (20-40%)
- Few broken references

**Poor Health (Score: <70)**:
- CRITICAL issues present
- Many HIGH issues
- High redundancy (>50%)
- Multiple broken references

## Evolution Note

**Version History**:
- v1.0 (2026-04-11): Initial doc-monitor skill

**How to update this skill**:
1. If new check types needed, add to CheckType options
2. If detection algorithms improve, update algorithm sections
3. If severity thresholds change, update severity levels
4. If report format evolves, update report template
