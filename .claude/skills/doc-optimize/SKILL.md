---
name: doc-optimize
description: Shrink oversized docs (>30KB) by extracting sections, condensing examples to links, or splitting into focused files.
---

# Doc Optimize

**Format**: `/doc-optimize <Document> <Operation> <Details>`

## Operations

| Operation | What it does |
|-----------|-------------|
| `analyze` | Report file sizes, identify sections >5KB, suggest extractions |
| `extract-section` | Move a large section to its own file, replace with summary + link |
| `condense-examples` | Replace full code blocks with skill references or links to source files |
| `split-file` | Split large doc into focused subdocs with an index file |

## Size Targets

- Guides: <30KB (optimal for LLM context loading)
- References: <20KB
- Technical patterns: <15KB

## Action

1. For `analyze`: list files exceeding threshold with section sizes
2. For mutations: create the new file(s), update the original with links, update KEYWORDS_INDEX.md if paths changed
3. Report what changed in 3-5 lines
