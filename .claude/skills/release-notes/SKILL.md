---
name: release-notes
description: Generate release notes from git log between tags using conventional commit prefixes (feat/fix/chore/docs)
disable-model-invocation: false
---

# Release Notes Generator

Generate release notes from git history using conventional commit prefixes. Used before releases to preview what goes into the GitHub release body.

## Arguments

**Format**: `/release-notes [from-tag] [to-ref]`

**Examples**:
```
/release-notes                  # Auto-detect: previous tag → HEAD
/release-notes v1.5             # From v1.5 → HEAD
/release-notes v1.5 v1.6        # From v1.5 → v1.6
```

**Parameters**:
- `from-tag` (optional) — Starting tag. If omitted, auto-detects previous tag.
- `to-ref` (optional) — Ending ref (tag, branch, SHA). Defaults to `HEAD`.

## What This Skill Does

1. Detects previous tag (if not specified)
2. Extracts commits between tags
3. Categorizes by conventional commit prefix
4. Outputs formatted markdown release notes
5. Optionally writes to `release-notes.md`

## Commit Categorization

| Prefix | Category | Included |
|--------|----------|----------|
| `feat:` | New Features | Yes |
| `fix:` | Bug Fixes | Yes |
| `chore:` | — | Skipped (non-user-facing) |
| `docs:` | — | Skipped (non-user-facing) |
| `refactor:` | — | Skipped (non-user-facing) |
| `style:` | — | Skipped (non-user-facing) |
| `perf:` | Performance | Yes (if present) |
| `test:` | — | Skipped (non-user-facing) |
| `ci:` | — | Skipped (non-user-facing) |
| Other | Other Changes | Yes |

## Steps to Execute

1. **Determine tag range**:
   ```bash
   # List tags sorted by version
   git tag --sort=-version:refname | head -5

   # If from-tag not given, use the most recent tag
   # If to-ref not given, use HEAD
   ```

2. **Extract commits**:
   ```bash
   git log <from-tag>..<to-ref> --pretty=format:"%s" --no-merges
   ```

3. **Categorize and format** — Group commits by prefix, strip prefix from display:
   ```markdown
   ### New Features

   - add mod analyzer tool + fix enum serialization + UI polish
   - add file cleanup tool + lean skill-loader workflow

   ### Bug Fixes

   - thumbnail cleanup scans sub-folders and removes empty directories
   - screen capture not starting after heavy tasks

   ---

   ### Installation

   1. Download `D3dxSkinManager-v1.6-win-x64.zip`
   2. Extract to a folder
   3. Run `D3dxSkinManager.exe` (the launcher)
   4. The launcher will automatically install .NET 10 runtime if needed

   **Requirements**: Windows 10/11 (x64)
   ```

4. **Output**: Print the formatted notes and offer to save to `release-notes.md`

## Integration Points

This same logic runs in two other places — keep them in sync:

| Location | Usage |
|----------|-------|
| `.github/workflows/release.yml` Step 11 | Auto-generates notes during CI release |
| `release.ps1` `Get-ReleaseNotes` function | Auto-generates notes during local release |

When updating the categorization logic, update all three locations.

## Important Rules

- Only `feat:` and `fix:` commits are user-facing — skip `chore:`, `docs:`, `refactor:`, `style:`, `test:`, `ci:`
- Strip the prefix from display text (`feat: add X` → `- add X`)
- Multi-part commit messages (e.g., `feat: add X + fix Y`) stay as single bullet
- Always append installation instructions at the bottom
- If no user-facing commits found, output "No notable changes in this release."

## Reference

- GitHub Actions workflow: `.github/workflows/release.yml` (Step 11)
- Local release script: `release.ps1` (`Get-ReleaseNotes` function)
- Conventional commits: https://www.conventionalcommits.org/

## Evolution Note

**Version History**:
- v1.0 (2026-04-13): Initial version — git-log-based generation replacing CHANGELOG.md extraction

**How to update this skill**:
1. Update categorization rules in this file
2. Mirror changes to `.github/workflows/release.yml` Step 11
3. Mirror changes to `release.ps1` `Get-ReleaseNotes`
4. Update version history above
