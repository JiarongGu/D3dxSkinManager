---
name: error-with-i18n
description: Use whenever adding a new error, throw, or exception. Generates OperationException code + adds translations to both en.json and cn.json. Never add errors manually.
---

# Error with i18n Generator

Generate consistent error handling with OperationException and i18n translations.

## Arguments

**Format**: `/error-with-i18n <ErrorCode> <Parameters> <EnglishMessage> <ChineseMessage>`

**Example**:
```
/error-with-i18n TEXTURE_INVALID_FORMAT fileName,supportedFormats "Invalid texture format: {{fileName}}. Supported formats: {{supportedFormats}}" "无效的纹理格式：{{fileName}}。支持的格式：{{supportedFormats}}"
```

**Parameters**:
- `ErrorCode` - Error code in UPPER_SNAKE_CASE (e.g., TEXTURE_INVALID_FORMAT)
- `Parameters` - Comma-separated parameter names (e.g., fileName,supportedFormats)
- `EnglishMessage` - English error message with {{param}} placeholders
- `ChineseMessage` - Chinese error message with {{param}} placeholders

## What This Skill Generates

1. **C# OperationException code** - Throw statement with proper structure
2. **English translation** - Adds key to `D3dxSkinManager/Languages/en.json`
3. **Chinese translation** - Adds key to `D3dxSkinManager/Languages/cn.json`
4. **Verification** - Ensures no duplicate error codes

## Error Code Pattern

```csharp
throw new OperationException(
    "{ERROR_CODE}",
    new Dictionary<string, string>
    {
        { "param1", value1 },
        { "param2", value2 }
    },
    "Fallback error message"  // Used if translation missing
);
```

## i18n Translation Pattern

Both `en.json` and `cn.json` use the same key format:

```json
{
  "errors": {
    "{ERROR_CODE}": "Message with {{param1}} and {{param2}}"
  }
}
```

## Steps to Execute

1. **Check for existing error code**:
   - Search `D3dxSkinManager/Languages/en.json` for `errors.{ERROR_CODE}`
   - If exists, warn user and ask if they want to overwrite
   - If doesn't exist, proceed

2. **Generate C# throw statement**:
   ```csharp
   throw new OperationException(
       "{ERROR_CODE}",
       new Dictionary<string, string>
       {
           // Generate one entry per parameter
           { "paramName", variableName }
       },
       "{EnglishMessage without placeholders}"  // Fallback
   );
   ```

3. **Add English translation**:
   - Read `D3dxSkinManager/Languages/en.json`
   - Add to `errors` section (alphabetical order):
     ```json
     "errors.{ERROR_CODE}": "{EnglishMessage}"
     ```
   - Save file

4. **Add Chinese translation**:
   - Read `D3dxSkinManager/Languages/cn.json`
   - Add to `errors` section (same order as English):
     ```json
     "errors.{ERROR_CODE}": "{ChineseMessage}"
     ```
   - Save file

5. **Display usage example**:
   ```csharp
   // Backend usage (throw):
   throw new OperationException(...);

   // Frontend usage (display stored error):
   import { translateErrorMessage } from '@/shared/utils/errorHandler';
   const errorText = translateErrorMessage(workflow.errorMessage);

   // Frontend usage (catch IPC error):
   import { handleError } from '@/shared/utils/errorHandler';
   try {
     await modService.deleteMod(profileId, id);
   } catch (error: unknown) {
     handleError(error);  // Automatically shows translated notification
   }
   ```

## Parameter Placeholder Format

Messages use Mustache-style placeholders:

- `{{paramName}}` - Replaced with actual value at runtime
- Example: `"Failed to load mod: {{name}}"` → `"Failed to load mod: MyMod"`

## Error Code Naming Conventions

Follow these patterns:

- **Operation failures**: `{ENTITY}_{OPERATION}_FAILED`
  - `MOD_DELETE_FAILED`
  - `TEXTURE_IMPORT_FAILED`
  - `PROFILE_SAVE_FAILED`

- **Validation errors**: `{ENTITY}_{FIELD}_INVALID`
  - `TEXTURE_FORMAT_INVALID`
  - `MOD_NAME_INVALID`
  - `CATEGORY_PATH_INVALID`

- **Not found errors**: `{ENTITY}_NOT_FOUND`
  - `MOD_NOT_FOUND`
  - `PROFILE_NOT_FOUND`
  - `WORKFLOW_NOT_FOUND`

- **Duplicate errors**: `{ENTITY}_DUPLICATE`
  - `MOD_DUPLICATE`
  - `CATEGORY_DUPLICATE`

- **Business rule violations**: `{CONTEXT}_{SPECIFIC_RULE}`
  - `WORKFLOW_MI_DUPLICATE_MOD`
  - `MOD_LOAD_CATEGORY_CONFLICT`

## Example Outputs

### Example 1: Texture Validation Error

Input:
```
/error-with-i18n TEXTURE_INVALID_FORMAT fileName,supportedFormats "Invalid texture format: {{fileName}}. Supported formats: {{supportedFormats}}" "无效的纹理格式：{{fileName}}。支持的格式：{{supportedFormats}}"
```

Generates:

**C# Code:**
```csharp
throw new OperationException(
    "TEXTURE_INVALID_FORMAT",
    new Dictionary<string, string>
    {
        { "fileName", fileName },
        { "supportedFormats", supportedFormats }
    },
    $"Invalid texture format: {fileName}. Supported formats: {supportedFormats}"
);
```

**en.json:**
```json
"errors.TEXTURE_INVALID_FORMAT": "Invalid texture format: {{fileName}}. Supported formats: {{supportedFormats}}"
```

**cn.json:**
```json
"errors.TEXTURE_INVALID_FORMAT": "无效的纹理格式：{{fileName}}。支持的格式：{{supportedFormats}}"
```

### Example 2: Simple Not Found Error

Input:
```
/error-with-i18n MOD_NOT_FOUND id "Mod not found: {{id}}" "找不到模组：{{id}}"
```

Generates:

**C# Code:**
```csharp
throw new OperationException(
    "MOD_NOT_FOUND",
    new Dictionary<string, string> { { "id", id } },
    $"Mod not found: {id}"
);
```

**en.json:**
```json
"errors.MOD_NOT_FOUND": "Mod not found: {{id}}"
```

**cn.json:**
```json
"errors.MOD_NOT_FOUND": "找不到模组：{{id}}"
```

## Important Rules

- ✅ ALWAYS add translations to BOTH en.json and cn.json
- ✅ Error codes are UPPER_SNAKE_CASE
- ✅ Translation keys use dot notation: `errors.{CODE}`
- ✅ Parameters use Mustache syntax: `{{paramName}}`
- ✅ Fallback message in C# matches English translation (without placeholders)
- ✅ Keep translations in alphabetical order within errors section
- ✅ Use descriptive error codes following naming conventions
- ❌ Don't create duplicate error codes (check first)
- ❌ Don't forget any language file (both EN and CN required)
- ❌ Don't use hard-coded error messages (always use i18n)

## Verification Checklist

After generation:
- [ ] Error code added to en.json
- [ ] Error code added to cn.json
- [ ] Both translations use same parameter names
- [ ] C# code has all parameters in Dictionary
- [ ] Fallback message is meaningful
- [ ] No duplicate error codes

## Reference Files

- **English**: `D3dxSkinManager/Languages/en.json`
- **Chinese**: `D3dxSkinManager/Languages/cn.json`
- **Backend Usage**: `Modules/*/Services/*.cs` - throw OperationException
- **Frontend Usage**: `shared/utils/errorHandler.ts` - handleError, translateErrorMessage
