# Quick Start Guide

Get the d3dxSkinManage rewrite up and running in 5 minutes!

## Prerequisites Check

Before starting, verify you have:

```bash
# Check .NET SDK (need 8.0 or higher)
dotnet --version
# Should show: 8.x.x or 9.x.x

# Check Node.js (need 18 or higher)
node --version
# Should show: v18.x.x or higher

# Check npm
npm --version
# Should show: 8.x.x or higher
```

If any are missing:
- Install .NET: https://dotnet.microsoft.com/download
- Install Node.js: https://nodejs.org/

## 5-Minute Setup

### Step 1: Install Dependencies (2 min)

```bash
# Navigate to project
cd d3dxSkinManage-Rewrite

# Install .NET packages
cd D3dxSkinManager
dotnet restore

# Install React packages
cd ../frontend
npm install
```

### Step 2: Start Development Servers (3 min)

Open **TWO terminal windows**:

**Terminal 1 - React Dev Server:**
```bash
cd d3dxSkinManage-Rewrite/frontend
npm start
```
Wait for "Compiled successfully!" message and browser opens.

**Terminal 2 - Backend:**
```bash
cd d3dxSkinManage-Rewrite/D3dxSkinManager
dotnet run
```

A WebView2 window should open showing the d3dxSkinManage UI!

## What You'll See

The application opens with:
- **Header**: "d3dxSkinManage" title
- **Sidebar**: Three menu items (Mod Management, Mod Warehouse, Settings)
- **Main Content**: A table showing mods (empty initially, or mock data in dev mode)

## Development Mode Features

### Hot Reload
- **Frontend**: Edit any `.tsx` or `.ts` file → Auto-reloads instantly
- **Backend**: Edit `.cs` files → Stop (`Ctrl+C`) and restart `dotnet run`

### Mock Data
Since no mods are in the database yet, the frontend uses mock data:
- 2 sample Nahida mods
- You can click Load/Unload buttons (they work in dev mode)

### Browser DevTools
The WebView2 window uses Chromium, so you can:
- Press `F12` for DevTools
- Check console for errors
- Inspect React components

## Testing the App

### 1. Load a Mock Mod

1. Click the "Mod Management" tab
2. Find a mod in the table
3. Click the "Load" button
4. You should see a success message

### 2. Refresh the List

1. Click the "Refresh" button
2. Table should reload (with loading spinner)

### 3. Navigate Tabs

1. Click "Mod Warehouse" → Shows "Coming soon" message
2. Click "Settings" → Shows "Coming soon" message
3. Click back to "Mod Management" → Returns to mod table

## Project Structure at a Glance

```
d3dxSkinManage-Rewrite/
│
├── D3dxSkinManager/         # .NET Backend
│   ├── Program.cs              # 🔧 Entry point - Edit to add features
│   ├── Services/               # 🔧 Add your services here
│   │   ├── IModService.cs
│   │   └── ModService.cs
│   └── D3dxSkinManager.csproj
│
└── D3dxSkinManager.Client/                   # React Frontend
    ├── src/
    │   ├── App.tsx             # 🔧 Main UI - Edit to change layout
    │   ├── services/
    │   │   ├── bridgeService.ts # 🔧 C# ↔ React bridge
    │   │   └── modService.ts   # 🔧 Mod operations
    │   └── ...
    └── package.json
```

Files marked with 🔧 are the ones you'll edit most often.

## Making Your First Change

### Change the App Title

**File**: `D3dxSkinManager.Client/src/App.tsx` (line ~138)

```tsx
// Before
<div style={{ color: 'white', fontSize: '20px', fontWeight: 'bold' }}>
  d3dxSkinManage
</div>

// After
<div style={{ color: 'white', fontSize: '20px', fontWeight: 'bold' }}>
  My Cool Mod Manager
</div>
```

**Save the file** → Frontend auto-reloads → See your change instantly!

### Add a Backend Method

**File**: `D3dxSkinManager/Services/IModService.cs`

Add to interface:
```csharp
Task<int> GetModCountAsync();
```

**File**: `D3dxSkinManager/Services/ModService.cs`

Add implementation:
```csharp
public async Task<int> GetModCountAsync()
{
    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM Mods";

    var count = (long)(await command.ExecuteScalarAsync() ?? 0);
    return (int)count;
}
```

Now you have a method to count mods!

## Common Issues & Fixes

### Issue: "dotnet: command not found"
**Fix**: Install .NET SDK from https://dotnet.microsoft.com/download

### Issue: "npm: command not found"
**Fix**: Install Node.js from https://nodejs.org/

### Issue: WebView2 window is blank
**Fix**:
1. Make sure React dev server is running (`npm start`)
2. Check that you see "Compiled successfully!"
3. Try accessing http://localhost:3000 in a regular browser
4. Ensure WebView2 runtime is installed (bundled with Windows 11)

### Issue: Can't install packages (NuGet error)
**Fix**: Use official NuGet source:
```bash
dotnet restore --source https://api.nuget.org/v3/index.json
```

### Issue: Port 3000 already in use
**Fix**: Kill the process or change React port:
```bash
# Windows
npx kill-port 3000

# Or set different port
PORT=3001 npm start
```

Then update `Program.cs` line 20 to match the new port.

## Next Steps

Now that you're up and running:

1. **Read**: [ARCHITECTURE.md](ARCHITECTURE.md) - Understand the system design
2. **Read**: [README.md](README.md) - Full documentation
3. **Explore**: Look at the original Python code in `../src/` folder
4. **Build**: Start implementing features from the original app!

### Suggested First Features to Implement

1. **Import Mod** - Add ability to import a .7z file
2. **Calculate SHA** - Hash imported mods
3. **Real Database** - Connect to actual SQLite DB (not mock data)
4. **File Operations** - Copy mods to work directory
5. **Preview Images** - Display mod thumbnails

## Getting Help

- Check the [README.md](README.md) for detailed docs
- Review [ARCHITECTURE.md](ARCHITECTURE.md) for design decisions
- Look at the original Python code for reference
- Create an issue if you find bugs

## Development Tips

### Fast Iteration
1. Keep both terminals open
2. Edit frontend files for UI changes (instant reload)
3. Only restart backend when changing C# code

### Debugging
- **Frontend**: Browser DevTools (console.log)
- **Backend**: Add breakpoints in Visual Studio / VS Code

### Testing Changes
1. Make small changes
2. Test immediately
3. Commit working code frequently

Happy coding! 🚀
