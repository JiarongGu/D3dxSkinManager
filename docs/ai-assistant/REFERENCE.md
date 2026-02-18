# AI Assistant Reference

> **🤖 FOR AI ASSISTANTS:** Quick command and setting lookup - no explanations, just commands.

**Purpose:** Fast reference for common commands, paths, and settings.

**Last Updated:** 2026-02-17

---

## Quick Navigation

| Section | Contents |
|---------|----------|
| [Commands](#commands) | Build, run, test commands |
| [Paths](#file-paths) | Important file locations |
| [Namespaces](#namespaces) | C# and TypeScript namespaces |
| [Ports & URLs](#ports--urls) | Development server addresses |
| [Database](#database-commands) | SQLite commands |
| [Git](#git-commands) | Git quick reference |

---

## Commands

### Backend (.NET)

```bash
# Navigate to backend
cd D3dxSkinManager

# Build
dotnet build

# Run
dotnet run

# Clean
dotnet clean

# Restore packages
dotnet restore

# Restore from official source only
dotnet restore --source https://api.nuget.org/v3/index.json

# Build release
dotnet build -c Release

# Publish
dotnet publish -c Release -r win-x64 --self-contained
```

### Frontend (React)

```bash
# Navigate to frontend
cd D3dxSkinManager.Client

# Install dependencies
npm install

# Start dev server
npm start

# Build production
npm run build

# Run tests
npm test

# Check for vulnerabilities
npm audit

# Fix vulnerabilities
npm audit fix
```

### Both (Production Build)

```bash
# From project root
powershell -ExecutionPolicy Bypass -File build-production.ps1
```

---

## File Paths

### Backend

```
D3dxSkinManager/
├── Program.cs                          # Entry point
├── Services/
│   ├── IModService.cs                  # Mod service interface
│   └── ModService.cs                   # Mod service implementation
└── D3dxSkinManager.csproj              # Project file
```

### Frontend

```
D3dxSkinManager.Client/
├── src/
│   ├── App.tsx                         # Main component
│   ├── services/
│   │   ├── photino.ts                  # C# bridge
│   │   └── modService.ts               # Mod API wrapper
│   └── index.tsx                       # Entry point
├── public/
│   └── index.html                      # HTML template
├── package.json                        # Dependencies
└── tsconfig.json                       # TypeScript config
```

### Documentation

```
docs/
├── README.md                           # Developer hub
├── AI_GUIDE.md                         # AI hub
├── CHANGELOG.md                        # Change log
├── KEYWORDS_INDEX.md                   # Quick lookup
├── ai-assistant/                       # AI guides
├── core/                               # Core docs
└── features/                           # Feature docs
```

### Root

```
d3dxSkinManage-Rewrite/
├── D3dxSkinManager.sln                 # Solution file
├── D3dxSkinManager/                    # Backend
├── D3dxSkinManager.Client/             # Frontend
├── docs/                               # Documentation
├── build-production.ps1                # Build script
├── .gitignore                          # Git ignore
├── README.md                           # Main README
├── QUICKSTART.md                       # Quick start
└── ARCHITECTURE.md                     # Architecture
```

---

## Namespaces

### C# (Backend)

```csharp
using Photino.NET;                      // Photino window
using System;                           // System utilities
using System.Drawing;                   // Graphics (Size, etc.)
using System.IO;                        // File I/O
using Microsoft.Data.Sqlite;            // SQLite
using Newtonsoft.Json;                  // JSON serialization

namespace D3dxSkinManager { }           // Root namespace
namespace D3dxSkinManager.Services { }  // Services namespace
```

### TypeScript (Frontend)

```typescript
import React from 'react';              // React
import { useState, useEffect } from 'react';  // Hooks
import { Button, Table, message } from 'antd';  // Ant Design
import type { ColumnsType } from 'antd/es/table';  // Types
```

---

## Ports & URLs

### Development

| Service | URL | Port |
|---------|-----|------|
| React Dev Server | http://localhost:3000 | 3000 |
| Photino Window | Points to React or file:/// | N/A |

### Production

| Service | URL |
|---------|-----|
| Photino Window | file:///path/to/wwwroot/index.html |

---

## Database Commands

### SQLite CLI

```bash
# Open database
sqlite3 D3dxSkinManager/bin/Debug/net8.0/mods.db

# List tables
.tables

# Show schema
.schema Mods

# Query data
SELECT * FROM Mods;

# Query with WHERE
SELECT * FROM Mods WHERE IsLoaded = 1;

# Count records
SELECT COUNT(*) FROM Mods;

# Exit
.quit
```

### C# (In Code)

```csharp
// Connection string
var connectionString = "Data Source=path/to/mods.db";

// Open connection
using var connection = new SqliteConnection(connectionString);
await connection.OpenAsync();

// Create command
var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM Mods WHERE SHA = @sha";
command.Parameters.AddWithValue("@sha", sha);

// Execute query
using var reader = await command.ExecuteReaderAsync();

// Execute non-query
var affected = await command.ExecuteNonQueryAsync();
```

---

## Git Commands

### Branch Operations

```bash
# Check current branch
git branch

# Create and switch to new branch
git checkout -b feature/description

# Switch to existing branch
git checkout branch-name

# List all branches
git branch -a

# Delete local branch
git branch -d branch-name
```

### Status & Changes

```bash
# Check status
git status

# View changes
git diff

# View staged changes
git diff --cached

# View specific file
git diff path/to/file
```

### Staging & Committing

```bash
# Stage all changes
git add -A

# Stage specific file
git add path/to/file

# Unstage file
git restore --staged path/to/file

# Commit (ONLY AFTER USER APPROVAL!)
git commit -m "Your message"

# Amend last commit
git commit --amend
```

### Remote Operations

```bash
# Push to remote (first time)
git push -u origin branch-name

# Push subsequent changes
git push

# Pull changes
git pull

# Fetch remote branches
git fetch
```

### History

```bash
# View commit history
git log

# View one-line history
git log --oneline

# View last 5 commits
git log -5

# View changes in commit
git show commit-hash
```

---

## Package Versions

### Backend NuGet Packages

```xml
<PackageReference Include="Photino.NET" Version="4.0.16" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.3" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
```

### Frontend npm Packages

```json
{
  "react": "^19.2.4",
  "react-dom": "^19.2.4",
  "typescript": "^4.9.5",
  "antd": "^6.3.0",
  "axios": "^1.13.5",
  "react-router-dom": "^7.13.0"
}
```

---

## Configuration Files

### TypeScript Config

**File:** `D3dxSkinManager.Client/tsconfig.json`

```json
{
  "compilerOptions": {
    "target": "es5",
    "lib": ["dom", "dom.iterable", "esnext"],
    "allowJs": true,
    "skipLibCheck": true,
    "esModuleInterop": true,
    "allowSyntheticDefaultImports": true,
    "strict": true,
    "forceConsistentCasingInFileNames": true,
    "module": "esnext",
    "moduleResolution": "node",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx"
  }
}
```

### .NET Project File

**File:** `D3dxSkinManager/D3dxSkinManager.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

---

## Environment Variables

### Development

```bash
# React port (if changing from 3000)
PORT=3001

# Node environment
NODE_ENV=development
```

### Production

```bash
# .NET environment
DOTNET_ENVIRONMENT=Production

# Build configuration
CONFIGURATION=Release
```

---

## Shortcuts & Aliases

### PowerShell

```powershell
# Navigate to backend
function cdb { cd D:\Development\d3dxSkinManage-master\d3dxSkinManage-Rewrite\D3dxSkinManager }

# Navigate to frontend
function cdf { cd D:\Development\d3dxSkinManage-master\d3dxSkinManage-Rewrite\D3dxSkinManager.Client }

# Build both
function build-all {
    cd D:\Development\d3dxSkinManage-master\d3dxSkinManage-Rewrite\D3dxSkinManager
    dotnet build
    cd ..\D3dxSkinManager.Client
    npm run build
}
```

---

## Error Codes

### Common Build Errors

| Error | Meaning | Solution |
|-------|---------|----------|
| CS0246 | Type or namespace not found | Check using statements |
| CS1061 | Method not found | Check method name/parameters |
| NU1900 | NuGet vulnerability warning | Can ignore or update package |
| TS2339 | Property does not exist | Check TypeScript interface |
| TS2345 | Argument type mismatch | Check parameter types |

### HTTP Status Codes (Future API)

| Code | Meaning |
|------|---------|
| 200 | OK |
| 400 | Bad Request |
| 404 | Not Found |
| 500 | Internal Server Error |

---

## Database Schema Quick Reference

### Mods Table

```sql
CREATE TABLE Mods (
    SHA TEXT PRIMARY KEY,
    ObjectName TEXT NOT NULL,
    Name TEXT NOT NULL,
    Author TEXT,
    Description TEXT,
    Type TEXT DEFAULT '7z',
    Grading TEXT DEFAULT 'G',
    Tags TEXT,
    IsLoaded INTEGER DEFAULT 0,
    IsAvailable INTEGER DEFAULT 0,
    ThumbnailPath TEXT,
    PreviewPath TEXT,
    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_object_name ON Mods(ObjectName);
CREATE INDEX idx_is_loaded ON Mods(IsLoaded);
```

---

## Regular Expressions

### Common Patterns

```regex
# SHA256 hash
^[a-fA-F0-9]{64}$

# Email
^[^\s@]+@[^\s@]+\.[^\s@]+$

# File extension
\.(7z|zip|rar)$

# Semantic version
^\d+\.\d+\.\d+$
```

---

## Keyboard Shortcuts (Development)

### Visual Studio / VS Code

| Shortcut | Action |
|----------|--------|
| F5 | Start debugging |
| Ctrl+Shift+B | Build |
| Ctrl+. | Quick actions |
| F12 | Go to definition |
| Ctrl+/ | Toggle comment |
| Ctrl+K, Ctrl+D | Format document |

### Browser (React DevTools)

| Shortcut | Action |
|----------|--------|
| F12 | Open DevTools |
| Ctrl+Shift+C | Inspect element |
| Ctrl+Shift+J | Console |
| Ctrl+R | Reload |
| Ctrl+Shift+R | Hard reload |

---

## One-Liners

### Find Files

```bash
# Find all TypeScript files
find . -name "*.ts" -o -name "*.tsx"

# Find all C# files
find . -name "*.cs"

# Count lines of code
find . -name "*.cs" -o -name "*.ts" -o -name "*.tsx" | xargs wc -l
```

### Search Code

```bash
# Search for text in all files
grep -r "ModService" .

# Search in specific file type
grep -r "interface" --include="*.cs" .

# Case-insensitive search
grep -ri "photino" .
```

---

## Cheat Sheet Summary

### Most Used Commands

```bash
# Backend
cd D3dxSkinManager && dotnet build && dotnet run

# Frontend
cd D3dxSkinManager.Client && npm start

# Git
git branch
git checkout -b feature/name
git add -A
git commit -m "message"  # AFTER USER APPROVAL!

# Database
sqlite3 mods.db ".schema Mods"

# Documentation
code docs/KEYWORDS_INDEX.md  # For quick lookup
```

---

*This reference is maintained by AI assistants. Add commands as you discover them!*

*Last updated: 2026-02-17*
