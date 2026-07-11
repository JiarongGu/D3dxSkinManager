# Risky change → write tests FIRST, then start (user directive 2026-07-11)

**Rule:** when a change is *risky*, create enough tests to lock the behavior BEFORE making the change —
don't do the risky edit and lean on after-the-fact tsc / a subagent's green build to catch problems.
Tests-first de-risks the edit and leaves a regression guard.

**Why:** a green build ≠ correct behavior. A wide mechanical sweep or a type-level change can compile yet
be wrong (or need a mid-flight patch), and most existing code has thin coverage. Tests written first
(a) force you to state the expected behavior, (b) fail loudly if the edit breaks it, (c) stay as a guard.

## What counts as "risky" (write tests first for these)
- **Wide mechanical sweeps** across many files/call-sites (e.g. the useModsState / useEventSubscription
  adoptions — 40+ sites each).
- **Type-level changes** to a shared generic/wrapper (a wrong type silently degrades to `unknown` and
  breaks every consumer — exactly the useModsState wrapper incident: `ReturnType<typeof useModsStore>`
  resolved to `unknown`, needing a mid-sweep patch).
- **Event wiring / subscription** changes (payload-shape mistakes are silent at runtime).
- **Concurrency / file-op** changes (see `filesystem-operation-serialization.md`).
- **Refactors that move behavior** across a boundary (facade→service, hook extraction).

## How
1. Write focused tests that assert the CURRENT behavior + the invariant the change must preserve. For a
   typed hook/util, a test that dereferences the typed result **doubles as a compile-guard** (it won't
   compile if the type regresses — e.g. `useMods.test.tsx` selects `s.mods`, which fails to compile if
   the selector param is `unknown`).
2. Run them → green (baseline).
3. Make the risky change.
4. Re-run → still green. Only now is the change trustworthy.

## Grounding incident (2026-07-11)
The `useModsState` adoption (42 sites) shipped, then its wrapper type had to be patched mid-flight
(`ReturnType<typeof useModsStore>` → `unknown`). Correct order would have been: test `useModsState`
(select a typed slice + reactivity) FIRST — the test would have failed to compile on the bad type,
catching it before the 42-site sweep. Fixed + guarded by `modules/mod/hooks/__tests__/useMods.test.tsx`;
the wrapper now derives its state type as `ReturnType<typeof useModsStore.getState>` (self-deriving,
can't regress to the hook's `unknown`).

See `test-coverage-priorities.md` (what to cover) and `docs/ai-assistant/TESTING_GUIDE.md` (how).
