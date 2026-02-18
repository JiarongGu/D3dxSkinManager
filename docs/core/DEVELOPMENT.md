# Development Guide

**Project:** D3dxSkinManager
**Version:** 1.0.0
**Last Updated:** 2026-02-17

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Initial Setup](#initial-setup)
3. [Development Workflow](#development-workflow)
4. [Building](#building)
5. [Running](#running)
6. [Testing](#testing)
7. [Debugging](#debugging)
8. [Common Tasks](#common-tasks)
9. [IDE Setup](#ide-setup)
10. [Troubleshooting Setup Issues](#troubleshooting-setup-issues)

---

## Prerequisites

### Required Software

| Software | Minimum Version | Recommended | Purpose |
|----------|----------------|-------------|---------|
| **.NET SDK** | 8.0 | 8.0.x (latest) | Backend compilation |
| **Node.js** | 18.x | 20.x LTS | Frontend build tools |
| **npm** | 9.x | 10.x | Package management |
| **Git** | 2.x | Latest | Version control |

### Optional Software

| Software | Purpose |
|----------|---------|
| **Visual Studio 2022** | Full-featured IDE (recommended for C#) |
| **Visual Studio Code** | Lightweight editor (good for TypeScript) |
| **SQLite Browser** | Database inspection |
| **PowerShell 7+** | Better shell experience (Windows) |

### System Requirements

- **OS:** Windows 10/11 (primary), Linux/Mac (experimental)
- **RAM:** 4GB minimum, 8GB recommended
- **Disk:** 2GB for tools + dependencies
- **Display:** 1280x800 minimum resolution

---

## Initial Setup

### 1. Install Prerequisites

#### .NET SDK

```bash
# Check if installed
dotnet --version

# Should show: 8.0.x
```

**If not installed:**
- Download from: https://dotnet.microsoft.com/download/dotnet/8.0
- Install SDK (not just runtime)
- Restart terminal after installation

#### Node.js & npm

```bash
# Check if installed
node --version  # Should show: v18.x or v20.x
npm --version   # Should show: 9.x or 10.x
```

**If not installed:**
- Download from: https://nodejs.org/ (LTS version)
- Install with default options
- Restart terminal after installation

### 2. Clone Repository

```bash
# Navigate to your development directory
cd D:\Development

# Clone repository (or navigate to existing)
cd d3dxSkinManage-master\d3dxSkinManage-Rewrite

# Verify you're in correct directory
ls
# Should see: D3dxSkinManager/, D3dxSkinManager.Client/, docs/, etc.
```

### 3. Restore Backend Dependencies

```bash
# Navigate to backend project
cd D3dxSkinManager

# Restore NuGet packages
dotnet restore

# If you have private NuGet sources causing issues:
dotnet restore --source https://api.nuget.org/v3/index.json

# Verify packages restored
dotnet list package
# Should show: Photino.NET, Microsoft.Data.Sqlite, Newtonsoft.Json
```

### 4. Install Frontend Dependencies

```bash
# Navigate to frontend project
cd ..\D3dxSkinManager.Client

# Install npm packages
npm install

# This may take 2-5 minutes
# Should create node_modules/ folder

# Verify installation
npm list --depth=0
# Should show: react, typescript, antd, etc.
```

### 5. Verify Setup

```bash
# Backend build test
cd ..\D3dxSkinManager
dotnet build

# Should see: Build succeeded

# Frontend build test
cd ..\D3dxSkinManager.Client
npm run build

# Should see: Compiled successfully!
```

---

## Development Workflow

### Typical Development Session

```bash
# 1. Start in project root
cd D:\Development\d3dxSkinManage-master\d3dxSkinManage-Rewrite

# 2. Check git status
git status
git branch  # Ensure on correct branch

# 3. Pull latest changes (if working with team)
git pull

# 4. Start frontend dev server (in terminal 1)
cd D3dxSkinManager.Client
npm start
# Runs on http://localhost:3000

# 5. Start backend (in terminal 2)
cd ..\D3dxSkinManager
dotnet run
# Opens Photino window pointing to localhost:3000

# 6. Make code changes
# Edit files in VS Code / Visual Studio

# 7. Hot reload
# Frontend: Saves automatically trigger reload
# Backend: Restart with Ctrl+C then `dotnet run`

# 8. Test changes
# Verify in Photino window

# 9. Commit changes (with user approval)
git add -A
# Ask user: "Ready to commit?"
git commit -m "Descriptive message"
```

### Recommended Terminal Setup

**Windows PowerShell / Terminal:**
```powershell
# Terminal 1 - Frontend
cd D:\Development\d3dxSkinManage-master\d3dxSkinManage-Rewrite\D3dxSkinManager.Client
npm start

# Terminal 2 - Backend
cd D:\Development\d3dxSkinManage-master\d3dxSkinManage-Rewrite\D3dxSkinManager
dotnet run

# Terminal 3 - Git/General
cd D:\Development\d3dxSkinManage-master\d3dxSkinManage-Rewrite
# Available for git commands, file operations
```

---

## Building

### Development Build (Debug)

**Backend:**
```bash
cd D3dxSkinManager
dotnet build

# Output: D3dxSkinManager/bin/Debug/net8.0/
```

**Frontend:**
```bash
cd D3dxSkinManager.Client
npm run build

# Output: D3dxSkinManager.Client/build/
```

### Production Build (Release)

**Using Build Script (Recommended):**
```powershell
# From project root
powershell -ExecutionPolicy Bypass -File build-production.ps1

# This script:
# 1. Builds React frontend (production mode)
# 2. Copies frontend to backend wwwroot/
# 3. Publishes .NET app (self-contained)
```

**Manual Production Build:**
```bash
# 1. Build frontend
cd D3dxSkinManager.Client
npm run build

# 2. Copy to backend
Copy-Item -Path build\* -Destination ..\D3dxSkinManager\wwwroot\ -Recurse -Force

# 3. Publish backend
cd ..\D3dxSkinManager
dotnet publish -c Release -r win-x64 --self-contained

# Output: D3dxSkinManager/bin/Release/net8.0/win-x64/publish/
```

### Build Options

**Backend Options:**
```bash
# Debug build (default)
dotnet build

# Release build (optimized)
dotnet build -c Release

# Specific runtime
dotnet build -r win-x64

# Self-contained (includes .NET runtime)
dotnet publish --self-contained true

# Framework-dependent (requires .NET installed)
dotnet publish --self-contained false
```

**Frontend Options:**
```bash
# Development build (fast, not optimized)
npm run build

# Production build (optimized, minified)
# Already configured in package.json

# Check bundle size
npm run build
# Look for: "bundle.js (123 KB)"
```

---

## Running

### Development Mode

**Option 1: Frontend Only (Mock Data)**
```bash
cd D3dxSkinManager.Client
npm start

# Opens http://localhost:3000
# Uses mock data from photino.ts
# Good for UI development
```

**Option 2: Full Stack (Frontend + Backend)**
```bash
# Terminal 1: Frontend
cd D3dxSkinManager.Client
npm start

# Terminal 2: Backend
cd D3dxSkinManager
dotnet run

# Backend opens Photino window
# Points to http://localhost:3000
# Real IPC communication
```

### Production Mode

```bash
cd D3dxSkinManager
dotnet run

# Or run the executable directly:
cd bin\Release\net8.0\win-x64\publish
.\D3dxSkinManager.exe

# Loads frontend from wwwroot/ (file:///)
```

### Running with Options

```bash
# Verbose logging
dotnet run --verbosity detailed

# Specific configuration
dotnet run -c Release

# With environment variables
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run
```

---

## Testing

### Current State

**Status:** ⏳ No tests yet

**Planned:**
- Unit tests for services
- Integration tests for database
- Component tests for React
- E2E tests with Playwright

### Running Tests (Future)

**Backend Tests:**
```bash
cd D3dxSkinManager.Tests
dotnet test

# With coverage
dotnet test /p:CollectCoverage=true
```

**Frontend Tests:**
```bash
cd D3dxSkinManager.Client
npm test

# Watch mode
npm test -- --watch

# Coverage
npm test -- --coverage
```

### Manual Testing Checklist

Until automated tests exist:

**Backend:**
- [ ] Application starts without errors
- [ ] Database initializes (`mods.db` created)
- [ ] Can query all mods
- [ ] Can load/unload mods
- [ ] IPC messages work

**Frontend:**
- [ ] UI renders correctly
- [ ] Table displays mods
- [ ] Load/Unload buttons work
- [ ] Status icons update
- [ ] No console errors

**Integration:**
- [ ] Frontend receives backend data
- [ ] Actions trigger backend operations
- [ ] UI updates after operations
- [ ] No IPC errors in console

---

## Debugging

### Backend Debugging (Visual Studio)

1. **Open Solution:**
   ```bash
   # In project root
   D3dxSkinManager.sln
   ```

2. **Set Breakpoints:**
   - Click in left margin of Program.cs
   - Set breakpoint in OnWebMessageReceived()

3. **Start Debugging:**
   - Press F5 or Debug > Start Debugging
   - Application starts with debugger attached

4. **Inspect Variables:**
   - Hover over variables
   - Use Watch window
   - Use Immediate window

5. **Step Through Code:**
   - F10 = Step Over
   - F11 = Step Into
   - Shift+F11 = Step Out

### Backend Debugging (VS Code)

1. **Create launch.json:**
   ```json
   {
     "version": "0.2.0",
     "configurations": [
       {
         "name": ".NET Core Launch",
         "type": "coreclr",
         "request": "launch",
         "preLaunchTask": "build",
         "program": "${workspaceFolder}/D3dxSkinManager/bin/Debug/net8.0/D3dxSkinManager.dll",
         "cwd": "${workspaceFolder}/D3dxSkinManager",
         "console": "internalConsole"
       }
     ]
   }
   ```

2. **Start Debugging:**
   - Press F5
   - Set breakpoints by clicking in gutter

### Frontend Debugging (Browser)

1. **Open DevTools:**
   - Press F12 in Photino window
   - Or right-click > Inspect

2. **Use Console:**
   ```typescript
   console.log('Debug info:', data);
   console.error('Error:', error);
   ```

3. **Set Breakpoints:**
   - Open Sources tab
   - Find your .tsx file
   - Click line number to set breakpoint

4. **React DevTools:**
   - Install React DevTools extension
   - Inspect component state/props
   - Profile performance

### Common Debugging Scenarios

**IPC Not Working:**
```typescript
// In photino.ts, add logging:
sendMessage<T>(type: MessageType, payload?: any): Promise<T> {
  console.log('[IPC] Sending:', type, payload);
  return new Promise((resolve, reject) => {
    // ...
  });
}

// In message receiver:
window.external?.receiveMessage((message: string) => {
  console.log('[IPC] Received:', message);
  // ...
});
```

```csharp
// In Program.cs:
private static void OnWebMessageReceived(object? sender, string message)
{
    Console.WriteLine($"[IPC] Received: {message}");
    // ...
    Console.WriteLine($"[IPC] Sending: {json}");
    window.SendWebMessage(json);
}
```

**Database Issues:**
```csharp
// Add logging to ModService:
public async Task<List<ModInfo>> GetAllModsAsync()
{
    Console.WriteLine($"[DB] Connecting to: {_connectionString}");
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();
    Console.WriteLine("[DB] Connected successfully");

    var command = connection.CreateCommand();
    command.CommandText = "SELECT * FROM Mods";
    Console.WriteLine($"[DB] Executing: {command.CommandText}");

    // ...
}
```

---

## Common Tasks

### Adding a New Backend Service

1. **Create Interface:**
   ```bash
   # Create file: D3dxSkinManager/Services/IMyService.cs
   ```

   ```csharp
   namespace D3dxSkinManager.Services
   {
       public interface IMyService
       {
           Task<string> DoSomethingAsync();
       }
   }
   ```

2. **Create Implementation:**
   ```bash
   # Create file: D3dxSkinManager/Services/MyService.cs
   ```

   ```csharp
   namespace D3dxSkinManager.Services
   {
       public class MyService : IMyService
       {
           public async Task<string> DoSomethingAsync()
           {
               // Implementation
               return "Done";
           }
       }
   }
   ```

3. **Register in Program.cs:**
   ```csharp
   var myService = new MyService();

   // Use in IPC handler:
   case "MY_ACTION":
       var result = await myService.DoSomethingAsync();
       return new { success = true, data = result };
   ```

4. **Update Documentation:**
   - Add to `KEYWORDS_INDEX.md`
   - Add to `CHANGELOG.md`

### Adding a Frontend Component

1. **Create Component File:**
   ```bash
   # Create: D3dxSkinManager.Client/src/MyComponent.tsx
   ```

   ```typescript
   import React from 'react';

   interface MyComponentProps {
     title: string;
   }

   const MyComponent: React.FC<MyComponentProps> = ({ title }) => {
     return (
       <div>
         <h2>{title}</h2>
       </div>
     );
   };

   export default MyComponent;
   ```

2. **Use in App.tsx:**
   ```typescript
   import MyComponent from './MyComponent';

   // In render:
   <MyComponent title="Hello" />
   ```

3. **Update Documentation:**
   - Add to `KEYWORDS_INDEX.md`

### Adding a Database Column

1. **Update InitializeDatabaseAsync:**
   ```csharp
   command.CommandText = @"
       CREATE TABLE IF NOT EXISTS Mods (
           -- Existing columns
           SHA TEXT PRIMARY KEY,
           Name TEXT NOT NULL,
           -- New column:
           NewColumn TEXT,
           -- ...
       );
   ";
   ```

2. **Update ModInfo Class:**
   ```csharp
   public class ModInfo
   {
       // Existing properties
       public string SHA { get; set; }
       public string Name { get; set; }
       // New property:
       public string? NewColumn { get; set; }
   }
   ```

3. **Update MapToModInfo:**
   ```csharp
   private ModInfo MapToModInfo(SqliteDataReader reader)
   {
       return new ModInfo
       {
           // Existing mappings
           SHA = reader.GetString(0),
           Name = reader.GetString(2),
           // New mapping:
           NewColumn = reader.IsDBNull(x) ? null : reader.GetString(x)
       };
   }
   ```

4. **Handle Migration:**
   - Existing databases won't have new column
   - Use `ALTER TABLE` for existing databases
   - Or delete `mods.db` and regenerate

5. **Update Frontend:**
   ```typescript
   interface ModInfo {
     // Existing fields
     sha: string;
     name: string;
     // New field:
     newColumn?: string;
   }
   ```

---

## IDE Setup

### Visual Studio 2022

**Recommended Extensions:**
- ReSharper (optional, enhances C# editing)
- Web Essentials (CSS, HTML, JS)

**Settings:**
- Tools > Options > Text Editor > C# > Tabs: 4 spaces
- Tools > Options > Environment > Tabs: 2 spaces (for TS/TSX)

**Solution Explorer:**
- Right-click solution > Properties
- Set startup project: D3dxSkinManager

### Visual Studio Code

**Recommended Extensions:**
```
# Essential
ms-dotnettools.csharp          # C# support
ms-vscode.vscode-typescript    # TypeScript support
dbaeumer.vscode-eslint         # Linting
esbenp.prettier-vscode         # Formatting

# Helpful
ms-vscode.vscode-typescript    # React IntelliSense
eamodio.gitlens                # Git integration
ms-azuretools.vscode-docker    # Docker support
```

**Settings (.vscode/settings.json):**
```json
{
  "editor.formatOnSave": true,
  "editor.defaultFormatter": "esbenp.prettier-vscode",
  "[csharp]": {
    "editor.tabSize": 4
  },
  "[typescript]": {
    "editor.tabSize": 2
  },
  "[typescriptreact]": {
    "editor.tabSize": 2
  }
}
```

**Launch Configuration (.vscode/launch.json):**
See [Debugging](#backend-debugging-vs-code) section

---

## Troubleshooting Setup Issues

### "dotnet command not found"

**Problem:** .NET SDK not in PATH

**Solution:**
```bash
# Windows: Add to PATH
# Control Panel > System > Advanced > Environment Variables
# Add: C:\Program Files\dotnet\

# Verify:
dotnet --version
```

### "npm command not found"

**Problem:** Node.js not in PATH

**Solution:**
```bash
# Reinstall Node.js with default options
# Download from: https://nodejs.org/

# Verify:
node --version
npm --version
```

### NuGet Restore Fails

**Problem:** Private package sources configured

**Solution:**
```bash
# Use only official source:
dotnet restore --source https://api.nuget.org/v3/index.json

# Or edit NuGet.config to remove private sources
```

### npm install Fails

**Problem:** Network issues or corrupted cache

**Solution:**
```bash
# Clear npm cache
npm cache clean --force

# Delete node_modules and package-lock.json
rm -rf node_modules package-lock.json

# Reinstall
npm install
```

### Frontend Won't Start

**Problem:** Port 3000 in use

**Solution:**
```bash
# Kill process on port 3000 (Windows)
netstat -ano | findstr :3000
taskkill /PID <PID> /F

# Or use different port
PORT=3001 npm start
```

### Backend Crashes on Startup

**Problem:** Missing icon file or database issues

**Solution:**
```bash
# Check Program.cs has icon line commented:
// .SetIconFile("icon.ico")

# Delete and regenerate database:
rm bin\Debug\net8.0\mods.db
dotnet run
```

---

## Additional Resources

- [ARCHITECTURE.md](ARCHITECTURE.md) - System architecture
- [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) - File organization
- [TROUBLESHOOTING.md](../ai-assistant/TROUBLESHOOTING.md) - Common errors
- [WORKFLOWS.md](../ai-assistant/WORKFLOWS.md) - Step-by-step procedures
- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [React Documentation](https://react.dev/)
- [Photino.NET Docs](https://www.tryphotino.io/)

---

*This development guide is maintained as tools and processes evolve.*

*Last updated: 2026-02-17*
