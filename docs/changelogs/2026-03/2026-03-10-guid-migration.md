# Mod Identifier Migration: SHA → GUID

**Date:** 2026-03-10
**Type:** Architecture Change
**Impact:** Database Schema, File Structure, API

---

## Summary

Migrated mod identifiers from content-addressed SHA-256 hashes to stable GUID-based identifiers for improved mod lifecycle management and immutability.

## Motivation

### Problems with SHA-based Identifiers

1. **Identifier Instability**: SHA changes when mod files are modified (even minor changes)
2. **Content Coupling**: Identifier tied to file content rather than mod identity
3. **Collision Risk**: Theoretical SHA collision risk for identical mod files
4. **Import Complexity**: Required hash calculation during import (slow for large files)

### Benefits of GUID-based Identifiers

1. **Stable Identity**: GUID remains unchanged throughout mod lifecycle
2. **Entity-based**: ID represents mod entity, not file content
3. **Globally Unique**: No collision risk across all profiles and systems
4. **Fast Generation**: Instant ID generation without file processing
5. **Future-proof**: Supports mod versioning and updates without ID changes

## Implementation Details

### GUID Format

**Pattern**: 32-character uppercase hexadecimal string without hyphens

**Generation**:
```csharp
public static string NewId() => Guid.NewGuid().ToString("N").ToUpperInvariant();
```

**Example**: `A1B2C3D4E5F67890123456789ABCDEF0`

### Database Changes

**Migration**: `202603080001_CreateModsTable.cs`

```csharp
Create.Table("Mods")
    .WithColumn("Id").AsString().NotNullable().PrimaryKey()  // Changed from SHA
    .WithColumn("Name").AsString().NotNullable()
    .WithColumn("Category").AsString().NotNullable()
    // ... other columns
```

**Backward Compatibility**: Initial migration creates table with `Id` column. No rename migration needed for fresh installations.

### File Structure Changes

**Before (SHA-based)**:
```
mods/
├── 3A5F2D1B4C.../.7z          # SHA-256 hash (64 chars)
thumbnails/
├── 3A5F2D1B4C.../.png
work/Mods/
├── 3A5F2D1B4C.../            # Active mod
└── DISABLED-3A5F2D1B4C.../   # Disabled cache
```

**After (GUID-based)**:
```
mods/
├── A1B2C3D4E5F6.../.7z       # GUID (32 chars uppercase hex)
thumbnails/
├── A1B2C3D4E5F6.../.png
work/Mods/
├── A1B2C3D4E5F6.../          # Active mod
└── DISABLED-A1B2C3D4E5F6.../ # Disabled cache
```

### Code Changes

#### Domain Model

**ModInfo.cs**:
```csharp
public class ModInfo
{
    // Static factory method for centralized ID generation
    public static string NewId() => Guid.NewGuid().ToString("N").ToUpperInvariant();

    // Changed property name
    public string Id { get; set; } = string.Empty;  // Was: SHA

    // ... other properties
}
```

#### Import Service

**ModImportService.cs** - Removed SHA calculation:
```csharp
// OLD: Calculate SHA-256 hash during import
var sha = await _hashHelper.CalculateFileSHA256Async(filePath);

// NEW: Generate GUID instantly
var id = ModInfo.NewId();
```

#### Repositories

All repository methods updated:
```csharp
// Before
Task<ModEntity?> GetByIdAsync(string sha);
Task<bool> DeleteAsync(string sha);

// After
Task<ModEntity?> GetByIdAsync(string id);
Task<bool> DeleteAsync(string id);
```

#### Frontend Changes

**TypeScript interfaces**:
```typescript
// Before
export interface ModInfo {
  sha: string;
  // ...
}

// After
export interface ModInfo {
  id: string;
  // ...
}
```

**Event payloads**:
```typescript
// Before
{ sha: string }

// After
{ id: string }
```

## Migration Strategy

### For New Installations

- Database created with `Id` column from start
- All mods use GUID identifiers
- No migration required

### For Existing Installations

**Note**: Existing SHA-based databases would require data migration (not implemented yet). Current focus is on ensuring new installations use GUIDs correctly.

**Future Migration Considerations**:
1. Read existing mods with SHA identifiers
2. Generate new GUIDs for each mod
3. Rename files/folders (SHA → GUID)
4. Update database records
5. Preserve mod metadata and associations

## Testing

### Test Coverage

- ✅ 330/330 tests passing (100% pass rate)
- ✅ All repository CRUD operations
- ✅ Import workflow with GUID generation
- ✅ Load/unload lifecycle
- ✅ Cache management
- ✅ Event emission with correct payload structure

### Test Updates

Fixed 4 test assertions that checked for `sha` property:
1. `ModLifecycleServiceTests.LoadAsync_WhenModDoesNotExist_ShouldThrowOperationException`
2. `ModLifecycleServiceTests.LoadAsync_WithAutoImportedPreviews_ShouldEmitPreviewImportedEvent`
3. `ModMetadataServiceTests.UpdateAsync_ShouldEmitMetadataUpdatedEvent`
4. `ModImportServiceTests.ScanAndImportPreviewsFromFolderAsync_WithValidFolder_ShouldImportPreviews`

## Documentation Updates

Updated documentation files:
- ✅ `DATA_STORAGE_STRUCTURE.md` - File/folder structure examples
- ✅ `CACHE_MANAGEMENT.md` - Code examples and algorithms
- ✅ `DOMAIN_DESIGN.md` - Service examples
- ✅ `AI_GUIDE.md` - (Already reflected current patterns)

## Breaking Changes

### API Changes

All IPC methods that accepted `sha: string` now accept `id: string`:

**Backend**:
```csharp
// Method signatures changed
Task<ModInfo?> GetModByIdAsync(string id);  // Was: GetModByShaAsync
Task<bool> DeleteModAsync(string id);       // Parameter renamed
```

**Frontend**:
```typescript
// Service methods updated
modService.loadMod(profileId, id);          // Was: sha
modService.deleteMod(profileId, id);        // Was: sha
```

### Database Schema

- Column renamed: `SHA` → `Id`
- Data type unchanged: `TEXT` (SQLite)
- Constraint unchanged: `PRIMARY KEY NOT NULL`

## Performance Impact

### Positive

- **Faster Imports**: No SHA-256 calculation (saves ~100-500ms per mod)
- **Simpler Logic**: No hash collision handling needed
- **Smaller Identifiers**: 32 chars vs 64 chars (50% reduction)

### Neutral

- **Database Queries**: No performance change (indexed TEXT column)
- **File Operations**: No change (same filename pattern)

## Known Limitations

1. **No Automatic Data Migration**: Existing SHA-based databases not automatically migrated
2. **Manual Migration Required**: Users must manually migrate existing mod libraries
3. **Loss of Content Verification**: Can no longer verify mod file integrity via identifier

## Future Enhancements

1. **Data Migration Tool**: Automate migration of existing SHA-based databases
2. **Checksum Field**: Add optional checksum field for file integrity verification
3. **Mod Versioning**: Support multiple versions of same mod with different GUIDs
4. **Import Deduplication**: Detect duplicate mod files and reuse existing mods

## References

- **PR/Commit**: `b9b70cd` - test: fix assertion checks from sha to id
- **PR/Commit**: `24c5f7c` - refactor: migrate mod identifiers from SHA to GUID
- **Database Migration**: `202603080001_CreateModsTable.cs`
- **Design Discussion**: docs/AI_GUIDE.md (v3.6)

---

**Related Documentation**:
- [Data Storage Structure](../../architecture/DATA_STORAGE_STRUCTURE.md)
- [Database Migration Architecture](../../architecture/DATABASE_MIGRATION_ARCHITECTURE.md)
- [Domain Design](../../architecture/DOMAIN_DESIGN.md)
