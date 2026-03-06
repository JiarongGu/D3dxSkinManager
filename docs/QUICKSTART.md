# Quick Start Guide

Get D3dxSkinManager up and running in 5 minutes!

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
cd D3dxSkinManager

# Install .NET packages (backend)
dotnet restore

# Install React packages (frontend)
cd D3dxSkinManager.Client
npm install
```

### Step 2: Start Development Servers (3 min)

Open **TWO terminal windows**:

**Terminal 1 - React Dev Server:**
```bash
cd D3dxSkinManager/D3dxSkinManager.Client
npm start
```
Wait for "Compiled successfully!" message and browser opens.

**Terminal 2 - Backend:**
```bash
cd D3dxSkinManager/D3dxSkinManager
dotnet run
```

A WebView2 window should open showing the D3dxSkinManager UI!

## What You'll See

The application opens with:
- **Header**: "D3dxSkinManager" title with profile selector
- **Sidebar**: Multiple menu items (Mods, Launch, Workflow, Tools, Plugins, Settings)
- **Main Content**: Mod management interface with hierarchical view, list panel, and preview panel

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
D3dxSkinManager/
│
├── D3dxSkinManager/            # .NET Backend
│   ├── Program.cs                 # Entry point
│   ├── Modules/                   # Feature modules
│   │   ├── Mod/                   # Mod management
│   │   ├── Profile/               # Profile system
│   │   ├── Launch/                # Game launching
│   │   └── ...                    # Other modules
│   └── D3dxSkinManager.csproj
│
└── D3dxSkinManager.Client/     # React Frontend
    ├── src/
    │   ├── App.tsx                # Main app layout
    │   ├── modules/               # Feature modules
    │   │   ├── mod/               # Mod UI components
    │   │   ├── profile/           # Profile UI
    │   │   └── ...                # Other modules
    │   ├── shared/
    │   │   ├── services/
    │   │   │   └── bridgeService.ts  # WebView2 IPC bridge
    │   │   └── ...
    │   └── ...
    └── package.json
```

Files marked with 🔧 are the ones you'll edit most often.

## Making Your First Change

### Change the App Title

**File**: `D3dxSkinManager.Client/src/App.tsx` (line ~138)

```tsx
// Before
<div className="app-header-title">
  D3dxSkinManager
</div>

// After
<div className="app-header-title">
  My Cool Mod Manager
</div>
```

**Save the file** → Frontend auto-reloads → See your change instantly!

### Add a Backend Method

**File**: `D3dxSkinManager/Modules/Mod/Services/IModService.cs`

Add to interface:
```csharp
Task<int> GetModCountAsync(string profileId);
```

**File**: `D3dxSkinManager/Modules/Mod/Services/ModService.cs`

Add implementation:
```csharp
public async Task<int> GetModCountAsync(string profileId)
{
    var mods = await _repository.GetAllAsync(profileId);
    return mods.Count;
}
```

Now you have a method to count mods for a profile!

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
3. **Explore**: Browse the codebase to understand the modular architecture
4. **Build**: Start implementing new features or improving existing ones!

### Understanding the Codebase

Start exploring these key areas:
1. **Backend Modules** - `D3dxSkinManager/Modules/` - Feature-based modules (Mod, Profile, Launch, etc.)
2. **Frontend Modules** - `D3dxSkinManager.Client/src/modules/` - Corresponding UI components
3. **IPC Bridge** - `D3dxSkinManager.Client/src/shared/services/bridgeService.ts` - WebView2 communication
4. **Database** - `D3dxSkinManager/Data/` - SQLite with EF Core migrations
5. **Documentation** - `docs/ai-assistant/` - AI-focused development guides

## Getting Help

- Check the [README.md](README.md) for detailed docs
- Review [ARCHITECTURE.md](ARCHITECTURE.md) for design decisions
- Read [docs/ai-assistant/](ai-assistant/) guides for development patterns
- Create an issue if you find bugs

## Development Tips

### Fast Iteration
1. Keep both terminals open
2. Edit frontend files for UI changes (instant reload)
3. Only restart backend when changing C# code

### Debugging
- **Frontend**: Browser DevTools (logger.info)
- **Backend**: Add breakpoints in Visual Studio / VS Code

### Testing Changes
1. Make small changes
2. Test immediately
3. Commit working code frequently

Happy coding! 🚀
