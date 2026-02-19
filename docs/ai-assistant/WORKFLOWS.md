# AI Assistant Workflows

> **🤖 FOR AI ASSISTANTS:** Step-by-step procedures for common development tasks.

**Purpose:** Detailed workflows to ensure consistency and avoid mistakes.

**Last Updated:** 2026-02-19

---

## Table of Contents

1. [Git Workflow](#git-workflow)
2. [Adding Backend Service](#adding-backend-service)
3. [Adding Frontend Component](#adding-frontend-component)
4. [Adding IPC Message Type](#adding-ipc-message-type)
5. [Database Schema Changes](#database-schema-changes)
6. [Creating Feature Documentation](#creating-feature-documentation)
7. [Debugging Issues](#debugging-issues)

---

## Git Workflow

### Before Making ANY Changes

```bash
# 1. Check current branch
git branch

# 2. Verify you're NOT on main
#    Current branch will have * next to it
#    If on main, STOP and create feature branch

# 3. If on main, create feature branch
git checkout -b feature/your-feature-name

# 4. Verify branch creation
git branch
```

### Creating Feature Branch

```bash
# Branch naming conventions:
# feature/description - New features
# bugfix/description - Bug fixes
# docs/description - Documentation only

# Example:
git checkout -b feature/add-classification-system
git checkout -b bugfix/fix-mod-loading
git checkout -b docs/update-architecture
```

### Committing Changes

**⚠️ NEVER commit without user approval!**

```bash
# 1. Make your changes

# 2. Build and test
cd D3dxSkinManager
dotnet build
cd ../D3dxSkinManager.Client
npm run build

# 3. Check status
git status

# 4. Review changes
git diff

# 5. Stage changes
git add -A

# 6. **ASK USER FOR APPROVAL**
#    "Ready to commit? Branch: feature/xyz
#     Changes: [list files]
#     Commit message: [your message]"

# 7. **WAIT FOR USER RESPONSE**

# 8. After approval, commit
git commit -m "Your descriptive commit message"

# 9. Push to remote (if user approves)
git push -u origin feature/your-feature-name
```

### Commit Message Format

```
Type: Brief description (50 chars max)

- Detailed point 1
- Detailed point 2
- References: #issue-number

Files changed:
- path/to/file1.cs
- path/to/file2.tsx
```

**Types:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`

**Example:**
```
feat: Add classification system for mods

- Implemented ClassificationService with pattern matching
- Added UI for managing classifications
- Created classification table in database

Files changed:
- D3dxSkinManager/Services/ClassificationService.cs
- D3dxSkinManager.Client/src/components/Classifications.tsx
```

---

## Adding Backend Service

### Step 1: Create Service Interface

**File:** `D3dxSkinManager/Services/IYourService.cs`

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace D3dxSkinManager.Services
{
    /// <summary>
    /// Interface for [service purpose]
    /// </summary>
    public interface IYourService
    {
        /// <summary>
        /// [Method description]
        /// </summary>
        Task<List<YourModel>> GetAllAsync();

        /// <summary>
        /// [Method description]
        /// </summary>
        Task<bool> CreateAsync(YourModel model);
    }

    /// <summary>
    /// [Model description]
    /// </summary>
    public class YourModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        // Add properties
    }
}
```

### Step 2: Implement Service

**File:** `D3dxSkinManager/Services/YourService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace D3dxSkinManager.Services
{
    /// <summary>
    /// Implementation of [service purpose]
    /// </summary>
    public class YourService : IYourService
    {
        private readonly string _connectionString;

        public YourService(string dataPath)
        {
            _connectionString = $"Data Source={dataPath}/database.db";
            InitializeDatabaseAsync().Wait();
        }

        private async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS YourTable (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL
                );
            ";
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<YourModel>> GetAllAsync()
        {
            var results = new List<YourModel>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM YourTable";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new YourModel
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1)
                });
            }

            return results;
        }

        public async Task<bool> CreateAsync(YourModel model)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO YourTable (Id, Name)
                VALUES (@id, @name)
            ";
            command.Parameters.AddWithValue("@id", model.Id);
            command.Parameters.AddWithValue("@name", model.Name);

            var affected = await command.ExecuteNonQueryAsync();
            return affected > 0;
        }
    }
}
```

### Step 3: Register in Program.cs (Future)

```csharp
// When dependency injection is added
var yourService = new YourService(dataPath);
```

### Step 4: Update Documentation

1. Add to `docs/KEYWORDS_INDEX.md`:
   ```markdown
   - **YourService** → `D3dxSkinManager/Services/YourService.cs`
     - GetAllAsync → `:45`
     - CreateAsync → `:62`
   ```

2. Add to `docs/CHANGELOG.md`:
   ```markdown
   ### Added
   - YourService for [purpose]
     - File: `D3dxSkinManager/Services/YourService.cs`
   ```

3. Create feature doc: `docs/features/YOUR_FEATURE.md`

---

## Adding Frontend Component

### Step 1: Create Component File

**File:** `D3dxSkinManager.Client/src/components/YourComponent.tsx`

```typescript
import React, { useState, useEffect } from 'react';
import { Table, message } from 'antd';
import { CompactButton } from '../../shared/components/compact';  // ⭐ Use Compact components
import type { ColumnsType } from 'antd/es/table';

interface YourComponentProps {
  // Define props
  title?: string;
}

interface YourData {
  id: string;
  name: string;
}

const YourComponent: React.FC<YourComponentProps> = ({ title = 'Default Title' }) => {
  const [data, setData] = useState<YourData[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      // Load data from service
      const result = await yourService.getData();
      setData(result);
    } catch (error: unknown) {  // ⭐ Always use 'error: unknown'
      const errorMessage = error instanceof Error ? error.message : 'An unexpected error occurred';
      message.error(`Failed to load data: ${errorMessage}`);
      console.error('Load data error:', error);
    } finally {
      setLoading(false);
    }
  };

  const columns: ColumnsType<YourData> = [
    {
      title: 'ID',
      dataIndex: 'id',
      key: 'id',
    },
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
    },
  ];

  return (
    <div>
      <h2>{title}</h2>
      <Table
        columns={columns}
        dataSource={data}
        rowKey="id"
        loading={loading}
      />
    </div>
  );
};

export default YourComponent;
```

### Step 2: Import and Use Component

**File:** `D3dxSkinManager.Client/src/App.tsx`

```typescript
import YourComponent from './components/YourComponent';

// In your render:
<YourComponent title="My Feature" />
```

### Step 3: Update Documentation

1. Add to `docs/KEYWORDS_INDEX.md`:
   ```markdown
   - **YourComponent** → `D3dxSkinManager.Client/src/components/YourComponent.tsx`
   ```

2. Add to `docs/CHANGELOG.md`

---

## Creating Type-Safe Frontend Service ⭐ NEW (2026-02-19)

### Step 1: Create Service Class

**File:** `D3dxSkinManager.Client/src/modules/{module}/services/{module}Service.ts`

```typescript
import { BaseModuleService } from '../../../shared/services/baseModuleService';
import type { ModuleName } from '../../../shared/types/message.types';

// Define request/response types
export interface YourRequest {
  name: string;
  description?: string;
}

export interface YourResponse {
  id: string;
  name: string;
  createdAt: string;
}

export class YourModuleService extends BaseModuleService {
  constructor() {
    super('YOUR_MODULE' as ModuleName);  // Use ModuleName union type
  }

  // Type-safe method with generic payload types
  async create(request: YourRequest): Promise<YourResponse> {
    return this.sendMessage<YourResponse, YourRequest>(
      'CREATE',
      undefined,  // profileId if needed
      request
    );
  }

  async getAll(profileId?: string): Promise<YourResponse[]> {
    return this.sendArrayMessage<YourResponse>(
      'GET_ALL',
      profileId
    );
  }

  async delete(id: string, profileId?: string): Promise<boolean> {
    return this.sendBooleanMessage<{ id: string }>(
      'DELETE',
      profileId,
      { id }
    );
  }
}

// Export singleton instance
export const yourModuleService = new YourModuleService();
```

### Step 2: Add ModuleName Type

**File:** `D3dxSkinManager.Client/src/shared/types/message.types.ts`

```typescript
export type ModuleName =
  | 'MOD'
  | 'PROFILE'
  | 'SETTINGS'
  | 'YOUR_MODULE'  // ⭐ Add your module here
  | ...;
```

### Step 3: Use Service in Component

```typescript
import { yourModuleService } from '../services/yourModuleService';
import type { YourRequest } from '../services/yourModuleService';

const MyComponent: React.FC = () => {
  const handleCreate = async () => {
    try {
      const request: YourRequest = {
        name: 'New Item',
        description: 'Optional description'
      };

      const result = await yourModuleService.create(request);
      message.success(`Created: ${result.name}`);
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : 'Creation failed';
      message.error(errorMessage);
      console.error('Create error:', error);
    }
  };

  return <CompactButton onClick={handleCreate}>Create</CompactButton>;
};
```

**Key Points:**
- Use dual generic parameters: `<TResponse, TPayload>`
- Define specific request/response interfaces
- ModuleName must be union type member
- Always use `error: unknown` in catch blocks
- Export singleton service instance

---

## Adding IPC Message Type

### Step 1: Add TypeScript Type

**File:** `D3dxSkinManager.Client/src/services/photino.ts`

```typescript
export type MessageType =
  | 'GET_ALL_MODS'
  | 'LOAD_MOD'
  | 'UNLOAD_MOD'
  | 'YOUR_NEW_MESSAGE'  // Add here
  | ...;
```

### Step 2: Handle in Backend

**File:** `D3dxSkinManager/Program.cs`

```csharp
private static void OnWebMessageReceived(object? sender, string message)
{
    var window = (PhotinoWindow?)sender;
    if (window == null) return;

    try
    {
        // Parse message
        var msg = JsonSerializer.Deserialize<IpcMessage>(message);

        object? response = msg.Type switch
        {
            "GET_ALL_MODS" => await GetAllMods(),
            "YOUR_NEW_MESSAGE" => await YourNewHandler(msg.Payload),  // Add here
            _ => new { error = "Unknown message type" }
        };

        var json = JsonSerializer.Serialize(new {
            id = msg.Id,
            success = true,
            data = response
        });
        window.SendWebMessage(json);
    }
    catch (Exception ex)
    {
        // Error handling
    }
}

private static async Task<object> YourNewHandler(object? payload)
{
    // Implementation
    return new { result = "success" };
}
```

### Step 3: Create Frontend Service Method

**File:** `D3dxSkinManager.Client/src/services/yourService.ts`

```typescript
import { photinoService } from './photino';

interface YourRequest {
  param1: string;
  param2: number;
}

interface YourResponse {
  result: string;
}

export class YourService {
  async yourMethod(request: YourRequest): Promise<YourResponse> {
    return photinoService.sendMessage<YourResponse>(
      'YOUR_NEW_MESSAGE',
      request
    );
  }
}

export const yourService = new YourService();
```

### Step 4: Update Documentation

Add IPC message to documentation with request/response format.

---

## Database Schema Changes

### Step 1: Update InitializeDatabaseAsync

**File:** `D3dxSkinManager/Services/YourService.cs`

```csharp
private async Task InitializeDatabaseAsync()
{
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = @"
        -- Check if column exists
        PRAGMA table_info(YourTable);

        -- Add new column if doesn't exist
        ALTER TABLE YourTable ADD COLUMN NewColumn TEXT;

        -- Create index
        CREATE INDEX IF NOT EXISTS idx_new_column ON YourTable(NewColumn);
    ";
    await command.ExecuteNonQueryAsync();
}
```

### Step 2: Update Model Class

```csharp
public class YourModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NewColumn { get; set; }  // Add new property
}
```

### Step 3: Update All Queries

Update SELECT, INSERT, UPDATE statements to include new column.

### Step 4: Document Migration

**File:** `docs/CHANGELOG.md`

```markdown
### Changed
- Database schema: Added NewColumn to YourTable
  - Migration: Existing databases will auto-migrate
  - File: `D3dxSkinManager/Services/YourService.cs:30`
```

---

## Creating Feature Documentation

### Step 1: Create Feature Doc

**File:** `docs/features/YOUR_FEATURE.md`

```markdown
# Feature Name

**Status:** ✅ Complete / ⏳ In Progress / 📋 Planned
**Added:** 2026-02-17
**Location:** `path/to/main/file.cs`

## Overview

Brief description of what this feature does and why it exists.

## User Story

As a [user type], I want to [action] so that [benefit].

## Implementation

### Backend

- **Service:** `YourService.cs`
- **Methods:**
  - `GetAllAsync()` - Retrieves all items
  - `CreateAsync()` - Creates new item

### Frontend

- **Component:** `YourComponent.tsx`
- **Features:**
  - Display list
  - Add new items
  - Edit existing items

### Database

- **Table:** `YourTable`
- **Columns:**
  - Id (TEXT PRIMARY KEY)
  - Name (TEXT NOT NULL)

## Usage Examples

### Backend

\`\`\`csharp
var service = new YourService(dataPath);
var items = await service.GetAllAsync();
\`\`\`

### Frontend

\`\`\`typescript
import { yourService } from './services/yourService';

const items = await yourService.getAll();
\`\`\`

## Testing

- Manual test: [Steps to test]
- Edge cases: [Known edge cases]

## Future Improvements

- [ ] Feature enhancement 1
- [ ] Feature enhancement 2

## Related Files

- Backend: `D3dxSkinManager/Services/YourService.cs`
- Frontend: `D3dxSkinManager.Client/src/components/YourComponent.tsx`
- Documentation: `docs/features/YOUR_FEATURE.md`

## References

- [Original Python implementation](../path)
- [Related feature](#)
```

### Step 2: Update Features Index

**File:** `docs/features/README.md`

Add entry to feature list:
```markdown
- [Your Feature](YOUR_FEATURE.md) - Brief description
```

### Step 3: Update KEYWORDS_INDEX.md

Add all relevant classes, files, and methods.

---

## Debugging Issues

### Backend Issues

1. **Check console output:**
   ```bash
   cd D3dxSkinManager
   dotnet run
   # Watch for errors in console
   ```

2. **Add logging:**
   ```csharp
   Console.WriteLine($"Debug: Variable value = {value}");
   ```

3. **Use try-catch:**
   ```csharp
   try {
       // Problematic code
   } catch (Exception ex) {
       Console.WriteLine($"Error: {ex.Message}");
       Console.WriteLine($"Stack: {ex.StackTrace}");
   }
   ```

### Frontend Issues

1. **Check browser console:**
   - Open DevTools (F12)
   - Check Console tab for errors

2. **Add console.log:**
   ```typescript
   console.log('Debug: data =', data);
   console.error('Error occurred:', error);
   ```

3. **Check network tab:**
   - See if IPC messages are being sent/received

### Database Issues

1. **Check if database file exists:**
   ```bash
   ls D3dxSkinManager/bin/Debug/net8.0/mods.db
   ```

2. **Query database directly:**
   ```bash
   sqlite3 mods.db
   .tables
   SELECT * FROM Mods;
   .quit
   ```

3. **Check schema:**
   ```bash
   sqlite3 mods.db
   .schema Mods
   ```

---

## Common Task Checklist

### Before Starting

- [ ] Read relevant documentation
- [ ] Check `docs/KEYWORDS_INDEX.md` for existing code
- [ ] Review `docs/CHANGELOG.md` for recent changes
- [ ] Check current git branch

### During Development

- [ ] Follow coding guidelines
- [ ] Add proper error handling
- [ ] Add console logging for debugging
- [ ] Test as you go

### Before Committing

- [ ] Build backend: `dotnet build`
- [ ] Build frontend: `npm run build`
- [ ] Test functionality
- [ ] Update `docs/CHANGELOG.md`
- [ ] Update `docs/KEYWORDS_INDEX.md` (if new files)
- [ ] Create/update feature documentation
- [ ] Ask user for commit approval

### After Committing

- [ ] Update AI_GUIDE.md if learned something new
- [ ] Add to TROUBLESHOOTING.md if solved a tricky issue
- [ ] Update GUIDELINES.md if discovered a pattern

---

*This workflow guide is maintained by AI assistants. Add new workflows as you discover them!*

*Last updated: 2026-02-17*
