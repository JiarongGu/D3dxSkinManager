# {Rule Title — imperative, not historical}

**One-sentence summary of what is enforced.**

## Why

The reason this rule exists — a past incident, constraint, or strong preference. Future sessions need this to judge edge cases instead of blindly following the rule.

## How to Apply

When this rule kicks in, and what to do. Cover:
- The trigger (file pattern, task type, keyword)
- The prescribed action
- Edge cases where the rule *doesn't* apply

## Examples (optional)

Short concrete cases — the shape of compliance and the shape of violation.

## Related

- Links to other rules or docs that interact with this one

---

**Usage notes when creating a new rule:**

1. Copy this file to `.claude/knowledge/{kebab-case-name}.md` (situational — the default) or `.claude/rules/{name}.md` (only for a universal-workflow rule needed on every task). `node devtools/new-rule.mjs <name>` does this for you.
2. Replace content
3. **Add one row to [RULES_INDEX.md](RULES_INDEX.md)** — otherwise the rule is invisible to the discovery workflow
4. Delete this "Usage notes" section from your copy
5. Rule names describe *what is enforced*, not the incident that caused them (e.g. `enum-serialization.md`, not `fix-apr-2026-progress-bug.md`)
