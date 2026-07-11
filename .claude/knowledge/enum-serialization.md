# Enum Serialization Rule (CRITICAL)

**IpcHandler uses `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`.**

This means ALL C# enums serialize to **camelCase strings** when sent to the frontend.

## Impact

| C# Enum Value | JSON Output | Frontend Type |
|---|---|---|
| `AnalysisStatus.Running` | `"running"` | `'running'` |
| `HealthIssueSeverity.Error` | `"error"` | `'error'` |
| `DuplicateType.TextureVariant` | `"textureVariant"` | `'textureVariant'` |
| `OrphanCategory.TempFile` | `"tempFile"` | `'tempFile'` |

## Rule

When creating TypeScript types for C# enums:
1. **Always use camelCase** — `'running'` not `'Running'`
2. **Add the NOTE comment** at the top of the type:
   ```typescript
   // NOTE: Enums are camelCase because IpcHandler serializes with JsonStringEnumConverter(CamelCase)
   ```
3. **All comparisons must use camelCase** — `payload.status === 'running'` not `'Running'`

## Where to verify

The serializer config is in `Modules/Core/WebView/IpcHandler.cs`:
```csharp
_jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};
```

## Past incidents

- **2026-04-13**: Analysis progress never worked — `'Running'` vs `'running'` mismatch caused all progress events to be silently ignored.
- **2026-04-13**: File cleanup tool showed 0 items in all tabs — `'TempFile'` vs `'tempFile'` mismatch caused category matching to fail.
