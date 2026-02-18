# Migration Guide: Python to .NET

**Project:** D3dxSkinManager
**Source:** Python d3dxSkinManage v1.6.3
**Target:** .NET D3dxSkinManager v1.0+
**Last Updated:** 2026-02-17

---

## Table of Contents

1. [Overview](#overview)
2. [When to Migrate](#when-to-migrate)
3. [Pre-Migration Checklist](#pre-migration-checklist)
4. [Data Migration](#data-migration)
5. [Feature Porting Guide](#feature-porting-guide)
6. [Code Pattern Translation](#code-pattern-translation)
7. [Common Pitfalls](#common-pitfalls)
8. [Testing Migration](#testing-migration)

---

## Overview

This guide helps in two scenarios:
1. **Users** migrating their mod data from Python to .NET version
2. **Developers** porting Python features to .NET codebase

---

## When to Migrate

### For Users

**Migrate to .NET When:**
- ✅ You have 1000+ mods (performance improvement)
- ✅ Python version feels slow
- ✅ You want modern UI
- ✅ Core features you need are implemented

**Stay on Python If:**
- ❌ You need features not yet in .NET (check [ORIGINAL_COMPARISON.md](ORIGINAL_COMPARISON.md))
- ❌ You use Python-specific plugins
- ❌ You're comfortable with Python version

### For Developers

**Port Feature When:**
- ✅ Feature is in Python stable version
- ✅ Feature has clear requirements
- ✅ Dependencies are available in .NET
- ✅ Design fits .NET architecture

**Don't Port If:**
- ❌ Feature is experimental in Python
- ❌ Feature is Python-specific hack
- ❌ Better solution exists in .NET

---

## Pre-Migration Checklist

### For Users

Before migrating your data:

```
[ ] Backup your Python data directory
    Location: (Python app directory)/data/

[ ] Export mod list from Python
    File > Export > Export All Mods (or similar)

[ ] Note your current settings
    - Game directory path
    - Mod storage path
    - Loaded mods list

[ ] Take screenshots of Python UI
    For reference if you need to revert

[ ] Verify .NET version has features you need
    Check: docs/core/ORIGINAL_COMPARISON.md

[ ] Download .NET version
    From: (release page URL when available)

[ ] Keep Python version installed
    Don't uninstall until .NET is working
```

### For Developers

Before porting a feature:

```
[ ] Read Python implementation
    Understand what it does, not just how

[ ] Check Python dependencies
    Can they be replaced in .NET?

[ ] Design .NET architecture
    Where does it fit? Service? UI component?

[ ] Create interfaces first
    Define contracts before implementation

[ ] Write tests (future)
    Test-driven development

[ ] Update documentation
    CHANGELOG.md, KEYWORDS_INDEX.md, feature docs
```

---

## Data Migration

### Current Status

**⏳ Automated migration tool: Planned for Phase 3**

**✅ Manual migration: Available now**

### Manual Migration (Current)

**Step 1: Export from Python**

```python
# In Python app, run or create script:
import json

# Read Python data
with open('data/mods.json', 'r', encoding='utf-8') as f:
    python_mods = json.load(f)

# Export to intermediate format
export_data = []
for mod in python_mods['mods']:
    export_data.append({
        'SHA': mod.get('sha', ''),
        'ObjectName': mod.get('object', ''),
        'Name': mod.get('name', ''),
        'Author': mod.get('author', ''),
        'Description': mod.get('description', ''),
        'Type': mod.get('type', '7z'),
        'Grading': mod.get('grading', 'G'),
        'Tags': mod.get('tags', []),
        'IsLoaded': mod.get('is_loaded', False),
        'ThumbnailPath': mod.get('thumbnail', None),
        'PreviewPath': mod.get('preview', None)
    })

# Save export
with open('export_for_dotnet.json', 'w', encoding='utf-8') as f:
    json.dump(export_data, f, indent=2, ensure_ascii=False)

print(f"Exported {len(export_data)} mods to export_for_dotnet.json")
```

**Step 2: Import to .NET**

```csharp
// Create migration tool (D3dxSkinManager.Migrator project)
using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

public class Migrator
{
    public static async Task ImportFromJson(string jsonPath, string dbPath)
    {
        // Read export file
        var json = await File.ReadAllTextAsync(jsonPath);
        var mods = JsonSerializer.Deserialize<List<ExportedMod>>(json);

        Console.WriteLine($"Importing {mods.Count} mods...");

        // Connect to database
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        // Begin transaction
        using var transaction = connection.BeginTransaction();

        int imported = 0;
        foreach (var mod in mods)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Mods
                (SHA, ObjectName, Name, Author, Description, Type, Grading, Tags, IsLoaded, ThumbnailPath, PreviewPath)
                VALUES (@sha, @obj, @name, @author, @desc, @type, @grading, @tags, @loaded, @thumb, @preview)
            ";

            command.Parameters.AddWithValue("@sha", mod.SHA);
            command.Parameters.AddWithValue("@obj", mod.ObjectName);
            command.Parameters.AddWithValue("@name", mod.Name);
            command.Parameters.AddWithValue("@author", mod.Author ?? "");
            command.Parameters.AddWithValue("@desc", mod.Description ?? "");
            command.Parameters.AddWithValue("@type", mod.Type ?? "7z");
            command.Parameters.AddWithValue("@grading", mod.Grading ?? "G");
            command.Parameters.AddWithValue("@tags", JsonSerializer.Serialize(mod.Tags ?? new List<string>()));
            command.Parameters.AddWithValue("@loaded", mod.IsLoaded ? 1 : 0);
            command.Parameters.AddWithValue("@thumb", mod.ThumbnailPath ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@preview", mod.PreviewPath ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();
            imported++;

            if (imported % 100 == 0)
                Console.WriteLine($"Imported {imported}/{mods.Count}...");
        }

        // Commit transaction
        await transaction.CommitAsync();

        Console.WriteLine($"Import complete! Imported {imported} mods.");
    }
}

public class ExportedMod
{
    public string SHA { get; set; }
    public string ObjectName { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public string Grading { get; set; }
    public List<string> Tags { get; set; }
    public bool IsLoaded { get; set; }
    public string ThumbnailPath { get; set; }
    public string PreviewPath { get; set; }
}
```

**Step 3: Copy Files**

```powershell
# Copy mod archives
Copy-Item -Path "D:\Python_Mods\*" -Destination "D:\DotNet_Mods\" -Recurse

# Copy thumbnails
Copy-Item -Path "D:\Python_Data\thumbnails\*" -Destination "D:\DotNet_Data\thumbnails\" -Recurse

# Copy previews
Copy-Item -Path "D:\Python_Data\previews\*" -Destination "D:\DotNet_Data\previews\" -Recurse
```

**Step 4: Verify**

```bash
# Run .NET app
cd D3dxSkinManager
dotnet run

# Check:
# - Mod count matches Python
# - Random sample mods have correct data
# - Thumbnails load
# - Can load/unload mods
```

### Automated Migration Tool (Planned)

**Design:**
```
D3dxSkinManager.Migrator.exe
├─ Detects Python installation
├─ Reads Python data files
├─ Converts to .NET format
├─ Copies files
└─ Verifies migration
```

**Usage (Future):**
```bash
# Run migrator
D3dxSkinManager.Migrator.exe --from "C:\Python\d3dxSkinManage" --to "C:\DotNet\D3dxSkinManager"

# Output:
# Found Python installation
# Reading mods.json...
# Found 1234 mods
# Importing to SQLite...
# Copying files...
# Verifying...
# Migration complete!
```

---

## Feature Porting Guide

### General Process

1. **Understand Python Implementation**
   - Read Python code
   - Identify dependencies
   - Document behavior

2. **Design .NET Architecture**
   - Where does it fit?
   - What interfaces needed?
   - What services?

3. **Implement Backend**
   - Create service interface
   - Implement service
   - Add to IPC handler

4. **Implement Frontend**
   - Create React component
   - Add to UI
   - Wire up IPC calls

5. **Test**
   - Manual testing
   - Compare with Python behavior

6. **Document**
   - Update CHANGELOG.md
   - Update KEYWORDS_INDEX.md
   - Create feature doc if complex

### Example: Porting "Import Mod" Feature

**Python Implementation Analysis:**

```python
# Python code (simplified)
def import_mod(archive_path):
    # 1. Calculate SHA256
    sha = calculate_sha256(archive_path)

    # 2. Extract archive
    temp_dir = extract_archive(archive_path, f"temp/{sha}")

    # 3. Detect object name (classification)
    object_name = classify_mod(temp_dir)

    # 4. Extract metadata (if exists)
    metadata = read_metadata(temp_dir)

    # 5. Generate thumbnail
    thumbnail = generate_thumbnail(temp_dir)

    # 6. Copy to storage
    storage_path = f"mods/{sha}.7z"
    shutil.copy(archive_path, storage_path)

    # 7. Save to database
    mod = {
        'sha': sha,
        'object': object_name,
        'name': metadata.get('name', 'Unknown'),
        'author': metadata.get('author', ''),
        # ...
    }
    save_mod(mod)

    return mod
```

**.NET Implementation:**

**Step 1: Create Service Interface**

```csharp
// D3dxSkinManager/Services/IModService.cs
public interface IModService
{
    // ... existing methods ...

    /// <summary>
    /// Imports a mod from an archive file
    /// </summary>
    /// <param name="archivePath">Path to 7z/zip/rar file</param>
    /// <returns>Imported mod information</returns>
    Task<ModInfo> ImportModAsync(string archivePath);
}
```

**Step 2: Implement Service**

```csharp
// D3dxSkinManager/Services/ModService.cs
public async Task<ModInfo> ImportModAsync(string archivePath)
{
    // 1. Validate file exists
    if (!File.Exists(archivePath))
        throw new FileNotFoundException("Archive not found", archivePath);

    // 2. Calculate SHA256
    string sha = await CalculateSha256Async(archivePath);

    // 3. Check if already imported
    var existing = await GetModByShaAsync(sha);
    if (existing != null)
        throw new InvalidOperationException("Mod already imported");

    // 4. Extract archive to temp directory
    string tempDir = Path.Combine(Path.GetTempPath(), sha);
    await ExtractArchiveAsync(archivePath, tempDir);

    // 5. Classify (detect object name)
    string objectName = await ClassifyModAsync(tempDir);

    // 6. Read metadata (if exists)
    var metadata = await ReadMetadataAsync(tempDir);

    // 7. Generate thumbnail
    string thumbnailPath = await GenerateThumbnailAsync(tempDir, sha);

    // 8. Copy archive to storage
    string storagePath = Path.Combine(_modsDirectory, $"{sha}{Path.GetExtension(archivePath)}");
    File.Copy(archivePath, storagePath);

    // 9. Create mod info
    var mod = new ModInfo
    {
        SHA = sha,
        ObjectName = objectName,
        Name = metadata?.Name ?? Path.GetFileNameWithoutExtension(archivePath),
        Author = metadata?.Author ?? "",
        Description = metadata?.Description ?? "",
        Type = Path.GetExtension(archivePath).TrimStart('.'),
        Tags = metadata?.Tags ?? new List<string>(),
        IsLoaded = false,
        IsAvailable = true,
        ThumbnailPath = thumbnailPath
    };

    // 10. Save to database
    await SaveModAsync(mod);

    // 11. Cleanup temp directory
    Directory.Delete(tempDir, recursive: true);

    return mod;
}

private async Task<string> CalculateSha256Async(string filePath)
{
    using var sha256 = SHA256.Create();
    using var stream = File.OpenRead(filePath);
    var hash = await sha256.ComputeHashAsync(stream);
    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
}

// ... other helper methods ...
```

**Step 3: Add to IPC Handler**

```csharp
// D3dxSkinManager/Program.cs
private static void OnWebMessageReceived(object? sender, string message)
{
    // ... existing code ...

    case "IMPORT_MOD":
        var filePath = payload?.GetProperty("filePath").GetString();
        if (string.IsNullOrEmpty(filePath))
        {
            return new { success = false, error = "File path required" };
        }

        try
        {
            var imported = await modService.ImportModAsync(filePath);
            return new { success = true, data = imported };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
}
```

**Step 4: Add Frontend API**

```typescript
// D3dxSkinManager.Client/src/services/modService.ts
export class ModService {
  // ... existing methods ...

  /**
   * Import a mod from an archive file
   */
  async importMod(filePath: string): Promise<ModInfo> {
    return photinoService.sendMessage<ModInfo>('IMPORT_MOD', { filePath });
  }
}
```

**Step 5: Add UI Component**

```typescript
// D3dxSkinManager.Client/src/App.tsx
const App: React.FC = () => {
  // ... existing state ...

  const handleImport = async () => {
    try {
      // TODO: File picker dialog
      const filePath = 'path/to/mod.7z';  // placeholder

      setLoading(true);
      const imported = await modService.importMod(filePath);
      message.success(`Imported: ${imported.name}`);
      await loadMods();  // Refresh list
    } catch (error) {
      message.error(`Failed to import: ${(error as Error).message}`);
    } finally {
      setLoading(false);
    }
  };

  // ... in render:
  <Button onClick={handleImport} type="primary">
    Import Mod
  </Button>
};
```

**Step 6: Test**

```bash
# Manual test:
# 1. Start app
# 2. Click "Import Mod"
# 3. Select archive
# 4. Verify imported
# 5. Check database
# 6. Check thumbnails
```

**Step 7: Document**

```markdown
# In CHANGELOG.md
### Added
- Mod import functionality
  - Calculate SHA256 hash
  - Extract archives (7z, zip, rar)
  - Auto-detect object name
  - Generate thumbnails
  - File: `ModService.cs:ImportModAsync()`

# In KEYWORDS_INDEX.md
- **ImportModAsync** → `ModService.cs:148`
```

---

## Code Pattern Translation

### Common Patterns

#### Pattern: Synchronous File I/O

**Python:**
```python
with open('file.txt', 'r') as f:
    content = f.read()
```

**C# (.NET):**
```csharp
var content = await File.ReadAllTextAsync("file.txt");
```

**Key Difference:** C# uses async/await for I/O

---

#### Pattern: JSON Serialization

**Python:**
```python
import json

# Serialize
json_str = json.dumps(data, indent=2)

# Deserialize
data = json.loads(json_str)
```

**C# (.NET):**
```csharp
using System.Text.Json;

// Serialize
var jsonStr = JsonSerializer.Serialize(data, new JsonSerializerOptions
{
    WriteIndented = true
});

// Deserialize
var data = JsonSerializer.Deserialize<MyType>(jsonStr);
```

**Key Difference:** C# requires type parameter

---

#### Pattern: List Comprehension

**Python:**
```python
loaded_mods = [m for m in mods if m['is_loaded']]
```

**C# (.NET):**
```csharp
var loadedMods = mods.Where(m => m.IsLoaded).ToList();
```

**Key Difference:** C# uses LINQ

---

#### Pattern: Dictionary

**Python:**
```python
mod = {
    'sha': 'abc123',
    'name': 'My Mod'
}

print(mod['name'])
```

**C# (.NET):**
```csharp
// Option 1: Anonymous type
var mod = new
{
    SHA = "abc123",
    Name = "My Mod"
};
Console.WriteLine(mod.Name);

// Option 2: Class (better)
public class ModInfo
{
    public string SHA { get; set; }
    public string Name { get; set; }
}

var mod = new ModInfo { SHA = "abc123", Name = "My Mod" };
Console.WriteLine(mod.Name);
```

**Key Difference:** C# prefers strong types

---

#### Pattern: Error Handling

**Python:**
```python
try:
    result = risky_operation()
except FileNotFoundError as e:
    print(f"File not found: {e}")
except Exception as e:
    print(f"Error: {e}")
```

**C# (.NET):**
```csharp
try
{
    var result = await RiskyOperationAsync();
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"File not found: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

**Key Difference:** Very similar syntax

---

#### Pattern: Path Manipulation

**Python:**
```python
import os

path = os.path.join('dir', 'file.txt')
exists = os.path.exists(path)
```

**C# (.NET):**
```csharp
using System.IO;

var path = Path.Combine("dir", "file.txt");
var exists = File.Exists(path);
```

**Key Difference:** `Path.Combine` vs `os.path.join`

---

### UI Patterns

#### Pattern: Button Click Handler

**Python (Tkinter):**
```python
def on_click():
    print("Clicked!")

button = tk.Button(root, text="Click Me", command=on_click)
button.pack()
```

**React (TypeScript):**
```typescript
const onClick = () => {
  console.log("Clicked!");
};

<Button onClick={onClick}>Click Me</Button>
```

---

#### Pattern: List/Table Display

**Python (Tkinter):**
```python
tree = ttk.Treeview(root, columns=('name', 'author'))
tree.heading('name', text='Name')
tree.heading('author', text='Author')

for mod in mods:
    tree.insert('', 'end', values=(mod['name'], mod['author']))
```

**React (Ant Design):**
```typescript
const columns = [
  { title: 'Name', dataIndex: 'name', key: 'name' },
  { title: 'Author', dataIndex: 'author', key: 'author' },
];

<Table columns={columns} dataSource={mods} rowKey="sha" />
```

---

## Common Pitfalls

### 1. Forgetting Async/Await

**❌ Wrong:**
```csharp
public List<ModInfo> GetAllMods()  // Blocks!
{
    var connection = new SqliteConnection(_connectionString);
    connection.Open();  // Synchronous
    // ...
}
```

**✅ Right:**
```csharp
public async Task<List<ModInfo>> GetAllModsAsync()
{
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();  // Async
    // ...
}
```

---

### 2. Not Disposing Resources

**❌ Wrong:**
```csharp
var connection = new SqliteConnection(_connectionString);
await connection.OpenAsync();
// ... (connection never disposed!)
```

**✅ Right:**
```csharp
using var connection = new SqliteConnection(_connectionString);
await connection.OpenAsync();
// ... (connection auto-disposed)
```

---

### 3. Using `any` Type in TypeScript

**❌ Wrong:**
```typescript
const handleLoad = async (mod: any) => {  // No type safety!
  // ...
};
```

**✅ Right:**
```typescript
const handleLoad = async (mod: ModInfo) => {  // Type-safe
  // ...
};
```

---

### 4. SQL Injection

**❌ Wrong:**
```csharp
command.CommandText = $"SELECT * FROM Mods WHERE SHA = '{sha}'";  // SQL injection!
```

**✅ Right:**
```csharp
command.CommandText = "SELECT * FROM Mods WHERE SHA = @sha";
command.Parameters.AddWithValue("@sha", sha);
```

---

### 5. Blocking UI Thread

**❌ Wrong:**
```typescript
const loadMods = () => {
  // Synchronous, blocks UI
  const mods = fetchModsSync();
  setMods(mods);
};
```

**✅ Right:**
```typescript
const loadMods = async () => {
  setLoading(true);
  try {
    const mods = await modService.getAllMods();  // Async
    setMods(mods);
  } finally {
    setLoading(false);
  }
};
```

---

## Testing Migration

### Verification Checklist

After migrating feature or data:

```
Backend:
[ ] Compiles without errors
[ ] No TypeScript errors
[ ] Application starts
[ ] Feature works as expected
[ ] Database updates correctly
[ ] No exceptions in console

Frontend:
[ ] UI renders correctly
[ ] Actions trigger correctly
[ ] State updates
[ ] No console errors
[ ] Loading states work

Integration:
[ ] IPC messages work
[ ] Data flows correctly
[ ] Error handling works
[ ] Performance acceptable

Documentation:
[ ] CHANGELOG.md updated
[ ] KEYWORDS_INDEX.md updated
[ ] Feature doc created (if needed)
[ ] Code comments added
```

---

## Related Documentation

- [ORIGINAL_COMPARISON.md](ORIGINAL_COMPARISON.md) - Feature parity tracking
- [ARCHITECTURE.md](ARCHITECTURE.md) - System architecture
- [GUIDELINES.md](../ai-assistant/GUIDELINES.md) - Coding patterns
- [WORKFLOWS.md](../ai-assistant/WORKFLOWS.md) - Step-by-step procedures

---

*This migration guide evolves as more features are ported.*

*Last updated: 2026-02-17*
