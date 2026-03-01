# Development Guide

**Last Updated:** 2026-02-23
**Purpose:** Essential setup and patterns for development

---

## Tech Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Backend | .NET/C# | 10.0 |
| Frontend | React + TypeScript | 18.x + 4.9 |
| Desktop | WinForms + WebView2 | Latest |
| Database | SQLite + EF Core | Latest |
| Build | Vite | 5.x |

---

## Quick Setup

```bash
# Backend
cd D3dxSkinManager
dotnet restore
dotnet build

# Frontend
cd D3dxSkinManager.Client
npm install
npm run build

# Run application
cd D3dxSkinManager
dotnet run
```

---

## IDE Configuration

### Visual Studio Code

**Extensions Required:**
- C# Dev Kit
- TypeScript + ESLint
- Prettier

**Settings (.vscode/settings.json):**
```json
{
  "typescript.preferences.importModuleSpecifier": "relative",
  "editor.formatOnSave": true,
  "editor.defaultFormatter": "esbenp.prettier-vscode"
}
```

### Visual Studio 2022

- Install "Web Development" workload
- Enable "Format on Save": Tools → Options → Text Editor

---

## Debugging

### Backend Debugging

```json
// .vscode/launch.json
{
  "name": ".NET Core Launch",
  "type": "coreclr",
  "request": "launch",
  "program": "${workspaceFolder}/D3dxSkinManager/bin/Debug/net10.0/D3dxSkinManager.exe"
}
```

### Frontend Debugging

1. Add `debugger;` statement in code
2. Open Chrome DevTools (F12 in WebView2)
3. Use Sources tab for breakpoints

### Common Debug Points

```csharp
// Backend: IpcCommunicationHandler.cs
private async void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
{
    // Set breakpoint here for IPC debugging
}
```

```typescript
// Frontend: bridgeService.ts
sendMessage(message: BridgeMessage) {
    console.log('Sending:', message); // Debug IPC
}
```

---

## Common Tasks

### Add New Module

1. Create folder: `D3dxSkinManager/Modules/{ModuleName}/`
2. Add service extensions: `{ModuleName}ServiceExtensions.cs`
3. Register in `ApplicationHost.cs`

### Add Database Migration

```bash
dotnet ef migrations add {MigrationName}
dotnet ef database update
```

### Update Frontend Dependencies

```bash
npm update          # Update to latest minor/patch
npm outdated       # Check for major updates
```

---

## Build & Deploy

### Development Build

```bash
# Both frontend and backend
powershell -ExecutionPolicy Bypass -File build-dev.ps1
```

### Production Build

```bash
# Creates release executable
powershell -ExecutionPolicy Bypass -File build-production.ps1
# Output: bin/Release/net10.0/win-x64/
```

---

## Testing

```bash
# Backend tests
dotnet test

# Frontend tests
npm test

# Integration test
dotnet run --test-mode
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| WebView2 not loading | Install WebView2 Runtime |
| Port already in use | Kill process on port 5173 |
| Module not found | Check import paths (use relative) |
| Database locked | Close other instances |

---

## Project Structure

See [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) for detailed file organization.

## Design Patterns

See [DESIGN_DECISIONS.md](DESIGN_DECISIONS.md) for architectural constraints.