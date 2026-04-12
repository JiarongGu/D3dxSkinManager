# No Global Memory for Project Rules

**NEVER use global memory (`~/.claude/projects/*/memory/`) for project-related information.**

Global memory is reserved ONLY for user-specific preferences (role, communication style, personal settings).

All workflow feedback, conventions, corrections, and project rules go in `.claude/rules/*.md` — this is stated in CLAUDE.md Section 5 and is non-negotiable.

## What goes where

| Information type | Location |
|---|---|
| Workflow corrections ("don't do X") | `.claude/rules/*.md` |
| Coding conventions | `.claude/rules/*.md` |
| Project decisions | `.claude/rules/*.md` |
| User role / communication style | `~/.claude/projects/*/memory/` |
