# Module boundaries — sanctioned cross-module access + reviewed-accepted exceptions

**Rule (CLAUDE.md §2):** a module must NEVER access another module's **repository** directly. For a
cross-module need, inject that module's **service** (the sanctioned in-process API — its facade is the
IPC router, awkward to call in-process). Example done 2026-07-11: `ModFixService`/`ModCacheService`
read profile config via `IProfileService.GetProfileConfigurationAsync`, NOT `IProfileRepository` (B1).

## Reviewed-accepted exceptions (do NOT re-flag these in a boundary audit)

These cross-module `I*Repository` uses were reviewed 2026-07-11 and **kept deliberately** — routing them
through a service would need new surface + carry real risk for little behavior gain:

| Site | Access | Why accepted |
|------|--------|--------------|
| `RemoteImportService`, `ModFixService` | write mod metadata via `IModRepository` | the established metadata-write precedent (also noted in `filesystem-operation-serialization.md`) |
| Tool: `ModPackageService`, `FileCleanupService`, `ModAnalysisService` | read `IModRepository` (`GetAllAsync`/`GetByIdAsync`) | `IModQueryService` only exposes enriched `ModInfo` DTOs, not raw entities; these need `ModEntity` (paths/metadata). Routing would require new entity-returning service methods + per-consumer DTO verification — M-L work, no behavior gain. |
| `CategoryService` ↔ `ModQueryService` | `CategoryService` reads `IModRepository` (mod counts); `ModQueryService` reads `ICategoryRepository` (descendant/all-category) | **bidirectional** — routing both through sibling services risks a **DI cycle** (`ModService`↔`CategoryService`). Accepted as-is; if ever refactored, break the cycle with lazy/service-locator resolution. |
| Migration steps (`MigrationStep3/5`), `Context/ProfilePathService` | `IModRepository` / `IProfileRepository` | one-shot bulk migration + the path/infra layer that legitimately owns profile-config plumbing (not feature-module violations) |

**When adding a NEW cross-module need:** inject the sibling **service** (B1 pattern). Only add to the
accepted list above after weighing the same trade-off (new service surface / DI-cycle / behavior risk).

## Parked UI follow-up (not a boundary issue — recorded so it's not re-flagged)
- **Unified filter-chip atom (F4, parked 2026-07-11):** `FindingsView` (antd `Tag` chip) and
  `ModImportWorkflowScreen` (`<button>`+spinner+count chip) are the same *concept* but different
  *markup/CSS*. A shared `FilterChip` atom would unify them but **restyle one screen** — a design
  decision, low payoff (2 sites). Left as-is until a deliberate design pass wants one chip look.
  (ModListPanel's closable search-filter tag is a DIFFERENT pattern — not part of this.)
