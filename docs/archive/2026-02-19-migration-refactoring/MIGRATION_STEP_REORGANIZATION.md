# Migration Step Reorganization - Implementation Plan

**Date**: 2026-02-19
**Status**: 🚧 IN PROGRESS
**User Request**: Reorganize migration steps to correct logical order

---

## Problem

**Current Order (Illogical)**:
1. Analyze Source
2. Migrate Metadata (mods without nodes to attach to!)
3. Migrate Archives
4. Migrate Classifications (too late - nodes should exist first!)
5. Migrate Previews (mixes thumbnails with mod previews)
6. Migrate Configuration (should be FIRST to set up profile!)

**Issues**:
- Configuration comes LAST (should be first to set up profile)
- Mods created BEFORE classification nodes exist (can't attach properly)
- Thumbnails and mod previews mixed in one step

---

## Solution

**New Order (Logical)**:
1. **Analyze Source** - Validate Python installation ✅ No change
2. **Migrate Configuration** - Set up current profile (was Step 6)
3. **Migrate Classifications** - Create nodes (was Step 4)
4. **Migrate Classification Thumbnails** - Associate thumbnails (split from Step 5)
5. **Migrate Mod Archives + Metadata** - Create mods with details (merge Step 2 + Step 3)
6. **Migrate Mod Previews** - Copy mod preview images (split from Step 5)

**Rationale**:
- Configuration FIRST → Sets up profile properly
- Classifications BEFORE mods → Nodes exist for attachment
- Thumbnails separate from previews → Clear responsibility
- Archives + Metadata together → Mods created with all details at once

---

## File Changes Required

### Renames (Number Changes Only):

| Old File | New File | Changes |
|----------|----------|---------|
| MigrationStep6MigrateConfiguration.cs | MigrationStep2MigrateConfiguration.cs | StepNumber 6→2, PercentComplete 90→15 |
| MigrationStep4MigrateClassifications.cs | MigrationStep3MigrateClassifications.cs | StepNumber 4→3, PercentComplete 60→30 |

### Splits (New Files):

| Old File | New Files | Reason |
|----------|-----------|--------|
| MigrationStep5MigratePreviews.cs | MigrationStep4MigrateClassificationThumbnails.cs<br>MigrationStep6MigrateModPreviews.cs | Separate classification thumbnails from mod previews |

### Merges:

| Old Files | New File | Reason |
|-----------|----------|--------|
| MigrationStep2MigrateMetadata.cs<br>MigrationStep3MigrateArchives.cs | MigrationStep5MigrateModArchives.cs | Create mods with archives and metadata together |

---

## Detailed Step Descriptions

### Step 1: Analyze Source (Unchanged)
**File**: `MigrationStep1AnalyzeSource.cs`
**Purpose**: Validate Python installation structure
**Dependencies**: IImageService, IPythonConfigurationParser
**Percent**: 0-10%

### Step 2: Migrate Configuration (Was Step 6)
**File**: `MigrationStep2MigrateConfiguration.cs` ✅ RENAMED
**Purpose**: Update current profile configuration from Python
**Dependencies**: IConfigurationService
**Percent**: 10-20%
**Changes**:
- StepNumber: 6 → 2
- PercentComplete: 90 → 15
- Description updated

### Step 3: Migrate Classifications (Was Step 4)
**File**: `MigrationStep3MigrateClassifications.cs` ✅ RENAMED
**Purpose**: Create classification nodes (hierarchy)
**Dependencies**: IClassificationService, IModAutoDetectionService
**Percent**: 20-40%
**Changes**:
- StepNumber: 4 → 3
- PercentComplete: 60 → 30
- Description updated

### Step 4: Migrate Classification Thumbnails (New - Split from Step 5)
**File**: `MigrationStep4MigrateClassificationThumbnails.cs` 🆕 CREATE
**Purpose**: Associate thumbnails with classification nodes from _redirection.ini
**Dependencies**: IRedirectionFileParser, IClassificationService, IFileService
**Percent**: 40-50%
**Logic**: Extract from old Step5 lines 197-235 (thumbnail association)

### Step 5: Migrate Mod Archives (New - Merge Step 2 + Step 3)
**File**: `MigrationStep5MigrateModArchives.cs` 🆕 CREATE
**Purpose**: Copy/move mod archive files AND create mod entries with metadata
**Dependencies**: IModManagementService, IFileService
**Percent**: 50-75%
**Logic**: Merge old Step2 (metadata) + old Step3 (archives)

### Step 6: Migrate Mod Previews (New - Split from Step 5)
**File**: `MigrationStep6MigrateModPreviews.cs` 🆕 CREATE
**Purpose**: Copy mod preview images only
**Dependencies**: IFileService, IImageService
**Percent**: 75-100%
**Logic**: Extract from old Step5 lines 103-164 (preview copying)

---

## Implementation Steps

### Phase 1: Rename existing files ✅ STARTED
1. ✅ Rename Step6 → Step2 (Configuration)
2. ✅ Rename Step4 → Step3 (Classifications)
3. ✅ Update StepNumber and PercentComplete in both

### Phase 2: Split Step5 (Previews) into two
1. Create Step4 (Classification Thumbnails) - Extract lines 197-235
2. Create Step6 (Mod Previews) - Extract lines 103-164
3. Delete old Step5

### Phase 3: Merge Step2 + Step3 into new Step5
1. Create Step5 (Mod Archives + Metadata)
2. Merge metadata parsing from old Step2
3. Merge archive copying from old Step3
4. Delete old Step2 and Step3

### Phase 4: Update DI Registration
1. Update MigrationServiceExtensions.cs
2. Register all 6 steps in new order

### Phase 5: Update Tests
1. Update MigrationServiceTests.cs
2. Create step instances in new order
3. Update mocks as needed

### Phase 6: Build & Test
1. dotnet build
2. dotnet test
3. Verify all 173 tests pass

---

## Progress Percentages (New)

| Step | Range | Description |
|------|-------|-------------|
| 1 | 0-10% | Analyzing Python installation |
| 2 | 10-20% | Migrating configuration |
| 3 | 20-40% | Creating classification nodes |
| 4 | 40-50% | Associating classification thumbnails |
| 5 | 50-75% | Migrating mod archives + metadata |
| 6 | 75-100% | Copying mod preview images |

---

## Benefits of New Order

### 1. Configuration First
- Sets up profile properly before any data migration
- Ensures working directory is configured
- Migration metadata stored early

### 2. Classifications Before Mods
- Classification nodes exist before mods are created
- Mods can be properly attached to nodes during creation
- No orphaned mods

### 3. Thumbnails Separate from Previews
- Clear responsibility: Classification thumbnails vs Mod previews
- Classification thumbnails need nodes to exist (Step 3)
- Mod previews need mods to exist (Step 5)

### 4. Archives + Metadata Together
- Atomic operation: Create mod with ALL its data at once
- No partial mods (metadata without archive or vice versa)
- Simpler logic - one step does everything for a mod

---

## Migration Options Impact

| Option | Affects Steps | Notes |
|--------|---------------|-------|
| MigrateConfiguration | Step 2 | Can be disabled |
| MigrateClassifications | Steps 3, 4 | Step 4 depends on Step 3 |
| MigrateMetadata | Step 5 | Now combined with archives |
| MigrateArchives | Step 5 | Now combined with metadata |
| MigratePreviews | Step 6 | Now only mod previews |

---

## Status

- ✅ Step 2 (Configuration): Renamed and updated
- ✅ Step 3 (Classifications): Renamed and updated
- 🚧 Step 4 (Classification Thumbnails): Need to create
- 🚧 Step 5 (Mod Archives): Need to create (merge)
- 🚧 Step 6 (Mod Previews): Need to create (split)
- ⏳ DI Registration: Not started
- ⏳ Tests: Not started
- ⏳ Build & Verify: Not started

---

**Next Action**: Create Step 4 (MigrateClassificationThumbnails) by extracting thumbnail logic from old Step5
