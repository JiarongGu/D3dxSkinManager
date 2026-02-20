# Design Decisions

**Purpose:** Document critical architectural and design decisions for the d3dx-skin-manager project.

**Last Updated:** 2026-02-20

---

## Table of Contents

1. [Server-Side Processing Pattern](#server-side-processing-pattern)
2. [IPC Architecture](#ipc-architecture)
3. [State Management Strategy](#state-management-strategy)
4. [Component Architecture](#component-architecture)
5. [Refactoring Strategy](#refactoring-strategy)
6. [Modal Dialog Patterns](#modal-dialog-patterns)

---

## Server-Side Processing Pattern

### Decision

**All computation-heavy operations MUST be processed on the C# backend with real-time status updates sent to the React frontend.**

### Rationale

**Performance:**
- C# is significantly faster than JavaScript for CPU-intensive tasks
- Native file I/O operations are more efficient
- Better memory management for large file operations

**Resource Management:**
- Server has direct access to file system with proper permissions
- Better control over concurrent operations
- Can leverage .NET's ThreadPool for parallel processing

**User Experience:**
- Frontend remains responsive during long operations
- Real-time progress updates provide feedback
- Users can cancel operations without freezing UI

**Error Handling:**
- Server can safely handle file system errors
- Better logging and debugging capabilities
- Graceful degradation on failure

**Maintainability:**
- Business logic stays in one place (C#)
- Easier to test and debug
- Frontend focuses on UI/UX only

### Implementation Pattern

#### Backend (C#)

```csharp
// Example: Mod Import with Progress Updates
public class ModImportService : IModImportService
{
    private readonly IProgress<ImportProgress> _progress;

    public async Task<ImportResult> ImportModsAsync(
        string[] filePaths,
        IProgress<ImportProgress> progress,
        CancellationToken cancellationToken)
    {
        var results = new List<ImportedMod>();

        for (int i = 0; i < filePaths.Length; i++)
        {
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = filePaths[i];
            var fileName = Path.GetFileName(filePath);

            // Report progress BEFORE processing
            progress?.Report(new ImportProgress
            {
                CurrentFile = i + 1,
                TotalFiles = filePaths.Length,
                CurrentFileName = fileName,
                Status = "Processing...",
                PercentComplete = (int)((double)i / filePaths.Length * 100)
            });

            // Perform heavy computation
            var result = await ProcessModFileAsync(filePath, cancellationToken);

            results.Add(result);

            // Report completion
            progress?.Report(new ImportProgress
            {
                CurrentFile = i + 1,
                TotalFiles = filePaths.Length,
                CurrentFileName = fileName,
                Status = "Completed",
                PercentComplete = (int)((double)(i + 1) / filePaths.Length * 100)
            });
        }

        return new ImportResult { ImportedMods = results };
    }

    private async Task<ImportedMod> ProcessModFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        // 1. Extract archive (heavy I/O)
        var extractedPath = await ExtractArchiveAsync(filePath, cancellationToken);

        // 2. Calculate SHA256 hash (CPU-intensive)
        var sha = await CalculateSHA256Async(filePath, cancellationToken);

        // 3. Parse metadata (file I/O)
        var metadata = await ParseMetadataAsync(extractedPath, cancellationToken);

        // 4. Generate thumbnail (image processing)
        var thumbnail = await GenerateThumbnailAsync(extractedPath, cancellationToken);

        return new ImportedMod
        {
            SHA = sha,
            Name = metadata.Name,
            ThumbnailPath = thumbnail
        };
    }
}
```

#### IPC Message Handler

```csharp
// Program.cs - IPC Handler
case "IMPORT_MODS_START":
{
    var filePaths = data["filePaths"].ToObject<string[]>();

    // Create progress reporter
    var progress = new Progress<ImportProgress>(update =>
    {
        // Send progress update to frontend
        window.SendWebMessage(JsonConvert.SerializeObject(new
        {
            type = "IMPORT_PROGRESS",
            payload = update
        }));
    });

    // Create cancellation token (allow user to cancel)
    var cts = new CancellationTokenSource();
    _activeOperations["import"] = cts;

    // Start async operation
    _ = Task.Run(async () =>
    {
        try
        {
            var result = await importService.ImportModsAsync(
                filePaths,
                progress,
                cts.Token);

            // Send completion message
            window.SendWebMessage(JsonConvert.SerializeObject(new
            {
                type = "IMPORT_COMPLETE",
                payload = result
            }));
        }
        catch (OperationCanceledException)
        {
            window.SendWebMessage(JsonConvert.SerializeObject(new
            {
                type = "IMPORT_CANCELLED",
                payload = new { message = "Import cancelled by user" }
            }));
        }
        catch (Exception ex)
        {
            window.SendWebMessage(JsonConvert.SerializeObject(new
            {
                type = "IMPORT_ERROR",
                payload = new { error = ex.Message }
            }));
        }
        finally
        {
            _activeOperations.Remove("import");
        }
    });

    break;
}

case "IMPORT_CANCEL":
{
    if (_activeOperations.TryGetValue("import", out var cts))
    {
        cts.Cancel();
    }
    break;
}
```

#### Frontend (React/TypeScript)

```typescript
// Import service with progress tracking
import { photinoService } from './photino';

export interface ImportProgress {
  currentFile: number;
  totalFiles: number;
  currentFileName: string;
  status: string;
  percentComplete: number;
}

export interface ImportResult {
  importedMods: ModInfo[];
  errors: string[];
}

class ImportService {
  private progressCallback: ((progress: ImportProgress) => void) | null = null;

  async importMods(
    filePaths: string[],
    onProgress: (progress: ImportProgress) => void
  ): Promise<ImportResult> {
    this.progressCallback = onProgress;

    // Register progress listener
    window.addEventListener('IMPORT_PROGRESS', this.handleProgress);
    window.addEventListener('IMPORT_COMPLETE', this.handleComplete);
    window.addEventListener('IMPORT_ERROR', this.handleError);
    window.addEventListener('IMPORT_CANCELLED', this.handleCancelled);

    // Send start message to backend
    const response = await photinoService.sendMessage({
      type: 'IMPORT_MODS_START',
      payload: { filePaths }
    });

    return response as ImportResult;
  }

  cancelImport(): void {
    photinoService.sendMessage({
      type: 'IMPORT_CANCEL',
      payload: {}
    });
  }

  private handleProgress = (event: CustomEvent) => {
    const progress = event.detail as ImportProgress;
    this.progressCallback?.(progress);
  };

  private handleComplete = (event: CustomEvent) => {
    // Cleanup listeners
    this.cleanup();
  };

  private handleError = (event: CustomEvent) => {
    console.error('Import error:', event.detail);
    this.cleanup();
  };

  private handleCancelled = (event: CustomEvent) => {
    console.log('Import cancelled');
    this.cleanup();
  };

  private cleanup(): void {
    window.removeEventListener('IMPORT_PROGRESS', this.handleProgress);
    window.removeEventListener('IMPORT_COMPLETE', this.handleComplete);
    window.removeEventListener('IMPORT_ERROR', this.handleError);
    window.removeEventListener('IMPORT_CANCELLED', this.handleCancelled);
    this.progressCallback = null;
  }
}

export const importService = new ImportService();
```

#### React Component Usage

```typescript
// Component with progress tracking
const ImportWindow: React.FC = () => {
  const [progress, setProgress] = useState<ImportProgress | null>(null);
  const [importing, setImporting] = useState(false);

  const handleImport = async (files: string[]) => {
    setImporting(true);

    try {
      const result = await importService.importMods(files, (progress) => {
        // Update progress in real-time
        setProgress(progress);
      });

      message.success(`Imported ${result.importedMods.length} mods`);
    } catch (error) {
      message.error('Import failed');
    } finally {
      setImporting(false);
      setProgress(null);
    }
  };

  const handleCancel = () => {
    importService.cancelImport();
  };

  return (
    <Modal visible={importing} closable={false}>
      <div>
        <Progress
          percent={progress?.percentComplete || 0}
          status="active"
        />
        <div>
          {progress?.status}: {progress?.currentFileName}
        </div>
        <div>
          File {progress?.currentFile} of {progress?.totalFiles}
        </div>
        <Button onClick={handleCancel}>Cancel</Button>
      </div>
    </Modal>
  );
};
```

### Operations Requiring Server-Side Processing

**Category: File Operations**
- ✅ Archive extraction (.zip, .7z, .rar)
- ✅ Archive creation/compression
- ✅ File copying (large files)
- ✅ File deletion (with validation)
- ✅ Directory operations (create, move, delete)

**Category: Computation**
- ✅ SHA-256 hash calculation (CPU-intensive)
- ✅ Similarity matching algorithms
- ✅ Text search across large datasets
- ✅ Batch data processing

**Category: Image Processing**
- ✅ Thumbnail generation
- ✅ Image resizing
- ✅ Image format conversion
- ✅ Preview image optimization

**Category: Data Operations**
- ✅ Database queries (large result sets)
- ✅ Batch database updates
- ✅ Data import/export
- ✅ Index rebuilding

**Category: External Process**
- ✅ Game launching
- ✅ 3DMigoto launcher
- ✅ Custom program execution
- ✅ File explorer opening

### Anti-Patterns to Avoid

❌ **Don't do heavy computation in JavaScript:**
```typescript
// BAD: Calculating SHA-256 in frontend
const calculateSHA256 = async (file: File): Promise<string> => {
  const buffer = await file.arrayBuffer();
  const hashBuffer = await crypto.subtle.digest('SHA-256', buffer);
  // This blocks the UI thread!
  return Array.from(new Uint8Array(hashBuffer))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');
};
```

❌ **Don't do file I/O in frontend:**
```typescript
// BAD: Can't access file system directly in browser
const extractZip = async (filePath: string) => {
  // This doesn't work in browser context!
  const fs = require('fs'); // Not available
};
```

❌ **Don't block UI thread with synchronous operations:**
```typescript
// BAD: Synchronous operation blocks rendering
for (let i = 0; i < 10000; i++) {
  processItem(i); // UI freezes!
}
```

### Benefits Summary

| Aspect | Server-Side | Client-Side |
|--------|-------------|-------------|
| **Performance** | ⚡ Fast (C# native) | 🐌 Slow (JS interpreted) |
| **Resource Access** | ✅ Full file system | ❌ Browser sandbox |
| **Concurrent Operations** | ✅ ThreadPool | ⚠️ Web Workers (complex) |
| **Progress Updates** | ✅ Real-time IPC | ⚠️ Polling required |
| **Error Handling** | ✅ Comprehensive | ⚠️ Limited |
| **Cancellation** | ✅ CancellationToken | ⚠️ AbortController |
| **Memory Management** | ✅ GC optimized | ⚠️ Limited heap |
| **UI Responsiveness** | ✅ Always responsive | ❌ Can freeze |

---

## IPC Architecture

### Decision

Use **JSON-based message passing** between React frontend and C# backend via Photino's `SendWebMessage` and `window.chrome.webview.postMessage`.

### Message Format

```typescript
// Frontend → Backend
interface PhotinoMessage {
  type: string;          // Message type (e.g., "IMPORT_MODS_START")
  payload: any;          // Message data
  requestId?: string;    // Optional request correlation ID
}

// Backend → Frontend
interface PhotinoResponse {
  type: string;          // Response type (e.g., "IMPORT_COMPLETE")
  payload: any;          // Response data
  requestId?: string;    // Correlates with request
  error?: string;        // Error message if failed
}
```

### Best Practices

1. **Always use typed payloads** - Define TypeScript interfaces
2. **Include request IDs** - For request/response correlation
3. **Handle errors gracefully** - Every operation should have error handling
4. **Provide feedback** - Use progress updates for long operations
5. **Allow cancellation** - Long operations should be cancellable

---

## State Management Strategy

### Decision

Use **React Context + Custom Hooks** for global state, **local state** for component-specific state.

### Rationale

- No heavy state management library needed (Redux, MobX)
- Custom hooks provide reusable logic
- Context for truly global state (theme, user settings)
- Local state for component-specific UI state

### Example

```typescript
// Global: Annotation level (affects entire app)
const { annotationLevel, setAnnotationLevel } = useAnnotation();

// Local: Dialog visibility (component-specific)
const [dialogVisible, setDialogVisible] = useState(false);
```

---

## Component Architecture

### Decision

Use **composition over inheritance** with focused, single-responsibility components.

### Principles

1. **One component, one job** - ModTable shows mods, doesn't handle importing
2. **Props down, callbacks up** - Data flows down, events flow up
3. **Container/Presentational** - Logic in containers, UI in presentational
4. **Reusable utilities** - Extract common logic into custom hooks

---

## Refactoring Strategy

### Decision

**Implement all features first, then refactor based on usage patterns.**

### Phases

**Phase 1: Feature Implementation** (Current)
- Implement all 15 missing features
- Focus on functionality over optimization
- Document design decisions as we go

**Phase 2: Analysis** (After all features done)
- Identify performance bottlenecks
- Find code duplication
- Analyze component coupling
- Measure bundle size

**Phase 3: Refactoring** (Based on analysis)
- Extract common patterns
- Optimize heavy operations
- Reduce bundle size
- Improve code organization

**Phase 4: Testing & Validation**
- Verify functionality unchanged
- Performance benchmarks
- User acceptance testing

### Rationale

**"Premature optimization is the root of all evil"** - Donald Knuth

- We don't know usage patterns yet
- Requirements may change during implementation
- Refactoring without data leads to wrong abstractions
- Better to have working code than perfect code

### What NOT to Refactor Yet

- ❌ Don't optimize before measuring
- ❌ Don't abstract until pattern is clear
- ❌ Don't refactor during feature implementation
- ❌ Don't optimize for hypothetical scenarios

### What TO Refactor After Features Complete

- ✅ Duplicate code (actual duplication, not similar-looking)
- ✅ Performance bottlenecks (measured, not assumed)
- ✅ Complex components (>300 lines, multiple responsibilities)
- ✅ Type improvements (any types, missing interfaces)

---

## Versioning Strategy

### Decision

Use **Semantic Versioning 2.0.0** with clear phase tracking.

### Format

`MAJOR.MINOR.PATCH-PHASE`

- **MAJOR** - Breaking changes (v2.0.0 = rewrite from Python)
- **MINOR** - New features (v2.1.0 = Phase 15 complete)
- **PATCH** - Bug fixes (v2.0.1 = fix crash)
- **PHASE** - Development phase (optional, e.g., v2.1.0-beta)

### Current Version

**v2.0.0** - React rewrite with 65% feature parity

---

## Documentation Strategy

### Decision

Follow **[AI_GUIDE.md](AI_GUIDE.md)** for all documentation with RAG optimization.

### Principles

1. **Purpose-driven** - Each doc has a clear purpose
2. **Findable** - Keywords index for quick lookup
3. **Maintainable** - Update with code changes
4. **Scannable** - Headers, bullets, code blocks
5. **Actionable** - Clear next steps

### File Structure

```
docs/
├── AI_GUIDE.md           # START HERE for AI
├── KEYWORDS_INDEX.md     # Quick file lookup
├── CHANGELOG.md          # What changed
├── core/
│   ├── DESIGN_DECISIONS.md    # This file
│   ├── ARCHITECTURE.md         # System design
│   └── DEVELOPMENT.md          # Dev workflows
├── features/
│   └── FEATURE_GAP_ANALYSIS.md  # Missing features
└── planning/
    └── MISSING_FEATURES_ROADMAP.md  # Implementation plan
```

---

## Modal Dialog Patterns

### Decision

**All modal dialogs MUST use declarative rendering with disabled transitions for instant display without flashing animations.**

### Rationale

**User Experience:**
- Imperative APIs (`Modal.confirm()`) cause visible flashing/flickering when opening
- Disabled transitions provide instant feedback without animation delays
- Users expect dialogs to appear immediately when triggered

**Implementation Consistency:**
- Declarative modals integrate better with React's component lifecycle
- State management is explicit and predictable
- Easier to test and debug

**Technical Requirements:**
```typescript
// ✅ DO: Declarative with disabled transitions
<Modal
  open={visible}
  transitionName=""        // Disable modal transition
  maskTransitionName=""    // Disable mask transition
  centered                 // Center on viewport
>
```

```typescript
// ❌ DON'T: Imperative API
Modal.confirm({
  // Causes flashing and harder to control
});
```

### Implementation Pattern

**1. ConfirmDialog Component** (`shared/components/dialogs/ConfirmDialog.tsx`)
- Reusable confirmation dialog with proper theming
- Disabled transitions for instant display
- Custom close button matching design system
- Compact spacing with clear visual hierarchy

**2. Close Button Styling**
- Square 32x32px button with rounded corners
- Theme-aware (dark/light mode support)
- Matches FullScreenPreview close button design
- Hover states for visual feedback

**3. Spacing Guidelines**
- Header padding: `16px` top, `8px` horizontal and bottom (provides breathing room from top edge)
- Body padding: `8px` horizontal
- Footer padding: `8px` horizontal, `8px` bottom
- Gap after header: `12px` margin-bottom
- Gap after body: `16px` margin-bottom

**4. Animation Disabling (Critical for UX)**

All animations must be completely disabled for instant feedback. Ant Design buttons have multiple animation sources that must all be disabled:

```css
/* Comprehensive animation disabling - targets all button states */
.confirm-dialog .ant-btn,
.confirm-dialog .ant-btn:hover,
.confirm-dialog .ant-btn:focus,
.confirm-dialog .ant-btn:active,
.confirm-dialog .ant-btn *,
.confirm-dialog .ant-btn::after {
  transition: none !important;
  animation: none !important;
  transform: none !important;
}

/* Disable Ant Design's wave effect */
.confirm-dialog .ant-btn::after,
.confirm-dialog .ant-btn .ant-wave {
  display: none !important;
}

/* Prevent layout shifts */
.confirm-dialog .ant-btn {
  will-change: auto !important;
}
```

**Key Findings:**
- Must disable `transition`, `animation`, AND `transform` properties
- Must target all button pseudo-states (`:hover`, `:focus`, `:active`)
- Must disable on all child elements (`.ant-btn *`) to catch nested icons/spinners
- Must disable Ant Design's wave effect (`.ant-btn::after` and `.ant-wave`)
- Must prevent CSS optimization with `will-change: auto`
- Danger buttons (`CompactDangerButton`) are particularly prone to size change animations

**5. Loading State Management**

**Delayed Loading Indicator Pattern:**
Only show loading spinner if operation takes longer than 50ms to prevent annoying flicker for fast operations:

```typescript
const [loading, setLoading] = React.useState(false);
const loadingTimeoutRef = React.useRef<NodeJS.Timeout | null>(null);
const isProcessingRef = React.useRef(false);

// Reset loading state when dialog visibility changes
React.useEffect(() => {
  if (!visible) {
    setLoading(false);
    isProcessingRef.current = false;
    if (loadingTimeoutRef.current) {
      clearTimeout(loadingTimeoutRef.current);
      loadingTimeoutRef.current = null;
    }
  }
}, [visible]);

const handleOk = async () => {
  // Prevent multiple clicks while processing (without disabling button)
  if (isProcessingRef.current) {
    return;
  }
  isProcessingRef.current = true;

  // Only show loading spinner if operation takes longer than 50ms
  loadingTimeoutRef.current = setTimeout(() => {
    setLoading(true);
  }, 50);

  try {
    await onOk();
  } finally {
    if (loadingTimeoutRef.current) {
      clearTimeout(loadingTimeoutRef.current);
      loadingTimeoutRef.current = null;
    }
    setLoading(false);
    isProcessingRef.current = false;
  }
};
```

**Key Benefits:**
- **Fast operations (< 50ms)**: No loading spinner - dialog closes instantly without flicker
- **Slow operations (> 50ms)**: Loading spinner appears providing visual feedback
- **Click prevention**: `isProcessingRef` prevents multiple submissions without disabling button (avoids style changes)
- **Proper cleanup**: Resets all state when dialog closes

**Why 50ms?**
- Operations completing under 50ms feel instant to users
- Loading spinner for such short operations is more jarring than helpful
- Common pattern used by modern applications (GitHub, VSCode, etc.)

**6. Danger Button Styling**

Danger buttons (used for destructive actions like "Delete") use a progressive darkening approach for hover states:

```css
/* Danger button - deeper/darker on hover with subtle background tint */
[data-theme="dark"] .compact-button.ant-btn.ant-btn-dangerous:hover {
  border-color: #d9363e !important;
  color: #d9363e !important;
  background: rgba(217, 54, 62, 0.08) !important;
}

[data-theme="dark"] .compact-button.ant-btn.ant-btn-dangerous:active {
  border-color: #cf1322 !important;
  color: #cf1322 !important;
  background: rgba(207, 19, 34, 0.12) !important;
}

[data-theme="light"] .compact-button.ant-btn.ant-btn-dangerous:hover {
  border-color: #d9363e !important;
  color: #d9363e !important;
  background: rgba(217, 54, 62, 0.06) !important;
}

[data-theme="light"] .compact-button.ant-btn.ant-btn-dangerous:active {
  border-color: #a8071a !important;
  color: #a8071a !important;
  background: rgba(168, 7, 26, 0.1) !important;
}
```

**Design Philosophy:**
- **Progressive darkening:** Default red → Darker red (hover) → Darkest red (active)
- **Subtle background tint:** Very low opacity red background adds depth without overwhelming
- **"Pressed in" feeling:** Darker colors create impression of button being pressed
- **Minimal changes:** Only border, text, and subtle background - keeps design clean
- **Consistent opacity:** Dark theme uses slightly higher opacity (8%/12%) vs light theme (6%/10%) for better visibility

**7. Theme Integration**
```css
/* Dark theme close button */
[data-theme="dark"] .confirm-dialog-close-button {
  width: 32px;
  height: 32px;
  background: var(--color-bg-elevated);
  border: 1px solid var(--color-border-base);
  color: var(--color-text-secondary);
  transition: none;
}

/* Light theme close button */
[data-theme="light"] .confirm-dialog-close-button {
  width: 32px;
  height: 32px;
  background: rgba(240, 240, 245, 0.95);
  border: 1px solid rgba(0, 0, 0, 0.15);
  color: rgba(0, 0, 0, 0.8);
  transition: none;
}
```

### Examples

**Delete Confirmation (ModPreviewPanel)**
```typescript
const [deleteConfirmVisible, setDeleteConfirmVisible] = useState(false);

<ConfirmDialog
  visible={deleteConfirmVisible}
  title="Delete Preview Image"
  content="Are you sure you want to delete this preview image? This action cannot be undone."
  okText="Delete"
  okType="danger"
  onOk={handleDeleteConfirm}
  onCancel={() => setDeleteConfirmVisible(false)}
/>
```

### Benefits

**Instant Feedback:**
- No animation delay when opening dialogs
- Feels more responsive to user actions
- Reduces perceived latency

**Consistent Styling:**
- Matches application design system
- Theme-aware close buttons
- Compact spacing reduces visual noise

**Maintainability:**
- Single reusable component for all confirmations
- Easy to update styling globally
- Clear separation of concerns

### Related Components

- `FullScreenPreview` - Uses same close button pattern
- `ContextMenu` - Uses declarative rendering
- `CompactButton` / `CompactDangerButton` - Used in dialog footer

### Files

- `D3dxSkinManager.Client/src/shared/components/dialogs/ConfirmDialog.tsx`
- `D3dxSkinManager.Client/src/shared/components/dialogs/ConfirmDialog.css`
- `D3dxSkinManager.Client/src/modules/mods/components/ModPreviewPanel/FullScreenPreview.tsx`

---

## References

- **AI Guide:** [docs/AI_GUIDE.md](../AI_GUIDE.md)
- **Architecture:** [docs/core/ARCHITECTURE.md](ARCHITECTURE.md)
- **Feature Gap:** [docs/features/FEATURE_GAP_ANALYSIS.md](../features/FEATURE_GAP_ANALYSIS.md)
- **Roadmap:** [docs/planning/MISSING_FEATURES_ROADMAP.md](../planning/MISSING_FEATURES_ROADMAP.md)

---

*Last updated: 2026-02-20 | This document follows [AI_GUIDE.md](../AI_GUIDE.md) requirements*
