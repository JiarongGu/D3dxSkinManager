# Domain-Driven Refactoring Plan

**Date**: 2026-02-19
**Status**: Planning Phase
**Goal**: Split god classes into domain-specific services following Single Responsibility Principle

---

## Problem Analysis

### Identified God Classes

1. **MigrationService** (1,049 lines) - CRITICAL
   - Violates Single Responsibility Principle
   - Handles 11 different concerns
   - Difficult to test and maintain

2. **ProfileService** (550 lines) - HIGH PRIORITY
   - Handles 8 different concerns (see analysis below)
   - Mixes profile management with file operations

3. **ModFileService** (454 lines) - MEDIUM PRIORITY
   - Handles 4 different concerns (see analysis below)
   - Mixes cache management with archive operations

---

## MigrationService Domain Analysis

### Current Responsibilities (11 distinct domains)

1. **Source Analysis Domain**
   - `AnalyzeSourceAsync()` - Analyze Python installation structure
   - Responsibility: Understand what exists in source

2. **Migration Orchestration Domain**
   - `MigrateAsync()` - Main orchestration flow
   - Responsibility: Coordinate all migration steps

3. **Metadata Migration Domain**
   - `MigrateMetadataAsync()` - Migrate mod metadata from modsIndex
   - Responsibility: Import mod definitions

4. **Archive Migration Domain**
   - `MigrateArchivesAsync()` - Migrate mod archives
   - Responsibility: Copy/move archive files

5. **Preview Migration Domain**
   - `MigratePreviewsAsync()` - Migrate preview images
   - Responsibility: Copy preview images

6. **Classification Migration Domain**
   - `MigrateClassificationsAsync()` - Migrate classification tree
   - `MigrateClassificationFoldersAsync()` - Migrate classification folders
   - Responsibility: Import taxonomy structure

7. **Configuration Migration Domain**
   - `MigrateConfigurationAsync()` - Migrate configuration
   - `ParseConfigurationAsync()` - Parse Python config files
   - Responsibility: Import user settings

8. **File Operations Domain**
   - `CopyDirectoryRecursiveAsync()` - Copy directories
   - Responsibility: File system operations (should use FileService)

9. **Validation Domain**
   - `ValidateMigrationAsync()` - Validate completed migration
   - `VerifyModsForCategoryAsync()` - Verify mod existence
   - Responsibility: Ensure data integrity

10. **Auto-detection Domain**
    - `AutoDetectPythonInstallationAsync()` - Find Python installation
    - Responsibility: Discover source location

11. **Logging Domain**
    - `LogAsync()` - Write log messages
    - Responsibility: Audit trail (should use centralized logger)

---

## ProfileService Domain Analysis

### Current Responsibilities (8 distinct domains)

1. **Profile Persistence Domain**
   - `LoadProfilesFromDisk()` - Load profiles from JSON
   - `SaveProfilesToDisk()` - Save profiles to JSON
   - Responsibility: Profile storage

2. **Profile CRUD Domain**
   - `GetAllProfilesAsync()` - List all profiles
   - `GetProfileByIdAsync()` - Get single profile
   - `CreateProfileAsync()` - Create new profile
   - `UpdateProfileAsync()` - Update profile
   - `DeleteProfileAsync()` - Delete profile
   - Responsibility: Basic profile operations

3. **Active Profile Domain**
   - `GetActiveProfileAsync()` - Get active profile
   - `SwitchProfileAsync()` - Switch profiles
   - Responsibility: Active profile management

4. **Profile Statistics Domain**
   - `GetProfileStatisticsAsync()` - Calculate statistics
   - `UpdateProfileStatisticsAsync()` - Update stats
   - Responsibility: Profile analytics

5. **Profile Duplication Domain**
   - `DuplicateProfileAsync()` - Clone a profile
   - `CopyProfileDataAsync()` - Copy profile data
   - Responsibility: Profile cloning

6. **Profile Import/Export Domain**
   - `ExportProfileConfigAsync()` - Export configuration
   - `ImportProfileConfigAsync()` - Import configuration
   - Responsibility: Profile portability

7. **Profile Configuration Domain**
   - `GetProfileConfigurationAsync()` - Get config
   - `UpdateProfileConfigurationAsync()` - Update config
   - `SaveProfileConfigurationAsync()` - Save config
   - Responsibility: Profile settings

8. **File Operations Domain**
   - `CopyDirectoryAsync()` - Copy directories
   - Responsibility: File system operations (should use FileService)

### Proposed Split for ProfileService

```
Profiles/Services/
├── ProfileRepository.cs              # Profile persistence (100-150 lines)
│   └── IProfileRepository
│       ├── GetAllAsync()
│       ├── GetByIdAsync()
│       ├── CreateAsync()
│       ├── UpdateAsync()
│       └── DeleteAsync()
│
├── ProfileStatisticsService.cs       # Statistics calculation (100-150 lines)
│   └── IProfileStatisticsService
│       ├── CalculateStatisticsAsync()
│       └── UpdateStatisticsAsync()
│
├── ProfileImportExportService.cs     # Import/Export (100-150 lines)
│   └── IProfileImportExportService
│       ├── ExportAsync()
│       └── ImportAsync()
│
├── ProfileCloningService.cs          # Profile duplication (100-150 lines)
│   └── IProfileCloningService
│       └── DuplicateAsync()
│
└── ProfileManagementService.cs       # Main profile operations (150-200 lines)
    └── IProfileManagementService
        ├── SwitchProfileAsync()
        ├── GetActiveProfileAsync()
        └── ManageProfiles()
```

---

## ModFileService Domain Analysis

### Current Responsibilities (4 distinct domains)

1. **Archive Management Domain**
   - `LoadAsync()` - Extract archive to work directory
   - `UnloadAsync()` - Remove from work directory
   - `ArchiveExists()` - Check if archive exists
   - `GetArchivePath()` - Get archive path
   - `CopyArchiveAsync()` - Copy archive file
   - `DeleteAsync()` - Delete archive and related files
   - Responsibility: Archive lifecycle

2. **Cache Management Domain**
   - `ScanCacheAsync()` - Scan cache directory
   - `GetCacheStatisticsAsync()` - Calculate cache stats
   - `CleanCacheAsync()` - Clean cache by category
   - `DeleteCacheAsync()` - Delete specific cache
   - `HasCache()` - Check cache existence
   - Responsibility: Cache operations

3. **File System Domain**
   - Uses FileService for directory size calculations
   - Uses FileUtilities for size formatting
   - Responsibility: File system operations

4. **Path Management Domain**
   - Path construction for archives, cache, work directories
   - Responsibility: Path resolution

### Proposed Split for ModFileService

```
Mods/Services/
├── ModArchiveService.cs              # Archive operations (150-200 lines)
│   └── IModArchiveService
│       ├── ExtractAsync()
│       ├── UnloadAsync()
│       ├── CopyArchiveAsync()
│       ├── DeleteArchiveAsync()
│       ├── ArchiveExists()
│       └── GetArchivePath()
│
├── ModCacheService.cs                # Cache management (200-250 lines)
│   └── IModCacheService
│       ├── ScanCacheAsync()
│       ├── GetStatisticsAsync()
│       ├── CleanAsync()
│       ├── DeleteAsync()
│       └── HasCache()
│
└── ModFileService.cs                 # Orchestration (50-100 lines)
    └── IModFileService
        └── Orchestrates archive and cache services
```

---

## Proposed Refactoring: Split by Domain

### New Service Architecture

```
Migration/Services/
├── MigrationOrchestrator.cs          # Main orchestration (150-200 lines)
│   └── IMigrationOrchestrator
│       ├── MigrateAsync()
│       └── AnalyzeSourceAsync()
│
├── MigrationSourceAnalyzer.cs        # Source analysis (100-150 lines)
│   └── IMigrationSourceAnalyzer
│       ├── AnalyzeDirectoryStructure()
│       ├── CountMods()
│       └── CalculateSizes()
│
├── MetadataMigrationService.cs       # Metadata migration (150-200 lines)
│   └── IMetadataMigrationService
│       ├── MigrateModsIndexAsync()
│       └── ParseModEntries()
│
├── ArchiveMigrationService.cs        # Archive migration (100-150 lines)
│   └── IArchiveMigrationService
│       └── MigrateArchivesAsync()
│
├── PreviewMigrationService.cs        # Preview migration (100-150 lines)
│   └── IPreviewMigrationService
│       └── MigratePreviewsAsync()
│
├── ClassificationMigrationService.cs # Classification migration (150-200 lines)
│   └── IClassificationMigrationService
│       ├── MigrateClassificationTreeAsync()
│       └── MigrateClassificationFoldersAsync()
│
├── ConfigurationMigrationService.cs  # Configuration migration (150-200 lines)
│   └── IConfigurationMigrationService
│       ├── MigrateConfigurationAsync()
│       └── ParsePythonConfigAsync()
│
├── MigrationValidationService.cs     # Validation (100-150 lines)
│   └── IMigrationValidationService
│       ├── ValidateMigrationAsync()
│       └── VerifyDataIntegrity()
│
└── PythonInstallationDetector.cs     # Auto-detection (50-100 lines)
    └── IPythonInstallationDetector
        └── AutoDetectAsync()
```

### Total: ~1,000-1,350 lines spread across 8 focused services
- Each service: 50-200 lines (manageable)
- Each service: Single responsibility
- Each service: Independently testable

---

## Benefits of Refactoring

### 1. Single Responsibility Principle
- Each service has ONE reason to change
- Clear boundaries between domains

### 2. Testability
- Can unit test each migration step independently
- Can mock dependencies easily
- Can test error scenarios in isolation

### 3. Maintainability
- Easy to find where specific logic lives
- Changes don't ripple across unrelated code
- New developers can understand one service at a time

### 4. Extensibility
- Can add new migration types without touching existing services
- Can replace individual services (e.g., different archive strategy)

### 5. Parallel Development
- Multiple developers can work on different migration aspects
- Less merge conflicts

---

## Refactoring Strategy

### Phase 1: Extract Non-Intrusive Services (Low Risk)
1. **Extract PythonInstallationDetector** (50-100 lines)
   - Move `AutoDetectPythonInstallationAsync()`
   - No dependencies on other migration logic
   - Easy to test

2. **Extract MigrationSourceAnalyzer** (100-150 lines)
   - Move `AnalyzeSourceAsync()`
   - Only reads, doesn't modify
   - Easy to test

3. **Extract MigrationValidationService** (100-150 lines)
   - Move validation methods
   - Independent of migration logic

**Risk**: Low - These are read-only or post-migration operations

### Phase 2: Extract Migration Services (Medium Risk)
4. **Extract PreviewMigrationService** (100-150 lines)
   - Move `MigratePreviewsAsync()`
   - Simple file copying logic

5. **Extract ArchiveMigrationService** (100-150 lines)
   - Move `MigrateArchivesAsync()`
   - File copying with options (copy/move/link)

6. **Extract ConfigurationMigrationService** (150-200 lines)
   - Move configuration parsing and migration
   - Independent of mod/classification data

**Risk**: Medium - These modify data but are independent

### Phase 3: Extract Complex Services (Higher Risk)
7. **Extract MetadataMigrationService** (150-200 lines)
   - Move `MigrateMetadataAsync()`
   - Parses modsIndex.json
   - Creates mod records

8. **Extract ClassificationMigrationService** (150-200 lines)
   - Move classification migration logic
   - Handles taxonomy import

**Risk**: Medium-High - These are central to migration

### Phase 4: Create Orchestrator (Highest Risk)
9. **Create MigrationOrchestrator** (150-200 lines)
   - Inject all migration services
   - Coordinate the overall flow
   - Handle progress reporting
   - Handle transactions/rollback

**Risk**: High - Changes the main flow

### Phase 5: Deprecate Old Service
10. **Remove old MigrationService**
    - Update DI registrations
    - Update facades to use MigrationOrchestrator

---

## Implementation Order (Recommended)

### Priority Order
1. **ModFileService** (Lowest Risk) - Split into 2 services
2. **ProfileService** (Medium Risk) - Split into 5 services
3. **MigrationService** (Highest Risk) - Split into 8 services

---

## Phase A: ModFileService Refactoring (Lowest Risk - START HERE)

### Estimated Time: 8-12 hours

This is the safest place to start because:
- Smallest god class (454 lines)
- Clear domain separation (archives vs cache)
- Less used than other services
- Good practice for larger refactorings

### A1: Extract ModCacheService (3-4 hours)
- [ ] Create IModCacheService interface
- [ ] Create ModCacheService implementation
- [ ] Move cache methods:
  - `ScanCacheAsync()`
  - `GetCacheStatisticsAsync()`
  - `CleanCacheAsync()`
  - `DeleteCacheAsync()`
  - `HasCache()`
- [ ] Add DI registration
- [ ] Write unit tests
- [ ] Update ModFileService to inject ModCacheService

### A2: Extract ModArchiveService (3-4 hours)
- [ ] Create IModArchiveService interface
- [ ] Create ModArchiveService implementation
- [ ] Move archive methods:
  - `ExtractAsync()` (was LoadAsync)
  - `UnloadAsync()`
  - `CopyArchiveAsync()`
  - `DeleteArchiveAsync()`
  - `ArchiveExists()`
  - `GetArchivePath()`
- [ ] Add DI registration
- [ ] Write unit tests
- [ ] Update ModFileService to inject ModArchiveService

### A3: Simplify ModFileService (2-3 hours)
- [ ] Keep ModFileService as thin orchestrator
- [ ] Delegate to ModArchiveService and ModCacheService
- [ ] Update all consumers (should be minimal changes)
- [ ] Integration tests
- [ ] Verify in running app

**A-Phase Total**: 8-12 hours
**Risk**: Low
**Benefit**: Learn the refactoring pattern for larger classes

---

## Phase B: ProfileService Refactoring (Medium Risk)

### Estimated Time: 16-20 hours

### B1: Extract ProfileRepository (3-4 hours)
- [ ] Create IProfileRepository interface
- [ ] Move persistence methods
- [ ] Write unit tests

### B2: Extract ProfileStatisticsService (3-4 hours)
- [ ] Create IProfileStatisticsService interface
- [ ] Move statistics calculation
- [ ] Write unit tests

### B3: Extract ProfileImportExportService (3-4 hours)
- [ ] Create IProfileImportExportService interface
- [ ] Move import/export methods
- [ ] Write unit tests

### B4: Extract ProfileCloningService (3-4 hours)
- [ ] Create IProfileCloningService interface
- [ ] Move duplication logic
- [ ] Write unit tests

### B5: Simplify ProfileManagementService (4-5 hours)
- [ ] Rename ProfileService to ProfileManagementService
- [ ] Inject all extracted services
- [ ] Update consumers
- [ ] Integration tests

**B-Phase Total**: 16-20 hours
**Risk**: Medium
**Benefit**: Cleaner profile management

---

## Phase C: MigrationService Refactoring (Highest Risk)

### Estimated Time: 22-32 hours

### Sprint 1: Foundation (2-3 hours)
- [ ] Create service interfaces
- [ ] Set up DI registrations
- [ ] Create test projects

### Sprint 2: Low-Risk Extractions (3-4 hours)
- [ ] Extract PythonInstallationDetector
- [ ] Extract MigrationSourceAnalyzer
- [ ] Extract MigrationValidationService
- [ ] Test individually

### Sprint 3: Medium-Risk Extractions (4-5 hours)
- [ ] Extract PreviewMigrationService
- [ ] Extract ArchiveMigrationService
- [ ] Extract ConfigurationMigrationService
- [ ] Test individually

### Sprint 4: High-Risk Extractions (5-6 hours)
- [ ] Extract MetadataMigrationService
- [ ] Extract ClassificationMigrationService
- [ ] Test individually

### Sprint 5: Orchestration (6-8 hours)
- [ ] Create MigrationOrchestrator
- [ ] Inject all services
- [ ] Implement progress reporting
- [ ] Integration testing

### Sprint 6: Cleanup (2-3 hours)
- [ ] Update DI registrations
- [ ] Update facade
- [ ] Remove old MigrationService
- [ ] Final testing

**Total Estimated Time**: 22-32 hours

---

## Testing Strategy

### Unit Tests (Per Service)
- Test each method independently
- Mock dependencies
- Test error cases
- Test edge cases

### Integration Tests
- Test orchestrator with all services
- Test complete migration flow
- Test rollback scenarios
- Test with real Python installation

### Regression Tests
- Ensure existing migrations still work
- Compare output with old service
- Verify data integrity

---

## File Operations Consolidation

### Replace Direct File Operations with FileService
- `CopyDirectoryRecursiveAsync()` → Use `FileService.CopyDirectoryAsync()`
- `File.Copy()` calls → Use `FileService.CopyFileAsync()`
- Centralize file operations in FileService

### Replace Console.WriteLine with Logging
- Create `IMigrationLogger` interface
- Implement structured logging
- Support progress callbacks
- Replace 162+ Console.WriteLine calls

---

## Next Steps

1. **Review this plan with stakeholder** (User approval)
2. **Start with Phase 1** (Low-risk extractions)
3. **Test thoroughly after each extraction**
4. **Commit after each successful extraction**

---

## Success Criteria

✅ Each service < 200 lines
✅ Each service has ONE responsibility
✅ All services have unit tests
✅ Integration tests pass
✅ Existing migrations work unchanged
✅ Code coverage > 80%

---

**Last Updated**: 2026-02-19
**Author**: AI Assistant
**Status**: Awaiting User Approval
