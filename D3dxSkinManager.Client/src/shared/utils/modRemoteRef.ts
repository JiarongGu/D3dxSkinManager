/**
 * Parse the remote-library identity a remote import records in ModEntity.Metadata
 * (RemoteImportService.WriteRemoteMetadata: metadata.remote = { sourceId, listId, entryId,
 * detailUrl, sha256, importedAtUtc }). Lets the mod detail panel link back to the mod's
 * remote source page.
 */

export interface ModRemoteRef {
  sourceId: string;
  listId?: string;
  entryId?: string;
  detailUrl: string;
}

export function parseModRemoteRef(metadata?: string): ModRemoteRef | undefined {
  if (!metadata) return undefined;
  try {
    const remote = JSON.parse(metadata)?.remote;
    if (!remote?.sourceId || !remote?.detailUrl) return undefined;
    return {
      sourceId: remote.sourceId,
      listId: remote.listId ?? undefined,
      entryId: remote.entryId ?? undefined,
      detailUrl: remote.detailUrl,
    };
  } catch {
    return undefined;
  }
}
