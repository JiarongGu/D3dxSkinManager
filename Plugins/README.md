# D3dxSkinManager Plugins

This directory contains official plugins ported from the original Python-based D3dxSkinManager.

> **Recent Cleanup (2026-02-20)**: Removed 13 redundant/irrelevant plugins. Core functionality now provides features previously handled by plugins.

## Active Plugins (14 total)

### 1. AddModFileWarnSize
**Status:** ✅ Complete
**Priority:** Low
**Description:** Warns users when importing files exceeding configurable size thresholds (100MB-2GB)

### 2. BatchProcessingTools
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** High
**Description:** Bulk operations framework for delete, export, import, load, unload with progress tracking

### 3. CheckModsAccident
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** Medium
**Description:** Scans for and repairs corrupted mod files for data integrity

### 4. DropfilesMultiple
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** Medium
**Description:** Enhanced drag-and-drop handling for multiple files (mods + previews)

### 5. ExportModFile
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** Medium
**Description:** Export mods as zip/7z packages with previews for distribution

### 6. HandleUserEnv
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** High
**Description:** Multi-user environment management with separate mod libraries per user

### 7. Modify3dmKey
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** High
**Description:** 3DMigoto configuration editor for d3dx.ini and help.ini key bindings

### 8. ModifyKeySwap
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** Low
**Description:** Advanced key binding management for merged mods

### 9. ModifyListOrder
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** High
**Description:** User-defined display order for classes, objects, and mods with drag-and-drop

### 10. ModifyObjectName
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** Medium
**Description:** Rename object categories with cascade updates to associated mods

### 11. SearchClassAndObject
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** High
**Description:** Advanced search with negation support and real-time filtering

### 12. UnloadObjectMods
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** Medium
**Description:** Bulk unload all mods for specific objects or categories

### 13. ViewIniConfig
**Status:** 🔧 Stub (Needs Implementation)
**Priority:** Medium
**Description:** Browse and view INI files for 3DMigoto troubleshooting

### 14. ViewThumbnail
**Status:** ✅ Complete
**Priority:** Low
**Description:** Opens redirection.json for thumbnail mapping configuration

---

## Removed Plugins (13 total)

### Redundant - Core Functionality Exists (7 plugins)

#### DeleteModCache
**Removed:** 2026-02-20
**Reason:** Core `ModFileService.DeleteCacheAsync()` provides cache deletion
**Migration:** Use core mod deletion functionality

#### CacheClearup
**Removed:** 2026-02-20
**Reason:** Core has `ModFileService.ScanCacheAsync()` with categorization (invalid/rarely/often)
**Migration:** Use core cache management

#### DeleteAndRemoveMod
**Removed:** 2026-02-20
**Reason:** Core `ModFacade.DeleteModAsync()` includes cache cleanup
**Migration:** Use DELETE IPC message

#### UnloadWithDeleteCache
**Removed:** 2026-02-20
**Reason:** Core `ModFacade.UnloadModAsync()` handles cache. Auto-delete can be a setting
**Migration:** Implement as configuration option in core

#### PreviewClearup
**Removed:** 2026-02-20
**Reason:** Core `ImageService.ClearModCacheAsync()` manages previews
**Migration:** Use core preview management

#### TempCacheCleanup
**Removed:** 2026-02-20
**Reason:** Modern architecture doesn't use temp cache directories
**Migration:** N/A - obsolete in React+.NET architecture

#### MultiplePreview
**Removed:** 2026-02-20
**Reason:** Core supports multiple previews via `ModFacade.ImportPreviewImageAsync()`
**Migration:** Use core preview import functionality

### Irrelevant - Architecture Change (6 plugins)

#### HighlightLoadingMod
**Removed:** 2026-02-20
**Reason:** UI concern should be React component logic, not backend plugin
**Migration:** Implement in React components using mod state

#### AlphaWindow
**Removed:** 2026-02-20
**Reason:** No desktop windows in React+.NET web architecture
**Migration:** N/A - not applicable

#### ScreenCapture
**Removed:** 2026-02-20
**Reason:** Browser/OS responsibility in web architecture
**Migration:** Use browser screenshot APIs or OS tools

#### AutoLogin
**Removed:** 2026-02-20
**Reason:** Authentication should be proper auth system, not plugin
**Migration:** Implement in core authentication module

#### EnforceLogout
**Removed:** 2026-02-20
**Reason:** Logout is core auth feature, not user plugin
**Migration:** Implement in core authentication module

#### DeleteIndexNoFile
**Removed:** 2026-02-20
**Reason:** Database maintenance should be core admin tool
**Migration:** Add to core database maintenance utilities

---

## Building Plugins

### Individual Plugin
```bash
cd Plugins/AddModFileWarnSize
dotnet build
```

### All Plugins
Use the build script:
```bash
cd Plugins
./build-all-plugins.ps1
```

### Output
Plugin DLLs are output to:
- `Plugins/{PluginName}/bin/Debug/net10.0/{PluginName}.dll`

### Installation
Copy plugin DLL to:
```
{ApplicationData}/plugins/
```

Example:
```bash
copy Plugins\AddModFileWarnSize\bin\Debug\net10.0\AddModFileWarnSize.dll "{AppData}\plugins\"
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
       <TargetFramework>net10.0</TargetFramework>
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
   using D3dxSkinManager.Modules.Plugins.Services;
   using D3dxSkinManager.Modules.Core.Models;

   namespace D3dxSkinManager.Plugins.MyNewPlugin;

   public class MyNewPlugin : IMessageHandlerPlugin
   {
       private IPluginContext? _context;

       public string Id => "com.yourname.mynewplugin";
       public string Name => "My New Plugin";
       public string Version => "1.0.0";
       public string Description => "Plugin description";
       public string Author => "Your Name";

       public async Task InitializeAsync(IPluginContext context)
       {
           _context = context ?? throw new ArgumentNullException(nameof(context));
           _context.Log(LogLevel.Info, $"[{Name}] Initialized");
       }

       public async Task ShutdownAsync()
       {
           _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
       }

       public IEnumerable<string> GetHandledMessageTypes()
       {
           return new[] { "MY_MESSAGE_TYPE" };
       }

       public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
       {
           // Handle message
           return new MessageResponse { Success = true };
       }
   }
   ```

4. **Build and test:**
   ```bash
   dotnet build
   copy bin\Debug\net10.0\MyNewPlugin.dll "{AppData}\plugins\"
   ```

---

## Port Status from Original Python Plugins

### Conversion Summary
- **Original Python Plugins:** 26
- **Removed (Redundant/Irrelevant):** 13 (50%)
- **Active Plugins:** 14 (50%)
  - **Fully Implemented:** 2 plugins (14%)
  - **Functional Stubs:** 12 plugins (86%)
- **Build Success Rate:** 100% (14/14)

### Implementation Priority

#### High Priority (6 plugins)
Complex functionality essential for power users:
1. **BatchProcessingTools** - Bulk mod operations
2. **SearchClassAndObject** - Advanced search filtering
3. **ModifyListOrder** - Custom display order
4. **Modify3dmKey** - 3DMigoto configuration
5. **HandleUserEnv** - Multi-user management
6. **CheckModsAccident** - Data integrity

#### Medium Priority (6 plugins)
Standard features for enhanced UX:
1. **ExportModFile** - Mod distribution
2. **DropfilesMultiple** - Enhanced drag-and-drop
3. **ModifyObjectName** - Category renaming
4. **UnloadObjectMods** - Bulk unload
5. **ViewIniConfig** - Configuration viewer
6. **CheckModsAccident** - Corruption detection

#### Low Priority (2 plugins)
Nice-to-have utilities:
1. **AddModFileWarnSize** - ✅ Complete
2. **ViewThumbnail** - ✅ Complete

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
_context.Log(LogLevel.Info, "Message");
_context.Log(LogLevel.Error, "Error message", exception);
```

---

## Troubleshooting

### Plugin Not Loading
- Check DLL is in correct directory
- Verify plugin implements `IPlugin` interface
- Check console for error messages
- Ensure .NET 10 runtime installed

### Build Errors
- Verify project references are correct
- Run `dotnet restore`
- Check namespace matches
- Update target framework to net10.0

### IPC Messages Not Working
- Verify message type in `GetHandledMessageTypes()`
- Check frontend sends correct message format
- Add logging to both frontend and backend
- Verify plugin is registered in registry

---

## Credits

Original Python plugins by:
- D3dxSkinManager community contributors

Ported to .NET + React by:
- D3dxSkinManager Team

---

## License

See main project LICENSE file.

---

**Last Updated:** 2026-02-20
**Active Plugins:** 14
**Plugin System Version:** 1.0.0
**Target Framework:** .NET 10.0
