/**
 * Type-safe IPC request payload definitions for MOD module
 *
 * This file maps each IPC message type to its expected payload structure
 * based on the backend ModFacade implementation.
 *
 * All payloads use 'id' as the mod identifier (GUID format, 32-char uppercase hex without hyphens)
 * The parameter name 'id' is required by the backend, not 'sha'.
 */

import { UpdateModMetadataRequest } from '../mod.types';

/**
 * Comprehensive mapping of IPC message types to their payload structures
 */
export interface ModIpcRequests {
  // ============= Query Operations (No Payload) =============
  GET_ALL: Record<string, never>;                    // No payload required
  GET_LOADED: Record<string, never>;                 // No payload required
  GET_ACTIVE_MODS: Record<string, never>;            // No payload required
  GET_AUTHORS: Record<string, never>;                // No payload required
  GET_TAGS: Record<string, never>;                   // No payload required
  GET_STATISTICS: Record<string, never>;             // No payload required
  GET_UNCLASSIFIED_MODS: Record<string, never>;      // No payload required
  GET_UNCLASSIFIED_COUNT: Record<string, never>;     // No payload required
  GET_ALL_TAGS: Record<string, never>;               // No payload required
  GET_USED_TAG_NAMES: Record<string, never>;         // No payload required
  CHECK_CLIPBOARD_HAS_IMAGE: Record<string, never>;  // No payload required

  // ============= Single ID Operations =============
  GET_BY_ID: { id: string };
  LOAD: { id: string };
  UNLOAD: { id: string };
  DELETE: { id: string };
  DELETE_CACHE: { id: string };
  UPDATE_ARCHIVE_FROM_CACHE: { id: string };
  GET_PREVIEW_PATHS: { id: string };
  CHECK_FILE_PATHS: { id: string };
  GET_KEYBINDINGS: { id: string };
  REORDER_KEYBINDINGS: { id: string; keys: string[] };
  MERGE_MODS: { ids: string[]; name: string; key: string; activeOnly?: boolean };
  GET_INI_FILES: { id: string };
  UPDATE_INI_ENTRY: { id: string; relativePath: string; lineIndex: number; newValue: string };

  // ============= Import/Export Operations =============
  IMPORT: { filePath: string };
  UPDATE_MOD: { id: string; filePath: string };
  EXPORT: { id: string; targetPath: string };

  // ============= Batch Operations =============
  BATCH_DELETE: { ids: string[] };
  BATCH_DELETE_CACHES: { ids: string[] };
  BATCH_UPDATE_CATEGORY: { updates: Record<string, string> };
  BATCH_UPDATE_METADATA: { updates: Record<string, UpdateModMetadataRequest> };

  // ============= Metadata Operations =============
  UPDATE_METADATA: { id: string } & UpdateModMetadataRequest;
  UPDATE_CATEGORY: { id: string; category: string };

  // ============= Search Operations =============
  SEARCH: { searchTerm: string };
  GET_MODS_BY_CATEGORY: { categoryId: string };

  // ============= Image Operations =============
  IMPORT_PREVIEW_IMAGE: { id: string; imagePath: string };
  IMPORT_PREVIEW_FROM_CLIPBOARD: { id: string };
  COPY_PREVIEW_TO_CLIPBOARD: { previewPath: string };
  SET_THUMBNAIL: { id: string; previewPath: string };
  DELETE_PREVIEW: { id: string; previewPath: string };

  // ============= Tag Operations =============
  GET_TAG_BY_NAME: { name: string };
  UPSERT_TAG: { name: string; color: string };
  DELETE_TAG: { name: string };
  GET_TAG_USAGE_COUNT: { tag: string };
  SEARCH_TAGS: { searchTerm?: string };

  // ============= Preset Operations =============
  GET_PRESETS: Record<string, never>;
  SAVE_PRESET: { name: string };
  UPDATE_PRESET: { id: string; name: string };
  OVERWRITE_PRESET: { id: string };
  DELETE_PRESET: { id: string };
  APPLY_PRESET: { id: string };
  UNLOAD_ALL_MODS: Record<string, never>;
}

/**
 * Helper type to extract payload type for a specific IPC message
 *
 * @example
 * type LoadPayload = ModIpcPayload<'LOAD'>;  // { id: string }
 * type UpdatePayload = ModIpcPayload<'UPDATE_METADATA'>;  // { id: string } & UpdateModMetadataRequest
 */
export type ModIpcPayload<T extends keyof ModIpcRequests> = ModIpcRequests[T];