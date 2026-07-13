# Oversized files: accept reasonable oversize — split ONLY clean seams

**A file being large is not a bug. Split it only when a self-contained seam extracts cleanly and
behavior-preservingly; STOP at a reasonable size (~700 lines is fine). Never force an entangled
extraction — that trades one big file for two coupled files + indirection + regression risk.**

## Why

Line count is a *smell*, not a limit. The value of a split is **cohesion + readability + testability**,
not hitting a number. Extracting a cluster that is deeply entangled with its host (many dialog-state
setters, services, selection state threaded out as a big props/args surface) makes the code *worse*:
the "extracted" unit is now coupled to the parent through a wide interface, and you've added a file +
an indirection for no gain. (Grounded 2026-07-14: `ModList.tsx` went 932→671 via **three** clean
extractions, then stopped — the remaining `getContextMenuItems`/action-handler cluster is entangled, so
it stays in the component. User directive: "accept reasonable oversize".)

## How to apply — the bar a split must clear

Extract a seam ONLY when it is one of these **self-contained** shapes:

- **Pure static/stateless cluster** → a helper class/module. E.g. `ModAnalysisReportBuilder` (the
  duplicate/similar/conflict grouping — no instance state, moved verbatim).
- **Dumb, props-only subcomponent** (L1/L2 per `ui-component-layers.md`) → its own file. E.g.
  `ModListItem` (a row), `CategoryGroup` (a recursive group box) + a small shared helpers module for
  what both the parent and child use (`categoryGridSegments`).
- **Cohesive hook with a SMALL deps surface** → `src/modules/*/hooks/`. E.g. `useInfiniteScroll`
  (windowing + observer), `useModFixTools` (fix-tool load + menu builders; the builders take `modIds`
  so the hook stays decoupled from the current selection; a single `onManage` callback for dialog state).

Rules for every split:
1. **Move VERBATIM** — behavior-preserving. The host's existing tests are the compile-guard.
2. **Tests-first / add a focused test** for the extracted unit (`risky-change-tests-first.md`), then
   **live-verify** UI extractions via CDP (DOM assertion + a screenshot) in the running app.
3. **Prune the now-dead imports** in the host after the move (grep each symbol's ref count; `1` = dead).
4. **STOP at reasonable oversize.** A ~1200-line file with clean seams → extract them; a ~700-line file
   whose remainder is entangled handler/menu logic → **leave it**. Do not chase a line target.

## When NOT to split (leave it in the component)

- A **context-menu / action-handler builder** that needs many `setX` dialog setters + services +
  selection state threaded out (wide props surface) — keep it inline.
- A cluster whose extraction would **duplicate or externalize shared constants** used by the host too,
  unless you also cleanly lift those into a shared module.
- Anything where the extracted file would be **coupled back** to the parent through a big interface.

## Related

- [risky-change-tests-first.md](../rules/risky-change-tests-first.md) — verbatim move + tests as the guard.
- [ui-component-layers.md](ui-component-layers.md) — L1/L2 for extracted dumb subcomponents.
- `docs/ai-assistant/REACT_CLOSURE_PATTERNS.md` — `useStableRef` for ref-stored/external callbacks when a
  hook extraction would otherwise capture stale data (normal menu/JSX handlers don't need it).
