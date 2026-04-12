---
name: doc-monitor
description: Audit documentation health. Detects broken references, redundancy, stale skills, and version mismatches.
---

# Doc Monitor

**Format**: `/doc-monitor [CheckType] [Scope]`
**CheckTypes**: `redundancy` | `broken-links` | `outdated-skills` | `consistency` | `all`
**Scope**: `all` | `docs/` | `.claude/skills/` | specific file

## Action

Run the checks below and report issues grouped by severity (CRITICAL > HIGH > MEDIUM > LOW).

## Checks

### broken-links
- Grep docs for markdown links `[text](path)` — verify each path exists
- Grep for skill references `/skill-name` — verify `.claude/skills/{name}/SKILL.md` exists

### redundancy
- Compare docs pairwise for overlapping content (>50% overlap = HIGH)
- Check if skill SKILL.md duplicates content from docs/ files

### outdated-skills
- For each skill referencing a file path, verify the file still exists
- Check if skills reference patterns that no longer exist in code

### consistency
- Count skills in `.claude/skills/` vs entries in AI_GUIDE.md skills table
- Verify "Last Updated" dates are plausible

## Output

Short list of issues with file:line, severity, and one-line fix suggestion. No health scores, no pseudo-code, no checklists.
