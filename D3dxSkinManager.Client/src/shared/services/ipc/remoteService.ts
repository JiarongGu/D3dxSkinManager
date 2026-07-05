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
  RemoteIndexPage,
  RemoteModDetail,
  RemoteResolveResult,
  RemoteSourceConfigDto,
  RemoteSourceInfo,
  RemoteSourceTestResult,
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
    sourceId: string,
    detail: RemoteModDetail,
    option: RemoteDownloadOption,
  ): Promise<RemoteDownloadImportAck> {
    return this.sendMessage<RemoteDownloadImportAck>('DOWNLOAD_IMPORT', profileId, { sourceId, detail, option });
  }

  /** Filtered + paged slice of the SYNCED local index (instant; empty info when never synced). */
  async indexQuery(
    profileId: string,
    sourceId: string,
    listId: string,
    search: string | undefined,
    page: number,
    pageSize: number,
  ): Promise<RemoteIndexPage> {
    return this.sendMessage<RemoteIndexPage>('INDEX_QUERY', profileId, { sourceId, listId, search, page, pageSize });
  }

  /** Start a background crawl of all list pages (Activity panel). Immediate ack. */
  async indexSync(profileId: string, sourceId: string, listId: string): Promise<RemoteDownloadImportAck> {
    return this.sendMessage<RemoteDownloadImportAck>('INDEX_SYNC', profileId, { sourceId, listId });
  }

  /** Validate + persist a (possibly user-authored) adapter. */
  async saveSource(profileId: string, config: RemoteSourceConfigDto): Promise<RemoteSourceConfigDto> {
    return this.sendMessage<RemoteSourceConfigDto>('SAVE_SOURCE', profileId, { config });
  }

  async deleteSource(profileId: string, sourceId: string): Promise<boolean> {
    return this.sendMessage<boolean>('DELETE_SOURCE', profileId, { sourceId });
  }

  /** Run a candidate config against the live site (page 1 + first detail). */
  async testSource(profileId: string, config: RemoteSourceConfigDto, listId?: string): Promise<RemoteSourceTestResult> {
    return this.sendMessage<RemoteSourceTestResult>('TEST_SOURCE', profileId, { config, listId });
  }

  async getSourceTemplate(profileId: string): Promise<string> {
    return this.sendMessage<string>('GET_SOURCE_TEMPLATE', profileId);
  }

  /** The FULL adapter config (for the edit screen; GET_SOURCES only carries display info). */
  async getSourceConfig(profileId: string, sourceId: string): Promise<RemoteSourceConfigDto> {
    return this.sendMessage<RemoteSourceConfigDto>('GET_SOURCE_CONFIG', profileId, { sourceId });
  }
}
