# Workflow Module

Simple, stateless workflow system for managing multi-step processes.

## Architecture

### Backend
- **WorkflowEntity/WorkflowInfo**: Simple model (Id, Type, Status, Context JSON)
- **WorkflowRepository**: In-memory CRUD operations
- **Handlers**: Type-specific handlers (e.g., `ModImportWorkflowHandler`)
- **WorkflowFacade**: IPC interface

### Frontend
- **Types**: `WorkflowInfo`, `WorkflowStatus`, workflow-specific context types
- **Service**: `workflowService` for IPC calls
- **Hooks**: `useModImportWorkflow` for business logic
- **Components**: Type-specific UI components

## ModImportWorkflow

Simple 3-step workflow for importing mods from folders:

1. **compress_folder** - Auto: Compress folder to temp archive
2. **waiting_for_metadata** - **User Input**: Fill metadata form
3. **import_mod** - Auto: Import with metadata

### Usage

```tsx
import { FolderImportButton } from '@/modules/mod/components/ModManagementScreen/FolderImportButton';

// In your component
<FolderImportButton />
```

The button handles:
- Opening folder dialog
- Triggering workflow
- Showing workflow screen
- Handling success/errors

### Workflow Screen

```tsx
import { ModImportWorkflowScreen } from '@/modules/workflow/components';

<ModImportWorkflowScreen
  visible={showWorkflow}
  folderPath="/path/to/folder"
  onClose={() => setShowWorkflow(false)}
  onSuccess={(modId) => {
    logger.info('Imported:', modId);
  }}
/>
```

### Custom Hook

```tsx
import { useModImportWorkflow } from '@/modules/workflow/hooks';

const { workflow, loading, startImport, provideMetadata, cancelImport } = useModImportWorkflow();

// Start workflow
await startImport('/path/to/folder');

// Provide metadata when status is WaitingForInput
await provideMetadata({
  name: 'My Mod',
  author: 'Me',
  category: 'Weapons',
  tags: ['sword'],
  grading: 'G',
});

// Cancel anytime
await cancelImport();
```

## Events

The workflow emits real-time events:

```typescript
eventBus.subscribe(Module.WORKFLOW, WorkflowEventType.STATUS_CHANGED, (event) => {
  logger.info('Workflow status changed:', event.payload);
});
```

Event types:
- `CREATED` - Workflow created
- `STATUS_CHANGED` - Status updated
- `COMPLETED` - Workflow completed successfully
- `FAILED` - Workflow failed
- `CANCELLED` - Workflow cancelled

## Adding New Workflows

1. **Backend**: Create handler in `Modules/Workflow/Handlers/`
2. **Define context type**: Create context model in `Models/`
3. **Register in DI**: Add to `WorkflowServiceExtensions.cs`
4. **Add IPC handlers**: Update `WorkflowFacade.cs`
5. **Frontend**: Create types, hook, and UI component
6. **Add events**: Update event types if needed

## Benefits

✅ **Simple**: No complex routing, nodes, or conditions
✅ **Stateless**: UI just reads workflow state
✅ **Isolated**: Each workflow type manages its own logic
✅ **Event-driven**: Real-time updates via event bus
✅ **Type-safe**: Full TypeScript/C# typing
✅ **Easy to test**: Clear responsibilities
✅ **Easy to extend**: Add new workflow types easily
