# Python to React Migration Design

## Overview
This document describes the migration strategy from the Python-based d3dxSkinManage to the React/C# version.

---

## 1. Data Mapping Analysis

### Python Structure → React Structure

#### Database Migration
| Python | React | Migration Strategy |
|--------|-------|-------------------|
| `modsIndex/index_2026-02.json` | SQLite `mods.db` | Parse JSON, insert into SQLite with SHA mapping |
| Monthly JSON files | Single database | Merge all monthly indexes |
| SHA1 hashes (40 char) | SHA256 hashes (64 char) | Keep original SHA1 as alternate ID or recalculate SHA256 |

#### Directory Structure Mapping
| Python Path | React Path | Notes |
|-------------|------------|-------|
| `resources/mods/{SHA}.7z` | `data/mods/{SHA}.7z` | Copy archives, update SHA if needed |
| `resources/preview/{SHA}.png` | `data/previews/{SHA}.png` | Copy preview images |
| `resources/cache/` | `data/cache/` | Optional - can rebuild |
| `home/{ENV}/work/` | Configured work directory | Copy or link |
| `home/{ENV}/configuration` | `data/config.json` | Merge into unified config |
| `home/{ENV}/modsIndex/` | `data/mods.db` | Convert JSON to SQLite |
| `home/{ENV}/classification/` | `data/classification_rules.json` | Convert to rule format |
| `home/{ENV}/thumbnail/` | `data/thumbnails/` | Copy category icons |

#### Configuration Migration
| Python Setting | React Equivalent | Conversion |
|----------------|------------------|------------|
| `local/configuration` (JSON) | `config.json` | Map fields |
| `{ENV}/configuration` (game path) | `workDirectory` in config | Direct copy |
| `order.json` (sort rules) | UI preferences in config | Custom mapping |
| Plugin configurations | Plugin settings | Per-plugin migration |

---

## 2. Migration Phases

### Phase 1: Analysis & Validation
1. **Detect Python Installation**
   - Scan for `d3dxSkinManage.exe` or directory structure
   - Validate key files exist
   - Calculate total data size

2. **Analyze Content**
   - Count mods in JSON indexes
   - Identify active environment
   - Check for multiple environments
   - Validate archive integrity

3. **Pre-Migration Report**
   - Total mods to migrate
   - Total archive size
   - Configuration conflicts
   - Estimated time

### Phase 2: Data Migration
1. **Mod Metadata**
   - Parse all `modsIndex/*.json` files
   - Deduplicate across monthly files
   - Convert field names
   - Insert into SQLite database

2. **Mod Archives**
   - Copy archives from `resources/mods/`
   - Verify integrity (file size, existence)
   - Handle SHA1 vs SHA256 difference
   - Update database paths

3. **Preview Images**
   - Copy from `resources/preview/`
   - Copy embedded previews from `work/Mods/*/preview.png`
   - Generate missing thumbnails

4. **Configuration**
   - Merge `local/configuration`
   - Extract game path from environment config
   - Convert classification rules
   - Preserve user preferences

### Phase 3: Post-Migration
1. **Verification**
   - Compare mod counts
   - Verify archive accessibility
   - Check thumbnail generation
   - Validate database integrity

2. **Cleanup Options**
   - Keep Python installation (safe)
   - Create backup (recommended)
   - Archive old data
   - Delete after confirmation

---

## 3. Data Transformation Rules

### Mod Metadata Conversion

**Python JSON Format:**
```json
{
  "object": "莱万汀",
  "type": "7z",
  "name": "莱万汀-星光盛典v1.4-整合",
  "author": "",
  "grading": "G",
  "explain": "",
  "tags": []
}
```

**React SQLite Schema:**
```sql
CREATE TABLE Mods (
    SHA TEXT PRIMARY KEY,              -- SHA256 (or keep SHA1)
    ObjectName TEXT NOT NULL,          -- Python: "object"
    Name TEXT NOT NULL,                -- Python: "name"
    Author TEXT,                       -- Python: "author"
    Description TEXT,                  -- Python: "explain"
    Type TEXT DEFAULT '7z',            -- Python: "type"
    Grading TEXT DEFAULT 'G',          -- Python: "grading"
    Tags TEXT,                         -- JSON array from "tags"
    IsLoaded INTEGER DEFAULT 0,        -- NEW: track load state
    IsAvailable INTEGER DEFAULT 0,     -- NEW: archive exists
    ThumbnailPath TEXT,                -- NEW: generated path
    PreviewPath TEXT,                  -- NEW: copied from preview dir
    CreatedAt TEXT,                    -- NEW: timestamp
    UpdatedAt TEXT                     -- NEW: timestamp
);
```

**Field Mapping:**
- `object` → `ObjectName` (no change needed for Chinese characters)
- `name` → `Name`
- `author` → `Author` (empty string → NULL)
- `explain` → `Description`
- `type` → `Type` (always "7z" or "zip")
- `grading` → `Grading` (G/P/R/X system compatible)
- `tags` → `Tags` (JSON array → comma-separated string or keep JSON)

**New Fields:**
- `IsLoaded`: false by default (user needs to load manually)
- `IsAvailable`: true if archive file exists
- `ThumbnailPath`: generated during migration
- `PreviewPath`: copied from Python preview directory
- `CreatedAt`/`UpdatedAt`: use current timestamp

### Configuration Conversion

**Python `local/configuration`:**
```json
{
  "style_theme": "darkly",
  "thumbnail_approximate_algorithm": "similarity/key-in",
  "uuid": "C66A9772-4DF1-D745-8CD7-477817554DB0",
  "ocd_window_name": "Endfield",
  "ocd_window_width": 2560,
  "ocd_window_height": 1440,
  "main_window_position_x": 429,
  "main_window_position_y": 293,
  "file_warn_size": Infinity,
  "confirm_deletion": false
}
```

**React `config.json` (Proposed):**
```json
{
  "version": "1.0",
  "migratedFrom": "python",
  "migrationDate": "2026-02-17T10:30:00Z",
  "workDirectory": "E:\\Mods\\Endfield MOD\\home\\Endfield\\work",
  "ui": {
    "theme": "dark",
    "windowPosition": { "x": 429, "y": 293, "width": 1542, "height": 831 }
  },
  "fileHandling": {
    "warnSize": null,
    "confirmDeletion": true
  },
  "ocd": {
    "windowName": "Endfield",
    "width": 2560,
    "height": 1440
  },
  "advanced": {
    "thumbnailAlgorithm": "similarity",
    "uuid": "C66A9772-4DF1-D745-8CD7-477817554DB0"
  }
}
```

### Classification Rules Conversion

**Python Format** (`classification/干员·灼热`):
```
莱万汀
伊芙利特
```

**React Format** (`classification_rules.json`):
```json
[
  {
    "name": "莱万汀 (Fire Operator)",
    "pattern": "*莱万汀*",
    "objectName": "莱万汀",
    "priority": 100
  },
  {
    "name": "Fire Operators - Generic",
    "pattern": "*",
    "objectName": "干员·灼热",
    "priority": 10
  }
]
```

**Conversion Logic:**
1. Read all files in `classification/` directory
2. Directory name = category/class name
3. Each line in file = character name
4. Generate wildcard pattern: `*{character}*`
5. Set priority: 100 for specific characters, 10 for categories

---

## 4. SHA Hash Strategy

### Problem: Python uses SHA1, React uses SHA256

**Option 1: Keep SHA1 (Recommended)**
- ✅ No recalculation needed
- ✅ Archives don't need to be reprocessed
- ✅ Faster migration
- ⚠️ Less secure hash (not critical for mod files)
- Implementation: Change React to accept SHA1 or add `LegacySHA` field

**Option 2: Recalculate SHA256**
- ✅ Consistent with React design
- ✅ Better security
- ⚠️ Slower migration (must read all archives)
- ⚠️ Risk of errors during recalculation
- Implementation: Read each archive, calculate SHA256, rename files

**Decision: Use Option 1 for migration, but allow Option 2 as advanced setting**

---

## 5. Migration UI Flow

### Step 1: Detection
```
┌─────────────────────────────────────┐
│  Migration Wizard                   │
├─────────────────────────────────────┤
│                                     │
│  🔍 Detecting Python Installation   │
│                                     │
│  [Browse...]  Auto-detect           │
│                                     │
│  ✓ Found: E:\Mods\Endfield MOD      │
│  ✓ Mods: 65 mods                    │
│  ✓ Size: 2.9GB archives             │
│  ✓ Config: Valid                    │
│                                     │
│  [Cancel]              [Next >]    │
└─────────────────────────────────────┘
```

### Step 2: Options
```
┌─────────────────────────────────────┐
│  Migration Options                  │
├─────────────────────────────────────┤
│                                     │
│  What to migrate:                   │
│  ☑ Mod archives (65 mods, 2.9GB)    │
│  ☑ Mod metadata & descriptions      │
│  ☑ Preview images (104MB)           │
│  ☑ Configuration & preferences      │
│  ☑ Classification rules             │
│  ☐ Cache files (optional, 49MB)    │
│                                     │
│  Archive handling:                  │
│  ○ Copy files (safe, requires 2.9GB)│
│  ● Move files (faster, no copy)     │
│  ○ Link files (advanced)            │
│                                     │
│  After migration:                   │
│  ● Keep Python installation         │
│  ○ Create backup and remove         │
│  ○ Remove Python installation       │
│                                     │
│  [< Back]              [Migrate >] │
└─────────────────────────────────────┘
```

### Step 3: Progress
```
┌─────────────────────────────────────┐
│  Migrating Data...                  │
├─────────────────────────────────────┤
│                                     │
│  ████████████░░░░░░░  65%           │
│                                     │
│  Current: Copying mod archives      │
│  Progress: 42/65 mods               │
│  Speed: 15 MB/s                     │
│  Time remaining: ~2 minutes         │
│                                     │
│  Completed:                         │
│  ✓ Analyzed source (1s)             │
│  ✓ Created database (2s)            │
│  ✓ Migrated metadata (3s)           │
│  → Copying archives (120s)          │
│  ⏳ Copying previews                 │
│  ⏳ Converting configuration         │
│  ⏳ Finalizing                       │
│                                     │
│  [Cancel]                           │
└─────────────────────────────────────┘
```

### Step 4: Complete
```
┌─────────────────────────────────────┐
│  Migration Complete! ✓              │
├─────────────────────────────────────┤
│                                     │
│  Successfully migrated:             │
│  ✓ 65 mods with metadata            │
│  ✓ 2.9GB mod archives               │
│  ✓ 60 preview images                │
│  ✓ Configuration & preferences      │
│  ✓ 10 classification categories     │
│                                     │
│  Your Python installation is intact.│
│  You can safely delete it after     │
│  verifying everything works.        │
│                                     │
│  [View Migration Log]               │
│  [Open Mod Library]   [Close]      │
└─────────────────────────────────────┘
```

---

## 6. Error Handling

### Common Issues & Solutions

**Issue 1: Corrupted JSON Index**
- **Detection**: JSON parse error
- **Solution**: Try parsing other monthly files, skip corrupted entries
- **Fallback**: Scan archives directory, generate metadata from filenames

**Issue 2: Missing Archive Files**
- **Detection**: File reference in JSON but no file
- **Solution**: Mark as unavailable, log warning, continue migration
- **UI**: Show list of missing files, offer to skip

**Issue 3: Insufficient Disk Space**
- **Detection**: Check available space before copy
- **Solution**: Offer move instead of copy, or selective migration
- **UI**: Show space required vs available

**Issue 4: Permission Errors**
- **Detection**: Access denied during file operations
- **Solution**: Request admin elevation, or change target directory
- **UI**: Clear error message with resolution steps

**Issue 5: SHA Hash Mismatch**
- **Detection**: Calculated hash ≠ filename
- **Solution**: Log warning, use filename hash, mark for verification
- **UI**: Show list of mismatches, offer to fix

---

## 7. Backwards Compatibility

### Coexistence Mode
Both Python and React versions can coexist if:
1. **React uses copy mode** (not move)
2. **Different database files** (JSON vs SQLite)
3. **Separate work directories** (optional)

### Gradual Migration
Users can migrate in stages:
1. Migrate metadata only → Test React version
2. Migrate archives → Use React for management
3. Keep Python as backup → Delete when confident

---

## 8. Implementation Plan

### Backend Services

**MigrationService.cs**:
```csharp
public interface IMigrationService
{
    Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath);
    Task<MigrationResult> MigrateAsync(string pythonPath, MigrationOptions options);
    Task<bool> ValidateMigrationAsync(string pythonPath);
}
```

**PythonConfigParser.cs**:
```csharp
public class PythonConfigParser
{
    public PythonConfiguration ParseLocalConfiguration(string filePath);
    public PythonModIndex ParseModIndex(string jsonPath);
    public List<ClassificationRule> ParseClassifications(string classDir);
}
```

**ArchiveMigrator.cs**:
```csharp
public class ArchiveMigrator
{
    public Task CopyArchivesAsync(List<string> sources, string destination);
    public Task MoveArchivesAsync(List<string> sources, string destination);
    public Task<bool> VerifyArchiveIntegrity(string archivePath);
}
```

### Frontend Components

**MigrationWizard.tsx**:
- Multi-step wizard (Detection → Options → Progress → Complete)
- Progress bars with real-time updates
- Error display with retry options
- Log viewer for troubleshooting

**MigrationService.ts**:
- IPC communication with backend
- Progress streaming
- Cancel/pause functionality
- Result handling

---

## 9. Testing Strategy

### Test Cases

1. **Empty Python Installation**
   - Expected: Graceful error, clear message

2. **Partial Python Installation**
   - Missing mod archives but JSON exists
   - Expected: Migrate available data, log warnings

3. **Multiple Environments**
   - home/Endfield, home/AnotherGame
   - Expected: Detect all, offer to choose

4. **Large Dataset**
   - 100+ mods, 10GB+ archives
   - Expected: Progress tracking, no timeout

5. **Corrupted Data**
   - Invalid JSON, corrupt archives
   - Expected: Skip invalid entries, continue

6. **Mid-Migration Cancel**
   - User cancels during copy
   - Expected: Rollback or safe state

### Validation Checks

✅ All mods from JSON present in database
✅ Archive file count matches database count
✅ Preview images accessible
✅ Configuration fields migrated correctly
✅ No duplicate SHA entries
✅ Classification rules valid

---

## 10. Success Criteria

✅ **Data Integrity**: 100% of valid mods migrated
✅ **User Experience**: <5 clicks to complete migration
✅ **Performance**: <5 minutes for 100 mods
✅ **Safety**: Original data untouched (default)
✅ **Error Recovery**: Clear messages, retry options
✅ **Documentation**: Step-by-step guide with screenshots

---

**Implementation Priority**: HIGH (Phase 3 feature - Migration Tools)
**Estimated Time**: 12-16 hours (backend + frontend + testing)
**Dependencies**: ConfigurationService, ModRepository, FileService
