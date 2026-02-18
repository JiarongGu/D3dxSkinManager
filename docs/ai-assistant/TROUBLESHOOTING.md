# AI Assistant Troubleshooting Guide

> **🤖 FOR AI ASSISTANTS:** Known issues, error messages, and solutions - fix problems fast.

**Purpose:** Quickly resolve common errors and issues without searching through code.

**Last Updated:** 2026-02-17

---

## Table of Contents

1. [Build Errors](#build-errors)
2. [Runtime Errors](#runtime-errors)
3. [Frontend Errors](#frontend-errors)
4. [Database Errors](#database-errors)
5. [Git Issues](#git-issues)
6. [Environment Issues](#environment-issues)
7. [IPC Communication Issues](#ipc-communication-issues)
8. [Performance Issues](#performance-issues)

---

## Build Errors

### Error: `CS0246: The type or namespace name 'PhotinoNET' could not be found`

**Symptom:**
```
error CS0246: The type or namespace name 'PhotinoNET' could not be found
```

**Root Cause:** Incorrect namespace for Photino.NET package

**Solution:**
```csharp
// ❌ Wrong
using PhotinoNET;

// ✅ Correct
using Photino.NET;
```

**File:** `D3dxSkinManager/Program.cs:4`

**History:** Fixed on 2026-02-17 during initial build

---

### Error: `System.ArgumentException: Icon file: icon.ico does not exist`

**Symptom:**
Application crashes on startup with icon file error

**Root Cause:** Called `.SetIconFile("icon.ico")` but file doesn't exist

**Solution:**
```csharp
// Temporary fix - comment out the line
var window = new PhotinoWindow()
    .SetTitle("D3dxSkinManager")
    .SetSize(new Size(1280, 800))
    // .SetIconFile("icon.ico")  // TODO: Add icon file later
    .Center()
```

**Long-term Solution:** Add icon.ico file to project root and ensure it's copied to output

**File:** `D3dxSkinManager/Program.cs:29`

---

### Error: `NU1900: Package vulnerability warning`

**Symptom:**
```
warning NU1900: Error occurred while getting package vulnerability data:
Unable to load the service index for source https://pkgs.dev.azure.com/...
```

**Root Cause:** Private NuGet source configured on machine (Azure DevOps)

**Solution 1 (Quick Fix):**
```bash
# Restore from official source only
dotnet restore --source https://api.nuget.org/v3/index.json
```

**Solution 2 (Permanent Fix):**
Remove private NuGet source from NuGet.config or add source filter to project

**Impact:** Warning only, doesn't block build

---

### Error: `MSB3644: The reference assemblies were not found`

**Symptom:**
```
error MSB3644: The reference assemblies for .NETFramework,Version=v8.0 were not found
```

**Root Cause:** .NET 8 SDK not installed or wrong version

**Solution:**
```bash
# Check installed versions
dotnet --version

# Should show 8.0.x
# If not, download from https://dotnet.microsoft.com/download/dotnet/8.0
```

**Required:** .NET 8.0 SDK or later

---

### Error: `CS1061: Method does not contain a definition`

**Symptom:**
```
error CS1061: 'PhotinoWindow' does not contain a definition for 'SetIconFile'
```

**Root Cause:** Using outdated Photino.NET API or wrong version

**Solution:**
```bash
# Check package version
dotnet list package

# Update to latest
dotnet add package Photino.NET --version 4.0.16
```

**Current Version:** Photino.NET 4.0.16

---

## Runtime Errors

### Bug: Migration Wizard Shows Mods But Next Button Disabled

**Symptom:**
- Migration wizard displays "59 mods found" (or any number)
- Statistics show Archive Size, Preview Size, etc.
- Next button remains disabled (grayed out)
- Cannot proceed to migration options step

**Root Cause:**
The `AnalyzeSourceAsync` method in [MigrationService.cs:46-150](D3dxSkinManager/Modules/Migration/Services/MigrationService.cs#L46-L150) throws an exception AFTER counting mods but BEFORE setting `isValid = true`. This happens when:
- Preview or cache directory scanning fails (long paths, access denied, invalid characters)
- Recursive `Directory.GetFiles()` with `SearchOption.AllDirectories` encounters permission errors
- The exception is caught, but `isValid` remains `false` while `totalMods` was already set

**Frontend Logic:**
```typescript
// MigrationWizard.tsx:596
disabled={!analysis || !analysis.isValid}
```
Button requires BOTH `analysis` to exist AND `isValid` to be `true`.

**Solution:**
Fixed in commit [date]. Wrapped each directory scanning operation in individual try-catch blocks:
- Mods directory (lines 106-116)
- Preview directory (lines 119-133)
- Cache directory (lines 136-150)

Now file access errors generate **warnings** instead of failing validation.

**Diagnosis Steps:**
1. Open browser dev tools (F12)
2. Check console for: `Migration analysis result:` log
3. Look at `errors` array in the logged object
4. Common errors:
   - "Access to the path ... is denied"
   - "The specified path, file name, or both are too long"
   - "Illegal characters in path"

**Workaround (if not fixed):**
1. Check folder permissions on Python installation
2. Remove or rename folders with very long paths
3. Check for special characters in file/folder names

**Fixed:** 2026-02-18

---

### Error: `SqliteException: SQLite Error 1: 'no such table: Mods'`

**Symptom:**
Database queries fail with "no such table" error

**Root Cause:** Database not initialized before first query

**Solution:**
Ensure `InitializeDatabaseAsync()` is called before any database operations

```csharp
public class ModService : IModService
{
    private readonly string _connectionString;

    public ModService(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        // Initialize database synchronously in constructor
        Task.Run(InitializeDatabaseAsync).Wait();
    }

    private async Task InitializeDatabaseAsync()
    {
        // Create tables and indexes
    }
}
```

**File:** `D3dxSkinManager/Services/ModService.cs:18`

---

### Error: `NullReferenceException` in IPC Handler

**Symptom:**
```
System.NullReferenceException: Object reference not set to an instance of an object
```

**Root Cause:** Sender parameter is null or message is malformed

**Solution:**
```csharp
private static void OnWebMessageReceived(object? sender, string message)
{
    var window = (PhotinoWindow?)sender;
    if (window == null)
    {
        Console.WriteLine("Error: Window is null");
        return;
    }

    if (string.IsNullOrEmpty(message))
    {
        Console.WriteLine("Error: Message is empty");
        return;
    }

    try
    {
        // Process message
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
```

**File:** `D3dxSkinManager/Program.cs:41`

---

### Error: Database is Locked

**Symptom:**
```
SqliteException: SQLite Error 5: 'database is locked'
```

**Root Cause:** Concurrent access without proper connection disposal

**Solution:**
```csharp
// ✅ Always use 'using' for connections
public async Task<List<ModInfo>> GetAllModsAsync()
{
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();

    // Query here

    // Connection automatically disposed
}
```

**Prevention:** Never share SqliteConnection instances, always create new ones

---

## Frontend Errors

### Error: `Module not found: Can't resolve 'antd'`

**Symptom:**
```
Module not found: Error: Can't resolve 'antd' in 'D:\...\src'
```

**Root Cause:** Dependencies not installed

**Solution:**
```bash
cd D3dxSkinManager.Client
npm install
```

**Verification:**
```bash
npm list antd
# Should show antd@6.3.0 or similar
```

---

### Error: `TS2339: Property 'external' does not exist on type 'Window'`

**Symptom:**
TypeScript error when accessing `window.external.sendMessage`

**Root Cause:** Missing type definition for Photino bridge

**Solution:**
Add type declaration in `photino.ts`:

```typescript
// Add at top of file
declare global {
  interface Window {
    external?: {
      sendMessage: (message: string) => void;
      receiveMessage: (callback: (message: string) => void) => void;
    };
  }
}
```

**File:** `D3dxSkinManager.Client/src/services/photino.ts:1`

---

### Error: `TypeError: Cannot read property 'sendMessage' of undefined`

**Symptom:**
Runtime error when trying to send IPC message

**Root Cause:** Running in development mode without backend, or Photino bridge not initialized

**Solution:**
The photino.ts service already has fallback for development:

```typescript
if (window.external?.sendMessage) {
    window.external.sendMessage(JSON.stringify(message));
} else {
    // Development fallback
    console.log('[Dev Mode] Simulating backend response');
    this.simulateBackendResponse(message);
}
```

**Verification:** Check console for "[Dev Mode]" messages

**File:** `D3dxSkinManager.Client/src/services/photino.ts:55`

---

### Error: `Failed to compile` with TypeScript errors

**Symptom:**
```
Failed to compile.

TS2345: Argument of type 'string' is not assignable to parameter of type 'number'
```

**Root Cause:** Type mismatch in code

**Solution:**
1. Read the error message carefully
2. Check the file and line number
3. Fix the type issue (don't use `any` or `@ts-ignore`)

**Common Fixes:**
```typescript
// ❌ Bad - type assertion without checking
const value = data as string;

// ✅ Good - type guard
if (typeof data === 'string') {
    const value = data;
}

// ❌ Bad - ignoring error
// @ts-ignore
const result = someFunction();

// ✅ Good - fixing the type
interface Result {
    id: string;
}
const result: Result = someFunction();
```

---

## Database Errors

### Error: `UNIQUE constraint failed: Mods.SHA`

**Symptom:**
```
SqliteException: SQLite Error 19: 'UNIQUE constraint failed: Mods.SHA'
```

**Root Cause:** Trying to insert mod with duplicate SHA

**Solution:**
Use INSERT OR REPLACE or check existence first:

```csharp
// Option 1: INSERT OR REPLACE
command.CommandText = @"
    INSERT OR REPLACE INTO Mods
    (SHA, ObjectName, Name, Author, Description, Tags, IsLoaded)
    VALUES (@sha, @objectName, @name, @author, @description, @tags, @isLoaded)
";

// Option 2: Check first
command.CommandText = "SELECT COUNT(*) FROM Mods WHERE SHA = @sha";
command.Parameters.AddWithValue("@sha", sha);
var exists = (long)await command.ExecuteScalarAsync() > 0;

if (!exists)
{
    // Insert
}
```

---

### Error: Cannot Read Database File

**Symptom:**
```
SqliteException: SQLite Error 14: 'unable to open database file'
```

**Root Cause:** Database path doesn't exist or no write permissions

**Solution:**
```csharp
// Ensure directory exists
var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods.db");
var dbDir = Path.GetDirectoryName(dbPath);

if (!Directory.Exists(dbDir))
{
    Directory.CreateDirectory(dbDir);
}

var connectionString = $"Data Source={dbPath}";
```

---

## Git Issues

### Error: `Device or resource busy` when renaming folders

**Symptom:**
```
mv: cannot move 'frontend' to 'D3dxSkinManager.Client': Device or resource busy
```

**Root Cause:** Process (npm, IDE, file explorer) has folder locked

**Solution 1 (Windows):**
```bash
# Use PowerShell with -Force
powershell -Command "Move-Item -Path 'frontend' -Destination 'D3dxSkinManager.Client' -Force"
```

**Solution 2:**
```bash
# 1. Stop all processes
npm stop  # Stop dev server
# Close VS Code / Visual Studio

# 2. Try rename again
mv frontend D3dxSkinManager.Client
```

**Solution 3 (Last Resort):**
```bash
# 1. Copy instead of move
cp -r frontend D3dxSkinManager.Client

# 2. Delete original after verifying
rm -rf frontend
```

---

### Error: `fatal: not a git repository`

**Symptom:**
```
fatal: not a git repository (or any of the parent directories): .git
```

**Root Cause:** Current directory is not inside a git repository

**Solution:**
```bash
# Check if in git repo
git status

# If not, initialize
git init

# Or navigate to correct directory
cd d3dxSkinManage-Rewrite
```

---

### Error: Commit Rejected by Pre-commit Hook

**Symptom:**
```
error: failed to push some refs to 'origin'
hint: Updates were rejected because the tip of your current branch is behind
```

**Root Cause:** Remote has changes you don't have locally

**Solution:**
```bash
# 1. Fetch and review changes
git fetch origin
git log origin/main..HEAD

# 2. Pull with rebase
git pull --rebase origin main

# 3. Resolve conflicts if any
# 4. Push again
git push origin feature/your-branch
```

---

## Environment Issues

### Error: `npm command not found`

**Symptom:**
```bash
bash: npm: command not found
```

**Root Cause:** Node.js not installed or not in PATH

**Solution:**
```bash
# Check Node.js installation
node --version

# If not found, install Node.js LTS from https://nodejs.org/

# Verify npm after installation
npm --version
```

**Required:** Node.js 18+ recommended

---

### Error: `dotnet command not found`

**Symptom:**
```bash
bash: dotnet: command not found
```

**Root Cause:** .NET SDK not installed or not in PATH

**Solution:**
```bash
# Check installation
dotnet --version

# If not found, install .NET 8 SDK from https://dotnet.microsoft.com/download

# Verify after installation
dotnet --info
```

**Required:** .NET 8.0 SDK or later

---

### Error: Port 3000 Already in Use

**Symptom:**
```
Error: listen EADDRINUSE: address already in use :::3000
```

**Root Cause:** Another process using port 3000

**Solution 1 (Kill Process):**
```bash
# Windows
netstat -ano | findstr :3000
taskkill /PID <PID> /F

# Linux/Mac
lsof -ti:3000 | xargs kill
```

**Solution 2 (Use Different Port):**
```bash
# Set PORT environment variable
PORT=3001 npm start
```

---

## IPC Communication Issues

### Error: Messages Not Received in Frontend

**Symptom:**
Frontend sends message but never gets response

**Root Cause 1:** Message handler not registered

**Solution:**
```typescript
// Ensure this is called during initialization
photinoService.initializeMessageReceiver();
```

**Root Cause 2:** Backend not sending response

**Solution:**
```csharp
// Always send response in backend
private static void OnWebMessageReceived(object? sender, string message)
{
    var window = (PhotinoWindow?)sender;
    if (window == null) return;

    try
    {
        // Process message
        var response = new { success = true, data = result };
        var json = JsonSerializer.Serialize(response);

        // CRITICAL: Send response back
        window.SendWebMessage(json);
    }
    catch (Exception ex)
    {
        var errorResponse = new { success = false, error = ex.Message };
        window.SendWebMessage(JsonSerializer.Serialize(errorResponse));
    }
}
```

---

### Error: `JSON.parse: unexpected character`

**Symptom:**
```
SyntaxError: JSON.parse: unexpected character at line 1 column 1
```

**Root Cause:** Malformed JSON in IPC message

**Solution:**
```csharp
// Backend: Ensure proper JSON serialization
using System.Text.Json;

var response = new
{
    success = true,
    data = myData,
    timestamp = DateTime.Now
};

var json = JsonSerializer.Serialize(response);
window.SendWebMessage(json);
```

```typescript
// Frontend: Add error handling
try {
    const response = JSON.parse(message);
    // Process response
} catch (error) {
    console.error('Failed to parse message:', message);
    console.error('Error:', error);
}
```

---

## Performance Issues

### Issue: Slow Application Startup

**Symptom:**
Application takes 5+ seconds to start

**Possible Causes:**
1. Large database initialization
2. Loading too many resources on startup
3. Synchronous I/O in constructor

**Solution:**
```csharp
// ✅ Use async initialization
public class ModService : IModService
{
    private Task _initTask;

    public ModService(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        // Start initialization but don't wait
        _initTask = InitializeDatabaseAsync();
    }

    public async Task<List<ModInfo>> GetAllModsAsync()
    {
        // Ensure initialized before queries
        await _initTask;

        // Query here
    }
}
```

---

### Issue: Frontend Freezing During Large List Rendering

**Symptom:**
UI becomes unresponsive when displaying 1000+ mods

**Solution:**
```typescript
// Use virtualization for large lists
import { Table } from 'antd';

const columns = [
    // column definitions
];

<Table
  columns={columns}
  dataSource={mods}
  rowKey="sha"
  pagination={{
    pageSize: 50,
    showSizeChanger: true,
    pageSizeOptions: ['25', '50', '100', '200']
  }}
  virtual  // Enable virtualization
  scroll={{ y: 600 }}
/>
```

---

### Issue: Database Queries Slow

**Symptom:**
Queries take >1 second for 10,000+ records

**Solution:**
```csharp
// Ensure indexes exist
command.CommandText = @"
    CREATE INDEX IF NOT EXISTS idx_object_name ON Mods(ObjectName);
    CREATE INDEX IF NOT EXISTS idx_is_loaded ON Mods(IsLoaded);
    CREATE INDEX IF NOT EXISTS idx_name ON Mods(Name);
";

// Use indexed columns in WHERE clauses
command.CommandText = @"
    SELECT * FROM Mods
    WHERE IsLoaded = 1  -- Uses idx_is_loaded
    AND ObjectName = @objectName  -- Uses idx_object_name
";
```

---

## Common Troubleshooting Checklist

When encountering any issue:

### 1. Build Issues
```bash
# Clean build
dotnet clean
dotnet restore --source https://api.nuget.org/v3/index.json
dotnet build

cd D3dxSkinManager.Client
rm -rf node_modules package-lock.json
npm install
npm run build
```

### 2. Runtime Issues
```bash
# Check logs
cd D3dxSkinManager
dotnet run  # Watch console output

# Check frontend console
# Open browser DevTools (F12) and check Console tab
```

### 3. Database Issues
```bash
# Check database
cd D3dxSkinManager/bin/Debug/net8.0
sqlite3 mods.db
.tables  # Should show: Mods
.schema Mods  # Check schema
SELECT COUNT(*) FROM Mods;  # Check record count
.quit
```

### 4. Git Issues
```bash
# Check status
git status
git branch  # Verify not on main
git log --oneline -5  # Check recent commits

# If stuck, stash changes
git stash
git status  # Should be clean
git stash pop  # Restore changes
```

---

## Getting Help

If issue not found here:

1. **Check recent changes:** See [CHANGELOG.md](../CHANGELOG.md)
2. **Search code:** Use Grep tool or [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)
3. **Check guidelines:** See [GUIDELINES.md](GUIDELINES.md) for best practices
4. **Read documentation:** See [core/](../core/) for architecture details

### Updating This File

When you solve a new issue:

1. **Document the error message** - Exact text
2. **Explain root cause** - Why it happened
3. **Provide solution** - Code examples
4. **Note affected files** - With line numbers
5. **Update "Last Updated"** - Current date

**Format:**
```markdown
### Error: Short Description

**Symptom:**
Error message or behavior

**Root Cause:** Explanation

**Solution:**
Code or steps to fix

**File:** `path/to/file.cs:123`
```

---

## Known Issues (Pending Fixes)

### Icon File Missing

**Status:** ⏳ Pending

**Issue:** Application has no icon

**Temporary Fix:** `.SetIconFile()` line commented out

**Permanent Fix:** Create icon.ico file and add to project

**File:** `D3dxSkinManager/Program.cs:29`

---

### No Tests

**Status:** ⏳ Pending

**Issue:** No unit tests or integration tests

**Impact:** Can't verify changes don't break functionality

**Plan:** Create test projects for both backend and frontend

---

### Development Mode Without Backend

**Status:** ✅ Working (Mock Data)

**Issue:** Frontend can't communicate with backend in dev mode

**Current Solution:** Mock data in photino.ts

**Future:** Consider adding API server mode for development

---

*This troubleshooting guide is maintained by AI assistants. Add issues as you encounter them!*

*Last updated: 2026-02-17*
