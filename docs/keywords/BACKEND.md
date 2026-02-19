# Backend Keywords Index

> **Purpose:** Backend C# classes, services, and architecture
> **Parent Index:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)

**Last Updated:** 2026-02-20

---

## Entry Point

- **Program** → `D3dxSkinManager/Program.cs`
  - Main method → `:11`
  - InitializeServices (DI setup) → `:24-38`
  - Photino window setup → `:42-55`
  - IPC message handler → `:65-120`

---

## Modules

### Core Module

#### Services

- **FileService** → `Modules/Core/Services/FileService.cs:24`
  - CalculateSha256Async → `:26-48`
  - ExtractArchiveAsync → `:51-90`
  - CopyDirectoryAsync → `:93-120`
  - DeleteDirectoryAsync → `:123-137`
  - Is7ZipAvailable → `:140-142`
  - Get7ZipPath → `:145-165`

- **ImageService** → `Modules/Core/Services/ImageService.cs:26`
  - GetThumbnailPathAsync → `:72-85`
  - GetPreviewPathsAsync → `:87-107` (scans previews/{SHA}/ folder)
  - GenerateThumbnailAsync → `:110-167`
  - GeneratePreviewsAsync → `:169-246` (creates per-mod preview folders)
  - CacheImageAsync → `:249-277`
  - ResizeImageAsync → `:280-314`
  - ClearModCacheAsync → `:316-357`
  - GetSupportedImageExtensions → `:360-366`
  - GetImageAsDataUriAsync → `:368-394`
  - GetThumbnailAsDataUriAsync → `:396-400`
  - GetPreviewsAsDataUriAsync → `:402-414`

- **GlobalPathService** → `Modules/Core/Services/GlobalPathService.cs`
  - Global path resolution for application directories

- **LogHelper** → `Modules/Core/Services/LogHelper.cs`
  - Centralized logging infrastructure

- **FileSystemService** → `Modules/Core/Services/FileSystemService.cs`
  - File system abstraction layer

- **ProcessService** → `Modules/Core/Services/ProcessService.cs`
  - Process management and execution

- **ImageService** → `Modules/Core/Services/ImageService.cs`
  - Image processing (thumbnails, resizing, caching)
  - Note: Image serving now handled by CustomSchemeHandler

- **CustomSchemeHandler** → `Modules/Core/Services/CustomSchemeHandler.cs`
  - Handles custom `app://` scheme requests for serving local files
  - URL format: `app://encoded_file_path`
  - Includes security checks and content type detection
  - Registered as singleton via DI in CoreServiceExtensions
  - Interface: ICustomSchemeHandler
  - Created: 2026-02-20

#### Utilities

- **FileUtilities** → `Modules/Core/Utilities/FileUtilities.cs`
- **JsonHelper** → `Modules/Core/Utilities/JsonHelper.cs`
- **ValidationHelper** → `Modules/Core/Utilities/ValidationHelper.cs`

#### Facades

- **BaseFacade** → `Modules/Core/Facades/BaseFacade.cs`
  - Base class for all module facades

---

### Mods Module

#### Facade

- **ModFacade** → `Modules/Mods/ModFacade.cs:14`
  - Constructor (DI) → `:21-34`
  - GetAllModsAsync → `:37`
  - LoadModAsync → `:40-48`
  - UnloadModAsync → `:51-59`
  - ImportModAsync → `:62`
  - SearchModsAsync → `:65`
  - GetLoadedModsAsync → `:68`
  - DeleteModAsync → `:71-79`

#### Services

- **ModManagementService** → `Modules/Mods/Services/ModManagementService.cs`
  - CRUD operations for mods
  - Mod lifecycle management

- **ModFileService** → `Modules/Mods/Services/ModFileService.cs`
  - GetArchivePath → Returns path WITHOUT extension (matches Python format)
  - CopyArchiveAsync → Stores archives without extensions
  - LoadModAsync → Extract and load mod
  - UnloadModAsync → Remove extracted mod
  - ClearCacheAsync → Clear mod cache

- **ModImportService** → `Modules/Mods/Services/ModImportService.cs:14`
  - Constructor → `:26-40`
  - ImportAsync → `:43-120` (complete import workflow)
  - ReadMetadataAsync → `:123-145`
  - GenerateNameFromDirectory → `:148-160`

- **ModRepository** → `Modules/Mods/Services/ModRepository.cs:32`
  - Constructor → `:37-42`
  - InitializeDatabaseAsync → `:45-80`
  - GetAllAsync → `:83-105`
  - GetByIdAsync → `:108-131`
  - ExistsAsync → `:134-149`
  - InsertAsync → `:152-179`
  - UpdateAsync → `:182-207`
  - DeleteAsync → `:210-224`
  - GetByObjectNameAsync → `:227-249`
  - GetLoadedIdsAsync → `:252-269`
  - GetDistinctObjectNamesAsync → `:272-289`
  - GetDistinctAuthorsAsync → `:292-309`
  - GetAllTagsAsync → `:312-329`
  - SetLoadedStateAsync → `:332-347`

- **ClassificationService** → `Modules/Mods/Services/ClassificationService.cs:22`
  - ClassifyModAsync → `:29-65`
  - LoadRulesAsync → `:68-90`
  - GetRules → `:93`
  - AddRule → `:96`
  - SaveRulesAsync → `:99-110`

- **ClassificationRepository** → `Modules/Mods/Services/ClassificationRepository.cs`
  - Database access for classifications

#### Models

- **ModInfo** → `Modules/Mods/Models/ModInfo.cs:5`
  - Properties: SHA, ObjectName, Name, Author, Description, Type, Grading, Tags, IsLoaded, IsAvailable, ThumbnailPath, OriginalPath, WorkPath, CachePath, Category

---

### Migration Module

> **Key Update (2026-02-20):** Archives now stored WITHOUT extensions (matches Python format)
> **Architecture:** Step-based migration system with 6 steps
> **Documentation:** [architecture/MIGRATION_ARCHITECTURE.md](../architecture/MIGRATION_ARCHITECTURE.md)

#### Facade

- **MigrationFacade** → `Modules/Migration/MigrationFacade.cs`
  - IPC entry point for migration operations

#### Orchestrator

- **MigrationService** → `Modules/Migration/Services/MigrationService.cs:44`
  - Thin orchestrator (205 lines, down from 991)
  - AnalyzeSourceAsync → `:73`
  - MigrateAsync → `:97` (executes all steps in order)
  - ValidateMigrationAsync → `:184`

#### Migration Steps

- **IMigrationStep** → `Modules/Migration/Steps/IMigrationStep.cs:5`
- **MigrationStep1AnalyzeSource** → `Modules/Migration/Steps/MigrationStep1AnalyzeSource.cs:18`
- **MigrationStep2MigrateConfiguration** → `Modules/Migration/Steps/MigrationStep2MigrateConfiguration.cs`
- **MigrationStep3MigrateClassifications** → `Modules/Migration/Steps/MigrationStep3MigrateClassifications.cs`
- **MigrationStep4MigrateClassificationThumbnails** → `Modules/Migration/Steps/MigrationStep4MigrateClassificationThumbnails.cs`
- **MigrationStep5MigrateModArchives** → `Modules/Migration/Steps/MigrationStep5MigrateModArchives.cs:23`
  - Copies archives WITHOUT extensions → `:98`
  - DetectArchiveTypeAsync (SharpCompress) → `:166`
  - Creates mod entries using ModManagementService → `:118`
- **MigrationStep6MigrateModPreviews** → `Modules/Migration/Steps/MigrationStep6MigrateModPreviews.cs`

#### Python Parsers

- **IPythonConfigurationParser** → `Modules/Migration/Parsers/PythonConfigurationParser.cs`
- **IPythonClassificationFileParser** → `Modules/Migration/Parsers/PythonClassificationFileParser.cs`
- **IPythonRedirectionFileParser** → `Modules/Migration/Parsers/PythonRedirectionFileParser.cs`
- **IPythonModIndexParser** → `Modules/Migration/Parsers/PythonModIndexParser.cs`

#### Models

- **MigrationContext** → `Modules/Migration/Models/MigrationContext.cs`
- **MigrationOptions** → `Modules/Migration/Models/MigrationOptions.cs`
- **MigrationResult** → `Modules/Migration/Models/MigrationResult.cs`
- **MigrationProgress** → `Modules/Migration/Models/MigrationProgress.cs`
- **MigrationAnalysis** → `Modules/Migration/Models/MigrationAnalysis.cs`
- **PythonConfiguration** → `Modules/Migration/Models/PythonConfiguration.cs`

---

### Settings Module

#### Facade

- **SettingsFacade** → `Modules/Settings/SettingsFacade.cs`
  - HandleMessageAsync → `:37-79` (routes UPDATE_FIELD, GET_GLOBAL, etc.)
  - UpdateGlobalSettingHandlerAsync → `:247-255`

#### Services

- **GlobalSettingsService** → `Modules/Settings/Services/GlobalSettingsService.cs`
  - **File Location:** `data/settings/global.json`
  - **FIXED 2026-02-18:** Deadlock in UpdateSettingAsync resolved
  - GetSettingsAsync → `:41-81`
  - UpdateSettingsAsync → `:85-98`
  - UpdateSettingAsync → `:104-158` (fixed deadlock - no nested lock)
  - ResetSettingsAsync → `:163-174`

- **SettingsFileService** → `Modules/Settings/Services/SettingsFileService.cs`
  - Generic file-based storage service

---

### Profile Module

#### Facade

- **ProfileFacade** → `Modules/Profiles/ProfileFacade.cs`
  - IPC entry point for profile operations

#### Services

- **ProfileService** → `Modules/Profiles/Services/ProfileService.cs`
  - Profile CRUD operations
  - Profile switching and management

- **ProfilePathService** → `Modules/Profiles/Services/ProfilePathService.cs`
  - Profile-specific path resolution
  - Per-profile data directory management

- **ProfileServiceProvider** → `Modules/Profiles/Services/ProfileServiceProvider.cs`
  - Service provider for profile-scoped services

- **ProfileServerService** → `Modules/Profiles/ProfileServerService.cs`
  - Profile server coordination

#### Models

- **Profile** → `Modules/Profiles/Models/Profile.cs`
  - Profile data model

---

### Plugins Module

> **Architecture:** External plugin system with dynamic loading
> **Plugin Location:** `Plugins/` directory (27 external projects)
> **Infrastructure:** `Modules/Plugins/Services/`

#### Facade

- **PluginsFacade** → `Modules/Plugins/PluginsFacade.cs`
  - Handles plugin-related IPC messages

#### Services

- **PluginLoader** → `Modules/Plugins/Services/PluginLoader.cs`
  - Loads plugins from plugins directory
  - Constructor requires: pluginsPath, registry, services, logger

- **PluginRegistry** → `Modules/Plugins/Services/PluginRegistry.cs`
  - Registry of loaded plugins

- **PluginEventBus** → `Modules/Plugins/Services/PluginEventBus.cs`
  - Event bus for plugin communication
  - EmitAsync (virtual for mocking) → `:45`

- **PluginContext** → `Modules/Plugins/Services/PluginContext.cs`
  - Context for plugin execution

#### Interfaces

- **IPlugin** → `Modules/Plugins/Services/IPlugin.cs`
  - Base plugin interface
  - Properties: Id, Name, Version, Author, Description

- **IServicePlugin** → `Modules/Plugins/Services/IServicePlugin.cs`
  - Interface for plugins that provide services

- **IMessageHandlerPlugin** → `Modules/Plugins/Services/IMessageHandlerPlugin.cs`
  - Interface for plugins that handle IPC messages

- **IPluginContext** → `Modules/Plugins/Services/IPluginContext.cs`

#### Models

- **PluginInfo** → `Modules/Plugins/Models/PluginInfo.cs`
  - DTO for plugin information (IPC)

#### External Plugins

Located in `Plugins/` directory (external to backend):
- ScreenCapture, BatchProcessingTools, CacheClearup, etc. (27 projects)
- **Namespace:** All use `D3dxSkinManager.Modules.Plugins.Services` for infrastructure
- **Target Framework:** net8.0-windows

---

### Launch Module

#### Facade

- **LaunchFacade** → `Modules/Launch/LaunchFacade.cs`
  - IPC entry point for game launch operations

#### Services

- **D3DMigotoService** → `Modules/Launch/Services/D3DMigotoService.cs`
  - Game launch with 3DMigoto integration
  - Unity game launch configuration

---

### Tools Module

#### Facade

- **ToolsFacade** → `Modules/Tools/ToolsFacade.cs`
  - IPC entry point for utility operations

#### Services

- **ConfigurationService** → `Modules/Tools/Services/ConfigurationService.cs`
  - Configuration management utilities

- **ModAutoDetectionService** → `Modules/Tools/Services/ModAutoDetectionService.cs`
  - Automatic mod detection

- **StartupValidationService** → `Modules/Tools/Services/StartupValidationService.cs`
  - Application startup validation

---

## Shared Models

- **MessageRequest** → `Models/MessageRequest.cs:3`
  - Properties: Id, Type, Payload

- **MessageResponse** → `Models/MessageResponse.cs:3`
  - Properties: Id, Success, Data, Error

---

## Database

- **SQLite Connection** → `Modules/Mods/Services/ModRepository.cs:37`
- **Mods Table Schema** → `Modules/Mods/Services/ModRepository.cs:49-78`

---

## Configuration & DI

- **ServiceRouter** → `ServiceRouter.cs`
  - Routes IPC messages to appropriate facades

- **CoreServiceExtensions** → `Modules/Core/CoreServiceExtensions.cs`
  - DI registration for Core module

- **ModsServiceExtensions** → Similar pattern for each module
  - Each module has its own ServiceExtensions class

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Photino.NET | 4.0.16 | Desktop window framework |
| Microsoft.Data.Sqlite | 10.0.3 | SQLite database |
| Newtonsoft.Json | 13.0.4 | JSON serialization |
| Microsoft.Extensions.DependencyInjection | 10.0.3 | DI container |
| System.Drawing.Common | 10.0.3 | Image processing |
| SharpCompress | Latest | Archive format detection |
| xUnit | Latest | Unit testing |
| Moq | 4.20.73 | Mocking |
| FluentAssertions | 7.0.1 | Test assertions |

---

## Naming Conventions

- **PascalCase** for files: `ModFacade.cs`, `ModRepository.cs`, `IModFacade.cs`
- **Folders:** `Modules/`, `Services/`, `Models/`, `Facades/`

---

**Line Count:** ~350 lines
**Parent:** [KEYWORDS_INDEX.md](../KEYWORDS_INDEX.md)
