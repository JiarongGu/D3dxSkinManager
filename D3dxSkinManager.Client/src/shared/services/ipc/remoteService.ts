/**
 * Remote mod library IPC service — REMOTE module (RemoteFacade).
 * Browse configured remote sites, resolve downloads, kick off download+import
 * (fire-and-forget; progress arrives via the Activity panel / ProcessRegistry).
 */

import { BaseModuleService } from '../baseModuleService';
import type {
  RemoteBrowseResult,
  RemoteDownloadImportAck,
  RemoteDownloadOption,
  RemoteModDetail,
  RemoteResolveResult,
  RemoteSourceInfo,
} from '../../types/remote.types';

export class RemoteService extends BaseModuleService {
  constructor() {
    super('REMOTE');
  }

  async getSources(profileId: string): Promise<RemoteSourceInfo[]> {
    return this.sendArrayMessage<RemoteSourceInfo>('GET_SOURCES', profileId);
  }

  async browse(profileId: string, sourceId: string, listId: string, page: number): Promise<RemoteBrowseResult> {
    return this.sendMessage<RemoteBrowseResult>('BROWSE', profileId, { sourceId, listId, page });
  }

  async search(profileId: string, sourceId: string, query: string): Promise<RemoteBrowseResult> {
    return this.sendMessage<RemoteBrowseResult>('SEARCH', profileId, { sourceId, query });
  }

  async getDetail(profileId: string, sourceId: string, url: string): Promise<RemoteModDetail> {
    return this.sendMessage<RemoteModDetail>('GET_DETAIL', profileId, { sourceId, url });
  }

  async resolveDownload(profileId: string, option: RemoteDownloadOption): Promise<RemoteResolveResult> {
    return this.sendMessage<RemoteResolveResult>('RESOLVE_DOWNLOAD', profileId, { option });
  }

  /** Immediate ack — the download+import runs in the background (Activity panel). */
  async downloadImport(
    profileId: string,
    detail: RemoteModDetail,
    option: RemoteDownloadOption,
  ): Promise<RemoteDownloadImportAck> {
    return this.sendMessage<RemoteDownloadImportAck>('DOWNLOAD_IMPORT', profileId, { detail, option });
  }
}
