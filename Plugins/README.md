# D3dxSkinManager Plugins

This directory contains official plugins ported from the original Python-based D3dxSkinManager.

## Plugin List

### 1. HighlightLoadingMod
**Status:** ✅ Complete (Backend + Frontend)
**Version:** 1.0.0

Highlights the currently loaded mod in the UI with visual indicators.

**Features:**
- Backend tracks loaded mod state per object
- Frontend receives events and provides highlighting API
- Custom IPC messages: `GET_LOADED_MODS_MAP`, `GET_LOADED_MOD_FOR_OBJECT`
- Events: `HIGHLIGHT_MOD_LOADED`, `HIGHLIGHT_MOD_UNLOADED`

**Files:**
- `Highlight LoadingMod/HighlightLoadingModPlugin.cs` - Backend plugin
- `HighlightLoadingMod/HighlightLoadingMod.csproj` - Build configuration
- `../D3dxSkinManager.Client/src/plugins/HighlightLoadingMod/HighlightLoadingModPlugin.tsx` - Frontend plugin

**Usage:**
```csharp
// Backend automatically tracks loaded mods
// Frontend can query via:
const loadedSha = plugin.getLoadedModForObject("CharacterName");
const isLoaded = plugin.isModLoaded(sha);
```

---

### 2. DeleteAndRemoveMod
**Status:** ✅ Complete (Backend)
**Version:** 1.0.0

Enhanced mod deletion with automatic cache cleanup.

**Features:**
- Complete deletion: database + file + cache
- Soft removal: file + cache (keeps database record)
- Auto-unload if mod is currently loaded
- Proper error handling for permissions

**Files:**
- `DeleteAndRemoveMod/DeleteAndRemoveModPlugin.cs` - Backend plugin
- `DeleteAndRemoveMod/DeleteAndRemoveMod.csproj` - Build configuration

**IPC Messages:**
- `DELETE_MOD_COMPLETE` - Full removal (data + file + cache)
  - Parameters: `{ sha: string }`
  - Response: `{ success: boolean, sha: string, modName: string }`

- `REMOVE_MOD_SOFT` - Soft removal (file + cache only)
  - Parameters: `{ sha: string }`
  - Response: `{ success: boolean, sha: string, modName: string }`

**Usage:**
```typescript
// Complete deletion
await photinoService.sendMessage('DELETE_MOD_COMPLETE', { sha: modSha });

// Soft removal
await photinoService.sendMessage('REMOVE_MOD_SOFT', { sha: modSha });
```

---

## Building Plugins

### Individual Plugin
```bash
cd Plugins/HighlightLoadingMod
dotnet build
```

### All Plugins
```bash
# From repository root
cd Plugins
dotnet build HighlightLoadingMod/HighlightLoadingMod.csproj
dotnet build DeleteAndRemoveMod/DeleteAndRemoveMod.csproj
```

### Output
Plugin DLLs are output to:
- `Plugins/{PluginName}/bin/Debug/net8.0/{PluginName}.dll`

### Installation
Copy plugin DLL to:
```
{ApplicationData}/plugins/
```

Example:
```bash
copy Plugins\HighlightLoadingMod\bin\Debug\net8.0\HighlightLoadingMod.dll "{AppData}\plugins\"
```

---

## Plugin Development

### Creating a New Plugin

1. **Create plugin directory:**
   ```bash
   mkdir Plugins/MyNewPlugin
   ```

2. **Create .csproj file:**
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net8.0</TargetFramework>
       <ImplicitUsings>enable</ImplicitUsings>
       <Nullable>enable</Nullable>
       <AssemblyName>MyNewPlugin</AssemblyName>
       <RootNamespace>D3dxSkinManager.Plugins.MyNewPlugin</RootNamespace>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="..\..\D3dxSkinManager\D3dxSkinManager.csproj" />
     </ItemGroup>
   </Project>
   ```

3. **Create plugin class:**
   ```csharp
   using D3dxSkinManager.Plugins;

   namespace D3dxSkinManager.Plugins.MyNewPlugin;

   public class MyNewPlugin : IPlugin
   {
       public string Id => "com.yourname.mynewplugin";
       public string Name => "My New Plugin";
       public string Version => "1.0.0";
       public string Description => "Plugin description";
       public string Author => "Your Name";

       public async Task InitializeAsync(IPluginContext context)
       {
           // Initialize plugin
       }

       public async Task ShutdownAsync()
       {
           // Cleanup
       }
   }
   ```

4. **Build and test:**
   ```bash
   dotnet build
   copy bin\Debug\net8.0\MyNewPlugin.dll "{AppData}\plugins\"
   ```

---

## Port Status from Original Python Plugins

### Conversion Summary
- **Total Plugins:** 26 (converted from 26 Python plugins)
- **Fully Implemented:** 9 plugins (35%)
- **Functional Stubs:** 17 plugins (65%)
- **Build Success Rate:** 100% (26/26)

### Fully Implemented ✅ (9 plugins)

1. **HighlightLoadingMod** - Highlights currently loaded mod
2. **DeleteAndRemoveMod** - Enhanced deletion with cache cleanup
3. **CacheClearup** - Intelligent cache management with categorization
4. **AddModFileWarnSize** - File size warning thresholds
5. **ViewThumbnail** - Opens thumbnail configuration viewer
6. **DeleteModCache** - Deletes mod cache (DISABLED-{SHA} folders)
7. **TempCacheCleanup** - Cleans temporary cache files
8. **PreviewClearup** - Cleans unused preview images (invalid/rarely/often)
9. **UnloadWithDeleteCache** - Auto-deletes cache on mod unload

### Functional Stubs 🔧 (17 plugins)

**High Priority - Complex Functionality:**
- **AutoLogin** - Auto-login and program launch system
- **BatchProcessingTools** - Bulk mod operations
- **SearchClassAndObject** - Real-time search filtering
- **ModifyListOrder** - Customizes display order for classes/objects/mods
- **Modify3dmKey** - Edits 3DMigoto key bindings
- **HandleUserEnv** - User environment management

**Medium Priority - Standard Features:**
- **ExportModFile** - Export mods to zip/7z with preview
- **MultiplePreview** - Multiple preview image management
- **CheckModsAccident** - Detect and fix corrupted mods
- **ModifyObjectName** - Rename object categories
- **UnloadObjectMods** - Bulk unload operations
- **ViewIniConfig** - Browse and view INI files
- **DropfilesMultiple** - Drag-and-drop handler for multiple files
- **DeleteIndexNoFile** - Delete invalid index entries

**Low Priority - UI Enhancements:**
- **AlphaWindow** - Window transparency control
- **EnforceLogout** - User logout functionality
- **ModifyKeySwap** - Key swap configuration editor

### Plugin List with Status

| # | Plugin Name | Status | Priority | Description |
|---|-------------|--------|----------|-------------|
| 1 | AddModFileWarnSize | ✅ Complete | Low | File size warnings |
| 2 | AlphaWindow | 🔧 Stub | Low | Window transparency |
| 3 | AutoLogin | 🔧 Stub | High | Auto-login system |
| 4 | BatchProcessingTools | 🔧 Stub | High | Bulk operations |
| 5 | CacheClearup | ✅ Complete | High | Cache management |
| 6 | CheckModsAccident | 🔧 Stub | Medium | Detect corruption |
| 7 | DeleteAndRemoveMod | ✅ Complete | Medium | Enhanced deletion |
| 8 | DeleteIndexNoFile | 🔧 Stub | Medium | Clean invalid index |
| 9 | DeleteModCache | ✅ Complete | Medium | Delete mod cache |
| 10 | DropfilesMultiple | 🔧 Stub | Medium | Drag-and-drop |
| 11 | EnforceLogout | 🔧 Stub | Low | Logout function |
| 12 | ExportModFile | 🔧 Stub | Medium | Export mods |
| 13 | HandleUserEnv | 🔧 Stub | High | User management |
| 14 | HighlightLoadingMod | ✅ Complete | Medium | Highlight loaded |
| 15 | Modify3dmKey | 🔧 Stub | High | Edit key bindings |
| 16 | ModifyKeySwap | 🔧 Stub | Low | Key swap config |
| 17 | ModifyListOrder | 🔧 Stub | High | Customize order |
| 18 | ModifyObjectName | 🔧 Stub | Medium | Rename objects |
| 19 | MultiplePreview | 🔧 Stub | Medium | Multi previews |
| 20 | PreviewClearup | ✅ Complete | High | Clean previews |
| 21 | SearchClassAndObject | 🔧 Stub | High | Search filter |
| 22 | TempCacheCleanup | ✅ Complete | High | Clean temp cache |
| 23 | UnloadObjectMods | 🔧 Stub | Medium | Bulk unload |
| 24 | UnloadWithDeleteCache | ✅ Complete | Medium | Auto-delete cache |
| 25 | ViewIniConfig | 🔧 Stub | Medium | View INI files |
| 26 | ViewThumbnail | ✅ Complete | Low | View thumbnails |

---

## Architecture Notes

### Original Python System
- Event-driven with `core.construct.event`
- tkinter/ttkbootstrap UI
- Direct UI manipulation in plugins
- Threading with locks for operations

### New .NET + React System
- Event-driven with `PluginEventBus`
- React + Ant Design UI
- IPC message-based communication
- Async/await for operations
- Separation of backend (C#) and frontend (React)

### Key Differences
| Original | New System |
|----------|------------|
| Direct UI access | IPC messages + events |
| `core.module.*` | `IPluginContext.*` services |
| tkinter widgets | React components |
| Python threading | C# async/await |
| Single process | Client-server architecture |

---

## Testing Plugins

### Unit Testing
```bash
cd Plugins/MyPlugin.Tests
dotnet test
```

### Integration Testing
1. Build plugin
2. Copy DLL to plugins directory
3. Run application
4. Check console for initialization messages
5. Test plugin functionality
6. Check logs for errors

### Debug Logging
Plugins can log via context:
```csharp
context.Log(LogLevel.Info, "Message");
context.Log(LogLevel.Error, "Error message", exception);
```

---

## Troubleshooting

### Plugin Not Loading
- Check DLL is in correct directory
- Verify plugin implements `IPlugin` interface
- Check console for error messages
- Ensure .NET 8 runtime installed

### Build Errors
- Verify project references are correct
- Run `dotnet restore`
- Check namespace matches

### IPC Messages Not Working
- Verify message type in `GetHandledMessageTypes()`
- Check frontend sends correct message format
- Add logging to both frontend and backend
- Verify plugin is registered in registry

---

## Credits

Original Python plugins by:
- Other contributors to D3dxSkinManager community

Ported to .NET + React by:
- D3dxSkinManager Team

---

## License

See main project LICENSE file.

---

**Last Updated:** 2026-02-17
**Plugin System Version:** 1.0.0
